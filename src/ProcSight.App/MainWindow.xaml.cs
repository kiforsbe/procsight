using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using System.Collections.ObjectModel;
using ProcSight.App.ViewModels;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
using ProcSight.Application.Presentation;
namespace ProcSight.App;
public sealed partial class MainWindow : Window
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly WindowsSnapshotSource collector = new();
    private readonly LiveRowCollection rows = [];
    private readonly Dictionary<string, ProcessRow> knownRows = [];
    private IReadOnlyList<ResolvedEntity> entities = [];
    private string page = "Processes", search = "";
    private StackPanel? detailPanel;
    private ScrollViewer? detailScroll;
    private string? detailFingerprint;
    private string? detailSelectionKey;
    private string? detailEvidenceScope;
    private int detailVersion;
    private TextBlock? status;
    private ListView? table;
    private MachineSnapshot lastSnapshot=new(Guid.NewGuid(),DateTimeOffset.UtcNow,[],[]);
    private ProcessSort sort=ProcessSort.Name;
    private bool sortDescending;
    private readonly HashSet<string> expandedGroups=[];
    private readonly HashSet<string> collapsedTree=[];
    private string? classificationFilter;
    private bool refreshingRows;
    private DateTimeOffset prunedAt;
    private readonly SemaphoreSlim knowledgeMaintenance=new(1);
    private StackPanel? evidencePanel;
    public MainWindow()
    {
        InitializeComponent(); Title = "ProcSight";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory,"Assets","ProcSight.ico"));
#if DEBUG
        if(TryStartVisualFixture()) return;
#endif
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1250, 820));
        AppWindow.Show();
        LoadRuntime();
        ConfigureChrome();
        if(elevated) { Title="ProcSight (Administrator)"; AppTitleBar.Title="ProcSight (Administrator)"; }
        ConfigureTray();
        performanceLoop=RunPerformanceAsync();
        Closed += async (_, _) => {
            lifetime.Cancel();
            // Stop the owned kernel session before the first await and final dispatcher exit.
            collector.Stop(); tray?.Dispose();
            if(trayLoop is not null) await trayLoop;
            if(performanceLoop is not null) await performanceLoop;
            traySampler.Dispose(); await performanceSampler.DisposeAsync(); await collector.DisposeAsync();
            await coordinator.DisposeAsync(); providerHttp.Dispose();
        };
        RenderPage();
        _ = CollectAsync();
        _ = ReadAnalysisAsync();
        if(elevated) ProcSight.Windows.Security.ElevationLauncher.MarkReady(WinRT.Interop.WindowNative.GetWindowHandle(this));
    }
    private void Navigate(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (PageContent is null) return;
        page = args.IsSettingsSelected ? "Settings" : (args.SelectedItem as NavigationViewItem)?.Tag?.ToString() ?? "Processes";

        RenderPage();
    }
    private void RenderPage()
    {
        if(PageContent is null) return;
        PageContent.Children.Clear();
        updatePerformanceView=null;
        changePerformanceSection=null;
        if(page=="Performance") { RenderPerformance(); return; }
        if(page=="Settings") { RenderSettings(); return; }
        if(page=="Knowledge") { RenderKnowledge(); return; }
        if(page=="AI activity") { RenderActivity(); return; }
        RenderProcessPage();
    }
    private void SearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        search=sender.Text;
        if(PageContent is not null) RefreshRows(reorder:true);
    }
    private async Task CollectAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            try
            {
                var mode=policy.CommandLineMode;
                var snapshot = await collector.CaptureAsync(mode, lifetime.Token);
                var resolved = coordinator.ResolveEntities(snapshot,EffectivePolicy);
                DispatcherQueue.TryEnqueue(() => { lastSnapshot=snapshot; entities = resolved; RefreshRows(); });
                try { await knowledgeMaintenance.WaitAsync(lifetime.Token);
                    try { await Task.Run(async()=>
                    {
                        if(DateTimeOffset.UtcNow-prunedAt>TimeSpan.FromDays(1))
                        { await knowledge.PruneAsync(DateTimeOffset.UtcNow,lifetime.Token); prunedAt=DateTimeOffset.UtcNow; }
                        await coordinator.ObserveAsync(snapshot,EffectivePolicy,lifetime.Token);
                    },lifetime.Token); } finally { knowledgeMaintenance.Release(); } }
                catch(Exception e) when(e is not OperationCanceledException)
                { DispatcherQueue.TryEnqueue(()=> { if(status is not null) status.Text="Knowledge unavailable; local collection continues. See Settings for recovery."; }); }
                await Task.Delay(1000, lifetime.Token);
            }
            catch (OperationCanceledException) { break; }
            catch { DispatcherQueue.TryEnqueue(() => { if (status is not null) status.Text = "Collection temporarily unavailable; retrying."; }); await Task.Delay(2000); }
        }
    }
    private void RefreshRows(bool reorder=false)
    {
        if (page is not ("Processes" or "Services")) return;
        var selected = (table?.SelectedItem as ProcessRow)?.Projection;
        var selections=table?.SelectedItems.OfType<ProcessRow>().Select(r=>r.Projection).ToArray()??[];
        var projected=ProcessListProjection.Build(lastSnapshot,entities,analysis.Values.ToList(),
            new(search,presentation.Grouped,page=="Services",sort,expandedGroups,classificationFilter,
                reorder?null:rows.Select(r=>r.Key).ToArray(),collapsedTree,sortDescending));
        var selectedKey=ProcessListProjection.SelectionKey(projected,selected);
        var ordered = new List<ProcessRow>();
        foreach (var projectedRow in projected.Rows)
        {
            var entity=projectedRow.Entity;
            var key = projectedRow.Key;
            if (!knownRows.TryGetValue(key, out var row)) knownRows[key] = row = new() { Key = key };
            row.TotalMemoryBytes=lastSnapshot.SystemResources?.TotalPhysicalBytes;
            if(!ReferenceEquals(row.TableColumns,tableColumnWidths)) row.TableColumns=tableColumnWidths;
            row.Update(projectedRow); ordered.Add(row);
            QueueIcon(row);
            if(analysis.TryGetValue(entity.Identity,out var finding)) row.SetAnalysis(finding);
        }
        var wanted = ordered.Select(r => r.Key).ToHashSet();
        refreshingRows=true;
        try {
        if(reorder&&Math.Max(rows.Count,ordered.Count)>64&&!rows.SequenceEqual(ordered)) rows.ReplaceOrder(ordered);
        else {
            for (var i = rows.Count-1; i >=0; i--) if (!wanted.Contains(rows[i].Key)) rows.RemoveAt(i);
            for (var i = 0; i < ordered.Count; i++) {
                if(i<rows.Count&&ReferenceEquals(rows[i],ordered[i])) continue;
                var index = rows.IndexOf(ordered[i]);
                if (index < 0) rows.Insert(i,ordered[i]); else if (index != i) rows.Move(index,i);
            }
        }
        var live = entities.Select(ProcessListProjection.RowKey).Concat(projected.Rows.Select(r=>r.Key)).ToHashSet();
        foreach (var key in knownRows.Keys.Where(k => !live.Contains(k)).ToArray()) knownRows.Remove(key);
        if(table is not null) {
            var selectedKeys=selections.Select(s=>ProcessListProjection.SelectionKey(projected,s)).Where(k=>k is not null).ToHashSet();
            if(selectedKey is not null) selectedKeys.Add(selectedKey);
            foreach(var row in table.SelectedItems.OfType<ProcessRow>().Where(r=>!selectedKeys.Contains(r.Key)).ToArray()) table.SelectedItems.Remove(row);
            foreach(var row in rows.Where(r=>selectedKeys.Contains(r.Key)&&!table.SelectedItems.Contains(r))) table.SelectedItems.Add(row);
        }
        } finally { refreshingRows=false; }
        var active=entities.Select(e=>e.Identity).ToHashSet();
        foreach(var id in analysis.Keys.Where(id=>!active.Contains(id)).ToArray()) analysis.Remove(id);
        ShowDetail(table?.SelectedItem as ProcessRow);
        RefreshResourceSummary();
        if (status is not null) status.Text = $"{entities.Count(e=>page=="Processes"?e.Process is not null:e.Service is not null)} {page.ToLowerInvariant()} · {rows.Count} visible rows"+
            (page=="Processes"&&lastSnapshot.Processes.FirstOrDefault()?.Resources.DiskBytesPerSecond.Reason is { } ioReason?" · "+ioReason:"");
    }
    private void ToggleExpansion(object sender,RoutedEventArgs args)
    {
        if(sender is not Button { Tag:string key }) return;
        var row=rows.FirstOrDefault(r=>r.Key==key); if(row is null) return;
        if(table is not null) table.SelectedItem=row;
        var state=row.IsGroup?expandedGroups:collapsedTree;
        if(!state.Add(key)) state.Remove(key);
        RefreshRows();
    }
    private void SavePresentation()
    { if(preferences is null) return; _=SaveViewAsync(); }
    private async Task SaveViewAsync()
    { try { if(!(await ApplySettingsAsync(policy)).Persisted) runtimeWarning="View preferences could not be saved."; } catch { runtimeWarning="View preferences could not be saved."; } }
}

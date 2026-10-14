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
    private readonly ObservableCollection<ProcessRow> rows = [];
    private readonly Dictionary<string, ProcessRow> knownRows = [];
    private IReadOnlyList<ResolvedEntity> entities = [];
    private string page = "Processes", search = "";
    private TextBlock? detail;
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
#if DEBUG
        if(TryStartVisualFixture()) return;
#endif
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1250, 820));
        AppWindow.Show();
        LoadRuntime();
        ConfigureChrome();
        Closed += async (_, _) => { lifetime.Cancel(); await coordinator.DisposeAsync(); providerHttp.Dispose(); };
        RenderPage();
        _ = CollectAsync();
        _ = ReadAnalysisAsync();
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
        if(page=="Settings") { RenderSettings(); return; }
        if(page=="Knowledge") { RenderKnowledge(); return; }
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
                var resolved = coordinator.ResolveEntities(snapshot,policy);
                DispatcherQueue.TryEnqueue(() => { lastSnapshot=snapshot; entities = resolved; RefreshRows(); });
                try { await knowledgeMaintenance.WaitAsync(lifetime.Token);
                    try { await Task.Run(async()=>
                    {
                        if(DateTimeOffset.UtcNow-prunedAt>TimeSpan.FromDays(1))
                        { await knowledge.PruneAsync(DateTimeOffset.UtcNow,lifetime.Token); prunedAt=DateTimeOffset.UtcNow; }
                        await coordinator.ObserveAsync(snapshot,policy,lifetime.Token);
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
        var selected = (table?.SelectedItem as ProcessRow)?.Key;
        var projected=ProcessListProjection.Build(lastSnapshot,entities,analysis.Values.ToList(),
            new(search,presentation.Grouped,page=="Services",sort,expandedGroups,classificationFilter,
                reorder?null:rows.Select(r=>r.Key).ToArray(),collapsedTree,sortDescending));
        var ordered = new List<ProcessRow>();
        foreach (var projectedRow in projected.Rows)
        {
            var entity=projectedRow.Entity;
            var key = projectedRow.Key;
            if (!knownRows.TryGetValue(key, out var row)) knownRows[key] = row = new() { Key = key };
            row.TotalMemoryBytes=lastSnapshot.SystemResources?.TotalPhysicalBytes;
            row.Update(projectedRow); ordered.Add(row);
            QueueIcon(row);
            if(analysis.TryGetValue(entity.Identity,out var finding)) row.SetAnalysis(finding);
        }
        var wanted = ordered.Select(r => r.Key).ToHashSet();
        refreshingRows=true;
        try {
        for (var i = rows.Count-1; i >=0; i--) if (!wanted.Contains(rows[i].Key)) rows.RemoveAt(i);
        for (var i = 0; i < ordered.Count; i++)
        {
            var index = rows.IndexOf(ordered[i]);
            if (index < 0) rows.Insert(i,ordered[i]); else if (index != i) rows.Move(index,i);
        }
        var live = entities.Select(ProcessListProjection.RowKey).Concat(projected.Rows.Select(r=>r.Key)).ToHashSet();
        foreach (var key in knownRows.Keys.Where(k => !live.Contains(k)).ToArray()) knownRows.Remove(key);
        if (selected is not null && table is not null) table.SelectedItem = rows.FirstOrDefault(r => r.Key == selected);
        } finally { refreshingRows=false; }
        var active=entities.Select(e=>e.Identity).ToHashSet();
        foreach(var id in analysis.Keys.Where(id=>!active.Contains(id)).ToArray()) analysis.Remove(id);
        ShowDetail(table?.SelectedItem as ProcessRow);
        RefreshResourceSummary();
        if (status is not null) status.Text = $"{entities.Count(e=>page=="Processes"?e.Process is not null:e.Service is not null)} {page.ToLowerInvariant()} · {rows.Count} visible rows · disk/network not collected";
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
    private void ShowDetail(ProcessRow? row)
    {
        if (!detailsOpen || detail is null) return;
        if(row is null) { detail.Text="Select a process or service to inspect its identity and evidence."; evidencePanel?.Children.Clear(); return; }
        var e = row.Entity;
        var localRole=ProcessRoleLabels.Describe(e);
        detail.Text = $"{row.Name}\n\nOwnership: {e.Owner ?? "Unknown"}\n\n" +
            (localRole.UsesCommandContext?$"Local role: {localRole.Label}\n{localRole.Explanation}\n\n":"")+
            ProcessDetails.FormatIdentity(e,policy.CommandLineMode) +
            "\n\n" + FormatAnalysis(e);
        _=ShowEvidenceAsync(e);
    }
    private void SavePresentation()
    { if(preferences is null) return; try { preferences.Save(policy,presentation); } catch { runtimeWarning="View preferences could not be saved."; } }
}

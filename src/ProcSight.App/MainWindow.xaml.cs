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
    private readonly HashSet<string> expandedGroups=[];
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
        if(PageContent is not null) RefreshRows();
    }
    private async Task CollectAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            try
            {
                var mode=policy.CommandLineMode;
                var snapshot = await collector.CaptureAsync(mode, lifetime.Token);
                if(mode!=policy.CommandLineMode) snapshot=snapshot with { Processes=snapshot.Processes.Select(p=>p with { CommandLine=null }).ToList() };
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
    private void RefreshRows()
    {
        if (page is not ("Processes" or "Services")) return;
        var selected = (table?.SelectedItem as ProcessRow)?.Key;
        var projected=ProcessListProjection.Build(lastSnapshot,entities,analysis.Values.ToList(),
            new(search,presentation.Grouped,page=="Services",sort,expandedGroups,classificationFilter));
        var ordered = new List<ProcessRow>();
        foreach (var projectedRow in projected.Rows)
        {
            var entity=projectedRow.Entity;
            var key = projectedRow.Key;
            if (!knownRows.TryGetValue(key, out var row)) knownRows[key] = row = new() { Key = key };
            row.Expanded=expandedGroups.Contains(key); row.TotalMemoryBytes=lastSnapshot.SystemResources?.TotalPhysicalBytes;
            row.Update(projectedRow); ordered.Add(row);
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
        var live = entities.Select(e => e.Process?.Key.ToString() ?? e.Identity.Value).Concat(projected.Rows.Select(r=>r.Key)).ToHashSet();
        foreach (var key in knownRows.Keys.Where(k => !live.Contains(k)).ToArray()) knownRows.Remove(key);
        if (selected is not null && table is not null) table.SelectedItem = rows.FirstOrDefault(r => r.Key == selected);
        } finally { refreshingRows=false; }
        var active=entities.Select(e=>e.Identity).ToHashSet();
        foreach(var id in analysis.Keys.Where(id=>!active.Contains(id)).ToArray()) analysis.Remove(id);
        ShowDetail(table?.SelectedItem as ProcessRow);
        RefreshResourceSummary();
        if (status is not null) status.Text = $"{entities.Count(e=>page=="Processes"?e.Process is not null:e.Service is not null)} {page.ToLowerInvariant()} · {rows.Count} visible rows · disk/network not collected";
    }
    private void ShowDetail(ProcessRow? row)
    {
        if (!detailsOpen || detail is null) return;
        if(row is null) { detail.Text="Select a process or service to inspect its identity and evidence."; evidencePanel?.Children.Clear(); return; }
        var e = row.Entity;
        detail.Text = $"{e.DisplayName}\n\nOwnership: {e.Owner ?? "Unknown"}\n\n" +
            (e.Process is { } p ? $"PID: {p.Key.Pid}\nExecutable: {p.Executable.Name}\nPath: {p.Executable.FullPath ?? "Unavailable"}\nProduct: {p.Executable.Product ?? "Unavailable"}\nCompany metadata: {p.Executable.Company ?? "Unavailable"}\nSignature: {p.Executable.Signature}\nServices: {string.Join(", ",p.ServiceNames)}" :
            $"Service: {e.Service!.Name}\nState: {e.Service.State}\nStart mode: {e.Service.StartMode}\nHost PID: {e.Service.HostPid}\nBinary: {e.Service.BinaryPath}") +
            "\n\n" + FormatAnalysis(e);
        _=ShowEvidenceAsync(e);
    }
    private void SavePresentation()
    { if(preferences is null) return; try { preferences.Save(policy,presentation); } catch { runtimeWarning="View preferences could not be saved."; } }
}

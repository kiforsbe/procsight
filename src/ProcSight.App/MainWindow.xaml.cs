using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using System.Collections.ObjectModel;
using ProcSight.App.ViewModels;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
namespace ProcSight.App;
public sealed partial class MainWindow : Window
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly WindowsSnapshotSource collector = new();
    private readonly IdentityResolver resolver = new();
    private readonly ObservableCollection<ProcessRow> rows = [];
    private readonly Dictionary<string, ProcessRow> knownRows = [];
    private IReadOnlyList<ResolvedEntity> entities = [];
    private string page = "Processes", search = "";
    private TextBlock? detail;
    private TextBlock? status;
    private ListView? table;
    public MainWindow()
    {
        InitializeComponent(); Title = "ProcSight";
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1250, 820));
        Closed += (_, _) => lifetime.Cancel();
        RenderPage();
        _ = CollectAsync();
    }
    private void Navigate(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (PageTitle is null) return;
        page = args.IsSettingsSelected ? "Settings" : (args.SelectedItem as NavigationViewItem)?.Tag?.ToString() ?? "Processes";
        PageTitle.Text = page;
        RenderPage();
    }
    private void RenderPage()
    {
        if (PageContent is null) return;
        PageContent.Children.Clear();
        if (page is "Settings" or "Knowledge")
        {
            PageContent.Children.Add(new TextBlock { Text = page == "Settings" ?
                "Collection is local and read-only. AI provider configuration is being implemented." :
                "Reusable analysis rules will appear here as the analysis engine is implemented.", TextWrapping = TextWrapping.Wrap });
            return;
        }
        var grid = new Grid { RowSpacing = 8, ColumnSpacing = 16 };
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new() { Width = new(330) });
        var box = new AutoSuggestBox { PlaceholderText = "Search name, publisher, owner, service or PID", Text = search };
        box.TextChanged += (_, _) => { search = box.Text; RefreshRows(); };
        Grid.SetColumnSpan(box, 2); grid.Children.Add(box);
        table = new ListView { ItemsSource = rows, SelectionMode = ListViewSelectionMode.Single,
            ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <Grid Padding="4,8" ColumnSpacing="12"><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="55"/><ColumnDefinition Width="70"/><ColumnDefinition Width="95"/></Grid.ColumnDefinitions>
            <StackPanel><TextBlock Text="{Binding Name}" TextTrimming="CharacterEllipsis"/><TextBlock Text="{Binding Owner}" FontSize="12" Opacity="0.65" TextTrimming="CharacterEllipsis"/></StackPanel>
            <TextBlock Grid.Column="1" Text="{Binding Pid}"/><TextBlock Grid.Column="2" Text="{Binding Cpu}"/><TextBlock Grid.Column="3" Text="{Binding Memory}"/></Grid></DataTemplate>
            """) };
        table.SelectionChanged += (_, _) => ShowDetail(table.SelectedItem as ProcessRow);
        Grid.SetRow(table,1); grid.Children.Add(table);
        detail = new TextBlock { Text = "Select a process or service to inspect its metadata.", TextWrapping = TextWrapping.Wrap };
        var scroll = new ScrollViewer { Content = detail };
        Grid.SetRow(scroll,1); Grid.SetColumn(scroll,1); grid.Children.Add(scroll);
        status = new TextBlock { Opacity = .7, Text = "Collecting local metadata… CPU / memory shown; disk and network unavailable." };
        Grid.SetRow(status,2); Grid.SetColumnSpan(status,2); grid.Children.Add(status);
        PageContent.Children.Add(grid); RefreshRows();
    }
    private async Task CollectAsync()
    {
        while (!lifetime.IsCancellationRequested)
        {
            try
            {
                var snapshot = await collector.CaptureAsync(CommandLineMode.Off, lifetime.Token);
                var resolved = resolver.Resolve(snapshot, []);
                DispatcherQueue.TryEnqueue(() => { entities = resolved; RefreshRows(); });
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
        var visible = entities.Where(e => page == "Processes" ? e.Process is not null : e.Service is not null)
            .Where(e => string.IsNullOrEmpty(search) || string.Join(" ", e.Facts.Values.Append(e.DisplayName)
                .Append(e.Process?.Key.Pid.ToString() ?? "")).Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var ordered = new List<ProcessRow>();
        foreach (var entity in visible)
        {
            var key = entity.Process?.Key.ToString() ?? entity.Identity.Value;
            if (!knownRows.TryGetValue(key, out var row)) knownRows[key] = row = new() { Key = key };
            row.Update(entity); ordered.Add(row);
        }
        var wanted = ordered.Select(r => r.Key).ToHashSet();
        for (var i = rows.Count-1; i >=0; i--) if (!wanted.Contains(rows[i].Key)) rows.RemoveAt(i);
        for (var i = 0; i < ordered.Count; i++)
        {
            var index = rows.IndexOf(ordered[i]);
            if (index < 0) rows.Insert(i,ordered[i]); else if (index != i) rows.Move(index,i);
        }
        var live = entities.Select(e => e.Process?.Key.ToString() ?? e.Identity.Value).ToHashSet();
        foreach (var key in knownRows.Keys.Where(k => !live.Contains(k)).ToArray()) knownRows.Remove(key);
        if (selected is not null && table is not null) table.SelectedItem = rows.FirstOrDefault(r => r.Key == selected);
        if (status is not null) status.Text = $"{rows.Count} {page.ToLowerInvariant()} · local collection · CPU / memory · disk and network not collected";
    }
    private void ShowDetail(ProcessRow? row)
    {
        if (detail is null || row is null) return;
        var e = row.Entity;
        detail.Text = $"{e.DisplayName}\n\nOwnership: {e.Owner ?? "Unknown"}\n\n" +
            (e.Process is { } p ? $"PID: {p.Key.Pid}\nExecutable: {p.Executable.Name}\nPath: {p.Executable.FullPath ?? "Unavailable"}\nProduct: {p.Executable.Product ?? "Unavailable"}\nCompany metadata: {p.Executable.Company ?? "Unavailable"}\nSignature: {p.Executable.Signature}\nServices: {string.Join(", ",p.ServiceNames)}" :
            $"Service: {e.Service!.Name}\nState: {e.Service.State}\nStart mode: {e.Service.StartMode}\nHost PID: {e.Service.HostPid}\nBinary: {e.Service.BinaryPath}") +
            "\n\nSource: local Windows metadata. Classification analysis is being implemented.";
    }
}

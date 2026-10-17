using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using ProcSight.App.ViewModels;
using ProcSight.Application.Presentation;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private TextBlock? cpuSummary,memorySummary;
    private Border? detailsBorder;
    private Grid? processBody;
    private UIElement? tableHost;
    private AppBarButton? analyzeButton,detailsButton;
    private bool detailsOpen;
    private InfoBar? processIoNotice;
    private readonly Dictionary<ProcessSort,(TextBlock Text,string Label,Button Button)> sortHeaders=[];
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    private void ConfigureChrome()
    {
        ExtendsContentIntoTitleBar=true; SetTitleBar(AppTitleBar);
        var scale=GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))/96.0;
        var display=Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id,Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32((int)Math.Min(1100*scale,display.WorkArea.Width*.92),
            (int)Math.Min(780*scale,display.WorkArea.Height*.92)));
        ConfigureTheme();
    }
    private void ConfigureTheme()
    {
        var background=new global::Windows.UI.ViewManagement.UISettings().GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Background);
        WindowRoot.RequestedTheme=presentation.Theme switch { "Dark"=>ElementTheme.Dark,"Light"=>ElementTheme.Light,
            _=>background.R+background.G+background.B<384?ElementTheme.Dark:ElementTheme.Light };
        AppWindow.TitleBar.ButtonBackgroundColor=Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor=Microsoft.UI.Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor=WindowRoot.RequestedTheme==ElementTheme.Dark?Microsoft.UI.Colors.White:Microsoft.UI.Colors.Black;
    }
    private void RenderProcessPage()
    {
        var root=new Grid();
        root.RowDefinitions.Add(new(){Height=new GridLength(48)});
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});
        root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        root.RowDefinitions.Add(new(){Height=new GridLength(28)});
        var commands=new CommandBar { DefaultLabelPosition=CommandBarDefaultLabelPosition.Right,
            VerticalContentAlignment=VerticalAlignment.Center,
            Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent),IsDynamicOverflowEnabled=true,
            Content=new TextBlock { Text=page,FontSize=16,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,
                Margin=new Thickness(16,0,24,0),VerticalAlignment=VerticalAlignment.Center } };
        analyzeButton=new AppBarButton { Label="Analyze",Icon=new SymbolIcon(Symbol.Find),IsEnabled=false };
        analyzeButton.Click+=async(_,_)=> { if(table?.SelectedItem is ProcessRow { IsGroup:false } row)
            await coordinator.RequestReanalysisAsync(row.Entity.Identity,lifetime.Token); };
        detailsButton=new AppBarButton { Label="Details",Icon=new SymbolIcon(Symbol.Document) };
        detailsButton.Click+=(_,_)=> { detailsOpen=!detailsOpen; UpdateDetailsLayout(); ShowDetail(table?.SelectedItem as ProcessRow); };
        var refresh=new AppBarButton { Label="Refresh",Icon=new SymbolIcon(Symbol.Refresh) };
        refresh.Click+=(_,_)=> { collector.RequestRefresh(); RefreshRows(reorder:true); if(status is not null) status.Text="Refreshing local metadata…"; };
        commands.PrimaryCommands.Add(analyzeButton); commands.PrimaryCommands.Add(detailsButton); commands.PrimaryCommands.Add(refresh);
        var columns=new AppBarButton { Label="Choose columns",Icon=new SymbolIcon(Symbol.List) };
        columns.Click+=async(_,_)=>await ChooseColumnsAsync(); commands.SecondaryCommands.Add(columns);
        var export=new AppBarButton { Label="Export…",Icon=new SymbolIcon(Symbol.Save) };
        export.Click+=async(_,_)=>await ExportInventoryAsync(); commands.SecondaryCommands.Add(export);
        var grouped=new AppBarToggleButton { Label="Group by application",Icon=new SymbolIcon(Symbol.AllApps),IsChecked=presentation.Grouped };
        grouped.Click+=(_,_)=> { presentation=presentation with { Grouped=grouped.IsChecked==true }; SavePresentation(); RefreshRows(); };
        commands.SecondaryCommands.Add(grouped);
        if(page=="Processes") {
            var autoSort=new AppBarToggleButton { Label="Automatically sort live values",IsChecked=presentation.AutomaticProcessSorting };
            autoSort.Click+=(_,_)=> { presentation=presentation with { AutomaticProcessSorting=autoSort.IsChecked==true }; SavePresentation(); RefreshRows(); };
            commands.SecondaryCommands.Add(autoSort);
        }
        var raw=new AppBarButton { Label="Process tree",Icon=new SymbolIcon(Symbol.List) };
        raw.Click+=(_,_)=> { presentation=presentation with { Grouped=false }; grouped.IsChecked=false; SavePresentation(); RefreshRows(); };
        commands.SecondaryCommands.Add(raw);
        var filter=new AppBarButton { Label="Filter classifications",Icon=new SymbolIcon(Symbol.Filter) };
        var flyout=new MenuFlyout();
        foreach(var (label,value) in new (string,string?)[]{("All classifications",null),("Unknown","Unknown"),("Suspicious","Suspicious"),
            ("Known threat evidence","KnownThreat"),("Optional","Optional"),("Potentially unwanted","PotentiallyUnwanted"),
            ("Possible leftovers","PossibleLeftover"),("Developer tools","DeveloperTool"),("OS components","OSComponent")})
        {
            var option=new MenuFlyoutItem { Text=label };
            option.Click+=(_,_)=> { classificationFilter=value; RefreshRows(); }; flyout.Items.Add(option);
        }
        filter.Flyout=flyout; commands.SecondaryCommands.Add(filter); root.Children.Add(commands);

        processIoNotice=new InfoBar { Name="ProcessIoAvailability",IsOpen=page=="Processes"&&!elevated,IsClosable=true,
            Severity=InfoBarSeverity.Informational,Title="Process disk/network monitoring requires administrator mode",
            Message="Restart elevates the entire app. Online analysis then requires administrator-session consent.",Margin=new Thickness(12,0,12,8) };
        if(!elevated&&page=="Processes") {
            var restart=new Button { Content="Restart as administrator",IsEnabled=ProcSight.Windows.Security.PackageContext.Inspect().CanRestartElevated };
            var notice=processIoNotice;
            restart.Click+=async(_,_)=>await RestartAdministratorAsync(value=>restart.IsEnabled=value,value=>notice.Message=value);
            processIoNotice.ActionButton=restart;
            if(!restart.IsEnabled) processIoNotice.Message="This package cannot restart elevated. See Settings → Advanced → Administrator access for details.";
        }
        Grid.SetRow(processIoNotice,1); root.Children.Add(processIoNotice);
        processBody=new Grid(); processBody.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        processBody.ColumnDefinitions.Add(new(){Width=GridLength.Auto}); Grid.SetRow(processBody,2); root.Children.Add(processBody);
        var surface=new Grid();
        surface.RowDefinitions.Add(new(){Height=new GridLength(52)}); surface.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var header=new Grid { HorizontalAlignment=HorizontalAlignment.Left,BorderThickness=new Thickness(0,1,0,1),
            BorderBrush=(Brush)Microsoft.UI.Xaml.Application.Current.Resources["DividerStrokeColorDefaultBrush"] };
        InitializeColumns(); tableHeader=header; PopulateColumnHeader(header);
        var headerScroll=new ScrollViewer { Content=header,HorizontalScrollMode=ScrollMode.Enabled,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollMode=ScrollMode.Disabled,
            VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,IsTabStop=false };
        surface.Children.Add(headerScroll);
        table=new ListView { ItemsSource=rows,SelectionMode=ListViewSelectionMode.Extended,
            ItemContainerTransitions=new Microsoft.UI.Xaml.Media.Animation.TransitionCollection(),
            ItemsPanel=(ItemsPanelTemplate)XamlReader.Load("""
            <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><ItemsStackPanel ItemsUpdatingScrollMode="KeepItemsInView"/></ItemsPanelTemplate>
            """),
            ItemContainerStyle=(Style)XamlReader.Load("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ListViewItem">
            <Setter Property="MinHeight" Value="28"/><Setter Property="Padding" Value="0"/><Setter Property="Margin" Value="0"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="CornerRadius" Value="0"/></Style>
            """),ItemTemplate=ColumnTemplate() };
        table.SelectionChanged+=(_,_)=>
        {
            if(refreshingRows) return;
            UpdateSelectionActions(); ShowDetail(table.SelectedItem as ProcessRow);
        };
        table.DoubleTapped+=(_,_)=> { if(table.SelectedItem is ProcessRow { IsGroup:false }) { detailsOpen=true; UpdateDetailsLayout(); ShowDetail(table.SelectedItem as ProcessRow); } };
        table.ContextRequested+=ShowRowContextMenu;
        table.ContainerContentChanging+=BindRowChevron;
        table.KeyDown+=(_,args)=>
        {
            if(table.SelectedItem is not ProcessRow selected) return;
            if(selected.Projection.HasChildren && (args.Key==global::Windows.System.VirtualKey.Right&&!selected.Projection.Expanded ||
                args.Key==global::Windows.System.VirtualKey.Left&&selected.Projection.Expanded))
            { ToggleExpansion(new Button { Tag=selected.Key },new RoutedEventArgs()); args.Handled=true; }
            else if(args.Key==global::Windows.System.VirtualKey.Left && selected.Projection.Depth>0)
            {
                var index=rows.IndexOf(selected);
                var parent=rows.Take(index).LastOrDefault(r=>r.Projection.Depth<selected.Projection.Depth);
                if(parent is not null) { table.SelectedItem=parent; table.ScrollIntoView(parent); args.Handled=true; }
            }
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(table,ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollMode(table,ScrollMode.Enabled);
        ScrollViewer.SetVerticalScrollBarVisibility(table,ScrollBarVisibility.Auto);
        table.Loaded+=(_,_)=> {
            if(FindTableScrollViewer(table) is not { } scrolling) return;
            void SynchronizeHeader() {
                var width=Math.Max(TableMinimumWidth,Math.Max(scrolling.ExtentWidth,scrolling.ViewportWidth));
                if(double.IsNaN(header.Width)||Math.Abs(header.Width-width)>.5) header.Width=width;
                if(Math.Abs(headerScroll.HorizontalOffset-scrolling.HorizontalOffset)>.5)
                    headerScroll.ChangeView(scrolling.HorizontalOffset,null,null,true);
            }
            scrolling.ViewChanged+=(_,_)=>SynchronizeHeader();
            headerScroll.ViewChanged+=(_,_)=> {
                if(Math.Abs(headerScroll.HorizontalOffset-scrolling.HorizontalOffset)>.5)
                    scrolling.ChangeView(headerScroll.HorizontalOffset,null,null,true);
            };
            scrolling.SizeChanged+=(_,_)=>SynchronizeHeader(); SynchronizeHeader();
            scrolling.LayoutUpdated+=(_,_)=>SynchronizeHeader();
        };
        Grid.SetRow(table,1); surface.Children.Add(table);
        // The ListView owns both axes; its vertical bar stays inside the visible viewport.
        tableHost=surface;
        processBody.Children.Add(tableHost);
        detailPanel=new StackPanel { Spacing=12,Padding=new Thickness(16) };
        detailFingerprint=null; detailSelectionKey=null; detailVersion++;
        detailsBorder=(Border)XamlReader.Load("""
            <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="420" Visibility="Collapsed"
                Background="{ThemeResource LayerFillColorDefaultBrush}" BorderThickness="1,0,0,0"
                BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}" />
            """);
        detailScroll=new ScrollViewer { Content=detailPanel,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
        detailsBorder.Child=detailScroll;
        Grid.SetColumn(detailsBorder,1); processBody.Children.Add(detailsBorder);
        processBody.SizeChanged+=(_,_)=>UpdateDetailsLayout();
        status=new TextBlock { Text="Collecting local metadata…",FontSize=11,Opacity=.65,Margin=new Thickness(16,0,8,0),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis };
        Grid.SetRow(status,3); root.Children.Add(status); PageContent.Children.Add(root); UpdateDetailsLayout(); RefreshRows(); ConfigureResourceShadingTimer();
    }
    private void SelectSort(ProcessSort column)
    {
        sortDescending=sort==column?!sortDescending:column is ProcessSort.Cpu or ProcessSort.Memory or ProcessSort.Disk or ProcessSort.Network;
        sort=column; presentation=presentation with { Sort=sort.ToString(),SortDescending=sortDescending };
        UpdateSortHeaders(); SavePresentation(); RefreshRows(reorder:true);
    }
    private void UpdateSortHeaders()
    {
        foreach(var (column,header) in sortHeaders) {
            header.Text.Text=header.Label+(column==sort?(sortDescending?" ↓":" ↑"):"");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(header.Button,
                header.Label+(column==sort?(sortDescending?", descending":", ascending"):", sort"));
        }
    }
    private static ScrollViewer? FindTableScrollViewer(DependencyObject element)
    {
        if(element is ScrollViewer scrolling) return scrolling;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(element);i++)
            if(FindTableScrollViewer(VisualTreeHelper.GetChild(element,i)) is { } child) return child;
        return null;
    }
    private void UpdateSelectionActions()
    {
        if(analyzeButton is not null) analyzeButton.IsEnabled=table?.SelectedItems.Count==1&&table.SelectedItem is ProcessRow { IsGroup:false } && ProcSight.Application.Analysis.ConnectionRouter.Resolve(EffectivePolicy).Count>0;
    }
    private void UpdateDetailsLayout()
    {
        if(processBody is null||detailsBorder is null||tableHost is null) return;
        detailsBorder.Visibility=detailsOpen?Visibility.Visible:Visibility.Collapsed;
        var narrow=processBody.ActualWidth<1050;
        Grid.SetColumn(detailsBorder,detailsOpen&&narrow?0:1);
        detailsBorder.Width=detailsOpen&&narrow?double.NaN:420;
        tableHost.Visibility=detailsOpen&&narrow?Visibility.Collapsed:Visibility.Visible;
    }
    private void RefreshResourceSummary()
    {
        if(cpuSummary is not null) cpuSummary.Text=lastSnapshot.SystemResources?.CpuPercent is { } cpu?$"{cpu:F0}%":"—";
        if(memorySummary is not null) memorySummary.Text=lastSnapshot.SystemResources?.MemoryPercent is { } memory?$"{memory:F0}%":"—";
        var latest=performanceHistory.LastOrDefault();
        if(diskSummary is not null) diskSummary.Text=latest?.DiskBusy.Value is { } disk?$"{disk:F0}%":"—";
        if(gpuSummary is not null) gpuSummary.Text=latest?.Gpu.Value is { } gpu?$"{gpu:F0}%":"—";
        if(networkSummary is not null) networkSummary.Text=latest?.NetworkBytes.Value is { } network?$"{network*8/1000000:F1} Mbps":"—";
        UpdateSelectionActions();
    }
}

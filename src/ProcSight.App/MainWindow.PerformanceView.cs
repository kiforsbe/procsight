using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private static readonly PerformanceSubsystem[] PerformanceSections=[PerformanceSubsystem.Overview,PerformanceSubsystem.Cpu,
        PerformanceSubsystem.Memory,PerformanceSubsystem.Disks,PerformanceSubsystem.Network,PerformanceSubsystem.Gpu];
    private Action? changePerformanceSection;
    private void SelectPerformance(PerformanceSubsystem section) {
        if(performanceSubsystem==section) return;
        performanceSubsystem=section; changePerformanceSection?.Invoke();
    }
    private void RenderPerformance() {
        var root=new Grid(); root.RowDefinitions.Add(new() { Height=GridLength.Auto }); root.RowDefinitions.Add(new());
        var toolbar=new Grid { Margin=new Thickness(24,12,24,12),ColumnSpacing=16 };
        toolbar.ColumnDefinitions.Add(new()); toolbar.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
        toolbar.Children.Add(new TextBlock { Text="Performance",FontSize=20,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center });
        var logTime=new ToggleSwitch { Name="PerformanceLogTimeAxis",Header="Logarithmic time axis",IsOn=presentation.PerformanceLogTimeAxis };
        ToolTipService.SetToolTip(logTime,"Give recent samples more horizontal space. The value axis stays unchanged.");
        logTime.Toggled+=(_,_)=> { presentation=presentation with { PerformanceLogTimeAxis=logTime.IsOn }; SavePresentation(); updatePerformanceView?.Invoke(); };
        Grid.SetColumn(logTime,1); toolbar.Children.Add(logTime); root.Children.Add(toolbar);
        var layout=new Grid { ColumnSpacing=0,Name="PerformanceLayout" };
        layout.ColumnDefinitions.Add(new() { Width=new GridLength(190) }); layout.ColumnDefinitions.Add(new());
        Grid.SetRow(layout,1); root.Children.Add(layout); PageContent.Children.Add(root);
        var updates=new List<Action>();
        var sidebar=new ListView { Name="PerformanceSubsystems",SelectionMode=ListViewSelectionMode.Single,
            HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(8,0,8,12) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sidebar,"Performance subsystems");
        foreach(var section in PerformanceSections) {
            var row=new Grid { ColumnSpacing=10,Padding=new Thickness(4,10,4,10),MinHeight=62 };
            row.ColumnDefinitions.Add(new() { Width=new GridLength(48) }); row.ColumnDefinitions.Add(new());
            ResourceChart? mini=null;
            if(section==PerformanceSubsystem.Overview) row.Children.Add(new SymbolIcon(Symbol.ShowResults) { Width=20,Height=20 });
            else { mini=new ResourceChart(section) { Height=32 }; row.Children.Add(mini); }
            var label=new StackPanel { Spacing=3,VerticalAlignment=VerticalAlignment.Center };
            label.Children.Add(new TextBlock { Text=section==PerformanceSubsystem.Overview?"Overview":SubsystemName(section),FontWeight=Microsoft.UI.Text.FontWeights.SemiBold });
            var reading=new TextBlock { Text=section==PerformanceSubsystem.Overview?"All resources":"Warming up…",FontSize=12,Opacity=.75,TextTrimming=TextTrimming.CharacterEllipsis };
            label.Children.Add(reading); Grid.SetColumn(label,1); row.Children.Add(label);
            var item=new ListViewItem { Content=row,Tag=section,HorizontalContentAlignment=HorizontalAlignment.Stretch };
            sidebar.Items.Add(item); if(section==performanceSubsystem) sidebar.SelectedItem=item;
            if(mini is not null) {
                var graph=mini;
                updates.Add(()=> { var point=performanceHistory.LastOrDefault(); reading.Text=MetricValue(point?.Metric(section),section);
                    UpdateResourceChart(graph,section);
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item,SubsystemName(section)+": "+reading.Text); });
            }
        }
        sidebar.SelectionChanged+=(_,_)=> { if(sidebar.SelectedItem is ListViewItem { Tag:PerformanceSubsystem section }) SelectPerformance(section); };
        layout.Children.Add(sidebar);
        var scroll=new ScrollViewer { HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        Grid.SetColumn(scroll,1); layout.Children.Add(scroll);
        var main=new StackPanel { Spacing=18,Margin=new Thickness(20,0,24,24),HorizontalAlignment=HorizontalAlignment.Stretch };
        scroll.Content=main;
        var compactPicker=new ComboBox { Name="PerformanceCompactPicker",ItemsSource=PerformanceSections.Select(s=>s==PerformanceSubsystem.Overview?"Overview":SubsystemName(s)).ToArray(),
            SelectedIndex=(int)performanceSubsystem,HorizontalAlignment=HorizontalAlignment.Stretch,Visibility=Visibility.Collapsed };
        compactPicker.SelectionChanged+=(_,_)=> { if(compactPicker.SelectedIndex>=0) SelectPerformance((PerformanceSubsystem)compactPicker.SelectedIndex); };
        main.Children.Add(compactPicker);
        var contentUpdates=new List<Action>();
        Grid? tiles=null;
        TextBlock? context=null;
        var arranged=-1;
        void BuildContent() {
        contentUpdates.Clear(); tiles=null; arranged=-1;
        // Keep navigation controls attached: rebuilding them replays ListView entrance transitions.
        while(main.Children.Count>1) main.Children.RemoveAt(main.Children.Count-1);
        var heading=new Grid { ColumnSpacing=12 }; heading.ColumnDefinitions.Add(new()); heading.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
        heading.Children.Add(new TextBlock { Text=performanceSubsystem==PerformanceSubsystem.Overview?"System overview":SubsystemName(performanceSubsystem),FontSize=26,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap });
        context=new TextBlock { Text="Last 60 samples",Opacity=.7,FontSize=12,VerticalAlignment=VerticalAlignment.Center };
        Grid.SetColumn(context,1); heading.Children.Add(context); main.Children.Add(heading);
        if(performanceSubsystem==PerformanceSubsystem.Overview) {
            tiles=new Grid { Name="PerformanceOverviewTiles",ColumnSpacing=12,RowSpacing=12,HorizontalAlignment=HorizontalAlignment.Stretch };
            main.Children.Add(tiles);
            foreach(var section in PerformanceSections.Skip(1)) {
                var content=new StackPanel { Spacing=6,Margin=new Thickness(16),HorizontalAlignment=HorizontalAlignment.Stretch };
                content.Children.Add(new TextBlock { Text=SubsystemName(section),FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,FontSize=14 });
                var reading=new TextBlock { FontSize=28,TextTrimming=TextTrimming.CharacterEllipsis };
                var description=new TextBlock { FontSize=12,Opacity=.75,TextWrapping=TextWrapping.Wrap,MinHeight=32 };
                var chart=new ResourceChart(section) { Height=64,Margin=new Thickness(0,8,0,0) };
                content.Children.Add(reading); content.Children.Add(description); content.Children.Add(chart);
                var tile=new Button { Content=content,Padding=new Thickness(0),HorizontalAlignment=HorizontalAlignment.Stretch,
                    HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch,Tag=section };
                tile.Click+=(_,_)=>SelectPerformance(section); tiles.Children.Add(tile);
                contentUpdates.Add(()=> { var point=performanceHistory.LastOrDefault(); var metric=point?.Metric(section);
                    reading.Text=MetricValue(metric,section); description.Text=OverviewDetail(point,section);
                    UpdateResourceChart(chart,section);
                    ToolTipService.SetToolTip(tile,metric?.State==ObservationState.Unavailable?metric.Reason:MetricDescription(section));
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tile,SubsystemName(section)+": "+reading.Text+". Open details."); });
            }
        } else {
            var section=performanceSubsystem;
            var caption=new TextBlock { Text=MetricDescription(section),Opacity=.75,TextWrapping=TextWrapping.Wrap };
            main.Children.Add(caption);
            var reading=new TextBlock { FontSize=32 }; main.Children.Add(reading);
            var graph=new ResourceChart(section,showTimeLabels:true) { Height=260 }; main.Children.Add(graph);
            var graphAxis=new Grid(); graphAxis.ColumnDefinitions.Add(new()); graphAxis.ColumnDefinitions.Add(new() { Width=GridLength.Auto });
            var scale=new TextBlock { FontSize=12,Opacity=.7 }; graphAxis.Children.Add(scale);
            main.Children.Add(graphAxis);
            var readings=new Grid { ColumnSpacing=28,RowSpacing=16,Name="PerformanceDetailReadings" };
            for(var column=0;column<3;column++) readings.ColumnDefinitions.Add(new());
            main.Children.Add(readings);
            var values=new List<TextBlock>();
            var labels=section switch {
                PerformanceSubsystem.Cpu=>new[]{"Utilization","Logical processors"},
                PerformanceSubsystem.Memory=>new[]{"In use","Available","Total physical memory"},
                PerformanceSubsystem.Disks=>new[]{"Active time","Read","Write"},
                PerformanceSubsystem.Network=>new[]{"Receive","Send","Total"},
                _=>new[]{"Busiest engine","Counter coverage"} };
            foreach(var (label,index) in labels.Select((label,index)=>(label,index))) {
                var group=new StackPanel { Spacing=4 }; group.Children.Add(new TextBlock { Text=label,FontSize=12,Opacity=.75,TextWrapping=TextWrapping.Wrap });
                var value=new TextBlock { FontSize=20,TextWrapping=TextWrapping.Wrap }; values.Add(value); group.Children.Add(value);
                Grid.SetColumn(group,index); readings.Children.Add(group);
            }
            var note=new TextBlock { FontSize=12,Opacity=.7,TextWrapping=TextWrapping.Wrap }; main.Children.Add(note);
            contentUpdates.Add(()=> { var point=performanceHistory.LastOrDefault(); var metric=point?.Metric(section);
                reading.Text=MetricValue(metric,section); UpdateResourceChart(graph,section);
                caption.Text=section==PerformanceSubsystem.Cpu&&point?.CpuName is { Length:>0 } name?name:MetricDescription(section);
                var fields=DetailReadings(point,section); for(var index=0;index<values.Count;index++) values[index].Text=fields[index];
                scale.Text=(presentation.PerformanceLogTimeAxis?"Logarithmic time":"Linear time")+
                    (section==PerformanceSubsystem.Network?" · Mbps · automatic scale":" · 0–100%");
                note.Text=metric?.State==ObservationState.Unavailable?metric.Reason??"Counter unavailable":section switch {
                    PerformanceSubsystem.Disks=>"Physical disk activity includes cached-write effects. Process disk attribution is measured separately.",
                    PerformanceSubsystem.Network=>"Totals include active non-loopback adapters. Virtual and VPN adapters may report overlapping traffic.",
                    PerformanceSubsystem.Gpu=>"Shows the busiest reported GPU engine, not a whole-device average. Availability depends on the graphics driver.",
                    _=>"Live system measurements; independent of AI analysis." };
            });
            readings.SizeChanged+=(_,e)=> { var stacked=e.NewSize.Width<390; readings.RowDefinitions.Clear();
                for(var row=0;row<(stacked?labels.Length:1);row++) readings.RowDefinitions.Add(new() { Height=GridLength.Auto });
                for(var index=0;index<readings.Children.Count;index++) { Grid.SetColumn((FrameworkElement)readings.Children[index],stacked?0:index); Grid.SetRow((FrameworkElement)readings.Children[index],stacked?index:0); Grid.SetColumnSpan((FrameworkElement)readings.Children[index],stacked?3:1); } };
        }
        }
        void Arrange(double width) {
            var compact=width<790; sidebar.Visibility=compact?Visibility.Collapsed:Visibility.Visible;
            layout.ColumnDefinitions[0].Width=new GridLength(compact?0:190); compactPicker.Visibility=compact?Visibility.Visible:Visibility.Collapsed;
            if(context is not null) context.Visibility=width<500?Visibility.Collapsed:Visibility.Visible;
            if(tiles is null) return;
            var available=width-(compact?44:234); var columns=available>=620?3:available>=380?2:1;
            if(arranged==columns) return; arranged=columns; tiles.ColumnDefinitions.Clear(); tiles.RowDefinitions.Clear();
            var gridColumns=columns==3?6:columns;
            for(var column=0;column<gridColumns;column++) tiles.ColumnDefinitions.Add(new());
            for(var row=0;row<(columns==3?2:(5+columns-1)/columns);row++) tiles.RowDefinitions.Add(new() { Height=GridLength.Auto });
            for(var index=0;index<5;index++) {
                var element=(FrameworkElement)tiles.Children[index];
                Grid.SetRow(element,columns==3?(index<3?0:1):index/columns);
                Grid.SetColumn(element,columns==3?(index<3?index*2:(index-3)*3):index%columns);
                Grid.SetColumnSpan(element,columns==3?(index<3?2:3):1);
            }
        }
        root.SizeChanged+=(_,e)=>Arrange(e.NewSize.Width);
        updatePerformanceView=()=> { foreach(var update in updates) update(); foreach(var update in contentUpdates) update(); };
        changePerformanceSection=()=> {
            sidebar.SelectedItem=sidebar.Items.Cast<ListViewItem>().Single(item=>Equals(item.Tag,performanceSubsystem));
            compactPicker.SelectedIndex=(int)performanceSubsystem;
            BuildContent(); scroll.ChangeView(null,0,null,true);
            Arrange(root.ActualWidth>0?root.ActualWidth:PageContent.ActualWidth); updatePerformanceView();
        };
        changePerformanceSection();
    }
    private static string MemorySize(long? bytes)=>bytes is { } value?$"{Math.Max(0,value)/1073741824d:F1} GB":"Unavailable";
    private static string ByteRate(Observation<double>? value,bool network=false)=>value is null?"Unavailable":
        value.State==ObservationState.Unavailable?"Unavailable":value.State==ObservationState.Pending?"Warming up…":ResourceRatePresentation.Text([value],network);
    private static string OverviewDetail(PerformanceSnapshot? point,PerformanceSubsystem section)=>section switch {
        PerformanceSubsystem.Memory=>point?.TotalMemory is { } total?MemorySize(total-point.AvailableMemory)+" / "+MemorySize(total):"Physical RAM in use",
        PerformanceSubsystem.Disks=>"Read + write · "+ByteRate(point?.DiskBytes),
        PerformanceSubsystem.Network=>"Send + receive",
        PerformanceSubsystem.Gpu=>"Busiest reported engine",
        _=>point is null?"Total utilization":point.LogicalProcessors+" logical processors" };
    private static string[] DetailReadings(PerformanceSnapshot? point,PerformanceSubsystem section)=>section switch {
        PerformanceSubsystem.Cpu=>[MetricValue(point?.Cpu,section),point?.LogicalProcessors.ToString()??"Unavailable"],
        PerformanceSubsystem.Memory=>[MemorySize(point?.TotalMemory-point?.AvailableMemory),MemorySize(point?.AvailableMemory),MemorySize(point?.TotalMemory)],
        PerformanceSubsystem.Disks=>[MetricValue(point?.DiskBusy,section),ByteRate(point?.DiskRead),ByteRate(point?.DiskWrite)],
        PerformanceSubsystem.Network=>[ByteRate(point?.NetworkReceived,true),ByteRate(point?.NetworkSent,true),MetricValue(point?.NetworkBytes,section)],
        _=>[MetricValue(point?.Gpu,section),point?.Gpu.State==ObservationState.Available?"Available":"Unavailable"] };
}

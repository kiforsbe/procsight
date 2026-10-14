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
        root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        root.RowDefinitions.Add(new(){Height=new GridLength(28)});
        var commands=new CommandBar { DefaultLabelPosition=CommandBarDefaultLabelPosition.Right,
            Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent),IsDynamicOverflowEnabled=true,
            Content=new TextBlock { Text=page,FontSize=16,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,
                Margin=new Thickness(16,0,24,0),VerticalAlignment=VerticalAlignment.Center } };
        analyzeButton=new AppBarButton { Label="Analyze",Icon=new SymbolIcon(Symbol.Find),IsEnabled=false };
        analyzeButton.Click+=async(_,_)=> { if(table?.SelectedItem is ProcessRow { IsGroup:false } row)
            await coordinator.RequestReanalysisAsync(row.Entity.Identity,lifetime.Token); };
        detailsButton=new AppBarButton { Label="Details",Icon=new SymbolIcon(Symbol.Document) };
        detailsButton.Click+=(_,_)=> { detailsOpen=!detailsOpen; UpdateDetailsLayout(); ShowDetail(table?.SelectedItem as ProcessRow); };
        var refresh=new AppBarButton { Label="Refresh",Icon=new SymbolIcon(Symbol.Refresh) };
        refresh.Click+=(_,_)=> { collector.RequestRefresh(); if(status is not null) status.Text="Refreshing local metadata…"; };
        commands.PrimaryCommands.Add(analyzeButton); commands.PrimaryCommands.Add(detailsButton); commands.PrimaryCommands.Add(refresh);
        var grouped=new AppBarToggleButton { Label="Group by application",Icon=new SymbolIcon(Symbol.AllApps),IsChecked=presentation.Grouped };
        grouped.Click+=(_,_)=> { presentation=presentation with { Grouped=grouped.IsChecked==true }; SavePresentation(); RefreshRows(); };
        commands.SecondaryCommands.Add(grouped);
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

        processBody=new Grid(); processBody.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        processBody.ColumnDefinitions.Add(new(){Width=GridLength.Auto}); Grid.SetRow(processBody,1); root.Children.Add(processBody);
        var surface=new Grid { MinWidth=page=="Services"?710:848 };
        surface.RowDefinitions.Add(new(){Height=new GridLength(52)}); surface.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var header=new Grid { BorderThickness=new Thickness(0,1,0,1),
            BorderBrush=(Brush)Microsoft.UI.Xaml.Application.Current.Resources["DividerStrokeColorDefaultBrush"] };
        var widths=page=="Services"?new[]{280,140,56,108,108}:new[]{280,140,56,80,96,96,100};
        for(var i=0;i<widths.Length;i++) header.ColumnDefinitions.Add(new(){Width=i==0?new GridLength(1,GridUnitType.Star):new GridLength(widths[i]),MinWidth=i==0?280:0});
        for(var i=0;i<widths.Length;i++)
        {
            var column=i;
            var label=page=="Services"?new[]{"Name","Assessment","PID","Status","Startup"}[i]:new[]{"Name","Assessment","PID","CPU","Memory","Disk","Network"}[i];
            var stack=new StackPanel { VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=i>=3?HorizontalAlignment.Right:HorizontalAlignment.Left };
            var total=new TextBlock { FontSize=16,TextAlignment=TextAlignment.Right };
            if(page=="Processes"&&i==3) { cpuSummary=total; stack.Children.Add(total); }
            if(page=="Processes"&&i==4) { memorySummary=total; stack.Children.Add(total); }
            if(page=="Processes"&&i>=5) stack.Children.Add(new TextBlock { Text="—",FontSize=16,TextAlignment=TextAlignment.Right });
            stack.Children.Add(new TextBlock { Text=label,FontSize=12 });
            var button=new Button { Content=stack,Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness=new Thickness(0),CornerRadius=new CornerRadius(0),HorizontalAlignment=HorizontalAlignment.Stretch,
                HorizontalContentAlignment=i>=3?HorizontalAlignment.Right:HorizontalAlignment.Left,VerticalContentAlignment=VerticalAlignment.Bottom,
                Padding=new Thickness(i==0?16:8,0,8,8) };
            button.Click+=(_,_)=> { if(column is 0 or 2 or 3 or 4) { sort=column switch {2=>ProcessSort.Pid,3=>ProcessSort.Cpu,4=>ProcessSort.Memory,_=>ProcessSort.Name}; RefreshRows(); } };
            var border=new Border { Child=button,BorderThickness=new Thickness(i==0?0:1,0,0,0),BorderBrush=header.BorderBrush };
            Grid.SetColumn(border,i); header.Children.Add(border);
        }
        surface.Children.Add(header);
        table=new ListView { ItemsSource=rows,SelectionMode=ListViewSelectionMode.Single,
            ItemContainerStyle=(Style)XamlReader.Load("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ListViewItem">
            <Setter Property="MinHeight" Value="28"/><Setter Property="Padding" Value="0"/><Setter Property="Margin" Value="0"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="CornerRadius" Value="0"/></Style>
            """),ItemTemplate=(DataTemplate)XamlReader.Load(page=="Services"?ServiceTemplate:ProcessTemplate) };
        table.SelectionChanged+=(_,_)=>
        {
            if(refreshingRows) return;
            if(table.SelectedItem is ProcessRow { IsGroup:true } group)
            { if(!expandedGroups.Add(group.Key)) expandedGroups.Remove(group.Key); RefreshRows(); }
            UpdateSelectionActions(); ShowDetail(table.SelectedItem as ProcessRow);
        };
        table.DoubleTapped+=(_,_)=> { if(table.SelectedItem is ProcessRow { IsGroup:false }) { detailsOpen=true; UpdateDetailsLayout(); ShowDetail(table.SelectedItem as ProcessRow); } };
        Grid.SetRow(table,1); surface.Children.Add(table);
        tableHost=new ScrollViewer { Content=surface,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollMode=ScrollMode.Auto,VerticalScrollMode=ScrollMode.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled };
        processBody.Children.Add(tableHost);
        var detailPanel=new StackPanel { Spacing=12,Padding=new Thickness(20) };
        var close=new Button { Content="Back to list",HorizontalAlignment=HorizontalAlignment.Left };
        close.Click+=(_,_)=> { detailsOpen=false; UpdateDetailsLayout(); }; detailPanel.Children.Add(close);
        detail=new TextBlock { Text="Select a process or service to inspect its identity and evidence.",TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true };
        detailPanel.Children.Add(detail); evidencePanel=new StackPanel { Spacing=4 }; detailPanel.Children.Add(evidencePanel);
        var correct=new Button { Content="Correct classification…" }; correct.Click+=async(_,_)=>await CorrectSelectedAsync();
        var preview=new Button { Content="Preview analysis payload…" }; preview.Click+=async(_,_)=>await PreviewAsync();
        detailPanel.Children.Add(correct); detailPanel.Children.Add(preview);
        detailsBorder=(Border)XamlReader.Load("""
            <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="360" Visibility="Collapsed"
                Background="{ThemeResource LayerFillColorDefaultBrush}" BorderThickness="1,0,0,0"
                BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}" />
            """);
        detailsBorder.Child=new ScrollViewer { Content=detailPanel };
        Grid.SetColumn(detailsBorder,1); processBody.Children.Add(detailsBorder);
        processBody.SizeChanged+=(_,_)=>UpdateDetailsLayout();
        status=new TextBlock { Text="Collecting local metadata…",FontSize=11,Opacity=.65,Margin=new Thickness(16,0,8,0),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis };
        Grid.SetRow(status,2); root.Children.Add(status); PageContent.Children.Add(root); UpdateDetailsLayout(); RefreshRows();
    }
    private void UpdateSelectionActions()
    {
        if(analyzeButton is not null) analyzeButton.IsEnabled=table?.SelectedItem is ProcessRow { IsGroup:false } && (policy.Provider is not null||policy.LocalModel is not null);
    }
    private void UpdateDetailsLayout()
    {
        if(processBody is null||detailsBorder is null||tableHost is null) return;
        detailsBorder.Visibility=detailsOpen?Visibility.Visible:Visibility.Collapsed;
        var narrow=processBody.ActualWidth<1000;
        Grid.SetColumn(detailsBorder,detailsOpen&&narrow?0:1);
        detailsBorder.Width=detailsOpen&&narrow?double.NaN:360;
        tableHost.Visibility=detailsOpen&&narrow?Visibility.Collapsed:Visibility.Visible;
    }
    private void RefreshResourceSummary()
    {
        if(cpuSummary is not null) cpuSummary.Text=lastSnapshot.SystemResources?.CpuPercent is { } cpu?$"{cpu:F0}%":"—";
        if(memorySummary is not null) memorySummary.Text=lastSnapshot.SystemResources?.MemoryPercent is { } memory?$"{memory:F0}%":"—";
        UpdateSelectionActions();
    }
    private const string ProcessTemplate="""
    <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
     <Grid Height="28" MinWidth="848"><Grid.ColumnDefinitions><ColumnDefinition Width="*" MinWidth="280"/><ColumnDefinition Width="140"/><ColumnDefinition Width="56"/><ColumnDefinition Width="80"/><ColumnDefinition Width="96"/><ColumnDefinition Width="96"/><ColumnDefinition Width="100"/></Grid.ColumnDefinitions>
      <Grid Margin="16,0,8,0" ToolTipService.ToolTip="{Binding Owner}"><Grid.ColumnDefinitions><ColumnDefinition Width="24"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
       <SymbolIcon Symbol="Document" Width="16" Height="16" Opacity="0.7"/><TextBlock Grid.Column="1" Text="{Binding Name}" FontSize="13" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
      </Grid>
      <Border Grid.Column="1" BorderThickness="1,0,0,0" BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"><TextBlock Text="{Binding Status}" FontSize="12" Margin="8,0" VerticalAlignment="Center" TextTrimming="CharacterEllipsis" ToolTipService.ToolTip="{Binding Status}"/></Border>
      <Border Grid.Column="2" BorderThickness="1,0,0,0" BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"><TextBlock Text="{Binding Pid}" FontSize="12" Margin="8,0" VerticalAlignment="Center" HorizontalAlignment="Right"/></Border>
      <Border Grid.Column="3" Background="{Binding CpuBrush}" BorderThickness="1,0,0,0" BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"><TextBlock Text="{Binding Cpu}" FontSize="12" Margin="8,0" VerticalAlignment="Center" HorizontalAlignment="Right"/></Border>
      <Border Grid.Column="4" Background="{Binding MemoryBrush}" BorderThickness="1,0,0,0" BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"><TextBlock Text="{Binding Memory}" FontSize="12" Margin="8,0" VerticalAlignment="Center" HorizontalAlignment="Right"/></Border>
      <Border Grid.Column="5" BorderThickness="1,0,0,0" BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"><TextBlock Text="—" FontSize="12" Margin="8,0" VerticalAlignment="Center" HorizontalAlignment="Right"/></Border>
      <Border Grid.Column="6" BorderThickness="1,0,0,0" BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"><TextBlock Text="—" FontSize="12" Margin="8,0" VerticalAlignment="Center" HorizontalAlignment="Right"/></Border>
     </Grid>
    </DataTemplate>
    """;
    private const string ServiceTemplate="""
    <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
     <Grid Height="28" MinWidth="710"><Grid.ColumnDefinitions><ColumnDefinition Width="*" MinWidth="280"/><ColumnDefinition Width="140"/><ColumnDefinition Width="56"/><ColumnDefinition Width="108"/><ColumnDefinition Width="108"/></Grid.ColumnDefinitions>
      <StackPanel Orientation="Horizontal" Margin="16,0,8,0" Spacing="8" VerticalAlignment="Center"><SymbolIcon Symbol="Repair" Width="16" Height="16"/><TextBlock Text="{Binding Name}" FontSize="13" TextTrimming="CharacterEllipsis"/></StackPanel>
      <TextBlock Grid.Column="1" Text="{Binding Status}" FontSize="12" Margin="8,0" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
      <TextBlock Grid.Column="2" Text="{Binding Pid}" FontSize="12" Margin="8,0" VerticalAlignment="Center" HorizontalAlignment="Right"/>
      <TextBlock Grid.Column="3" Text="{Binding ServiceState}" FontSize="12" Margin="8,0" VerticalAlignment="Center" HorizontalAlignment="Right"/>
      <TextBlock Grid.Column="4" Text="{Binding Startup}" FontSize="12" Margin="8,0" VerticalAlignment="Center" HorizontalAlignment="Right"/>
     </Grid>
    </DataTemplate>
    """;
}

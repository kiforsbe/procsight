using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ProcSight.App.ViewModels;
using ProcSight.Core.Models;
using System.Diagnostics;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private DispatcherQueueTimer? resourceShadeTimer;
    private (double Disk,double Network,double Gpu) resourceShadeMaximum;
    private readonly global::Windows.UI.ViewManagement.UISettings resourceShadeColors=new();
    private readonly global::Windows.UI.ViewManagement.AccessibilitySettings resourceAccessibility=new();
    private global::Windows.UI.Color? resourceShadeColor;
    private Brush?[] resourceShadePalette=new Brush?[256];
    private static double ShadeClock=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
    private void RefreshResourceShading(bool all=false,ProcessRow? only=null) {
        if(page!="Processes"||table is null) return;
        var options=presentation.ResourceShading??new(); var now=ShadeClock;
        var color=resourceShadeColors.GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Accent);
        if(resourceShadeColor!=color) { resourceShadeColor=color; resourceShadePalette=new Brush?[256]; }
        Brush Palette(byte alpha)=>resourceShadePalette[alpha]??=new SolidColorBrush(global::Windows.UI.Color.FromArgb(alpha,color.R,color.G,color.B));
        IEnumerable<ProcessRow> targets=only is not null?[only]:all?rows:RealizedColumnRows().Select(g=>g.DataContext).OfType<ProcessRow>();
        var highContrast=resourceAccessibility.HighContrast;
        foreach(var row in targets) row.RefreshHighlights(options,now,resourceShadeMaximum,Palette,highContrast);
    }
    private void ConfigureResourceShadingTimer() {
        resourceShadeTimer??=DispatcherQueue.CreateTimer();
        resourceShadeTimer.Interval=TimeSpan.FromMilliseconds(200);
        resourceShadeTimer.Tick-=ResourceShadeTick; resourceShadeTimer.Tick+=ResourceShadeTick;
        var options=presentation.ResourceShading??new();
        var fades=new[]{options.Disk,options.Network,options.Gpu}.Any(m=>m is ResourceShadingMode.RecentChanges or ResourceShadingMode.RecentSpikes);
        if(page=="Processes"&&fades&&options.IntensityPercent>0) resourceShadeTimer.Start(); else resourceShadeTimer.Stop();
    }
    private void ResourceShadeTick(DispatcherQueueTimer sender,object args) {
        if(!AppWindow.IsVisible||AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter { State:Microsoft.UI.Windowing.OverlappedPresenterState.Minimized }) return;
        RefreshResourceShading();
    }
    private FrameworkElement BuildResourceShadingSettings() {
        var options=presentation.ResourceShading??new();
        var feedback=SettingsFeedback();
        async Task Apply(ResourceShadingOptions next) {
            presentation=presentation with { ResourceShading=next };
            try { feedback.Text=SettingsResultText(await ApplySettingsAsync(policy)); }
            catch { feedback.Text="Resource shading preferences could not be saved."; }
        }
        var labels=new[]{"Off","Recent value changes","Current activity","Recent spikes","Top consumers"};
        var disk=new ComboBox { Header="Disk",ItemsSource=labels,SelectedIndex=(int)options.Disk,HorizontalAlignment=HorizontalAlignment.Stretch };
        var network=new ComboBox { Header="Network",ItemsSource=labels,SelectedIndex=(int)options.Network,HorizontalAlignment=HorizontalAlignment.Stretch };
        var gpu=new ComboBox { Header="GPU",ItemsSource=labels,SelectedIndex=(int)options.Gpu,HorizontalAlignment=HorizontalAlignment.Stretch };
        async Task Modes() { var current=presentation.ResourceShading??new(); await Apply(current with {
            Disk=(ResourceShadingMode)disk.SelectedIndex,Network=(ResourceShadingMode)network.SelectedIndex,Gpu=(ResourceShadingMode)gpu.SelectedIndex }); }
        disk.SelectionChanged+=async(_,_)=>await Modes(); network.SelectionChanged+=async(_,_)=>await Modes(); gpu.SelectionChanged+=async(_,_)=>await Modes();
        var durations=new[]{2,5,10,20}; var strengths=new[]{25,50,75};
        var duration=new ComboBox { Header="Fade duration",ItemsSource=durations.Select(v=>$"{v} seconds").ToArray(),SelectedIndex=Array.IndexOf(durations,options.FadeSeconds) };
        var strength=new ComboBox { Header="Intensity",ItemsSource=new[]{"Subtle","Balanced","Strong"},SelectedIndex=Array.IndexOf(strengths,options.IntensityPercent) };
        duration.SelectionChanged+=async(_,_)=> { if(duration.SelectedIndex>=0) await Apply((presentation.ResourceShading??new()) with { FadeSeconds=durations[duration.SelectedIndex] }); };
        strength.SelectionChanged+=async(_,_)=> { if(strength.SelectedIndex>=0) await Apply((presentation.ResourceShading??new()) with { IntensityPercent=strengths[strength.SelectedIndex] }); };
        return SettingsExpander("\uE790","Resource cell shading","Choose how Disk, Network and GPU cells are highlighted",disk,network,gpu,duration,strength,
            new TextBlock { Text="Recent changes brighten when the displayed value changes, then fade. Unchanged polling does not refresh the highlight. Current activity compares with that row’s decaying recent peak; recent spikes highlight increases above its smoothed baseline; top consumers compare visible rows. Missing values and high contrast use no shading.",TextWrapping=TextWrapping.Wrap },feedback);
    }
}

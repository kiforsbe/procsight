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
    private Func<byte,Brush>? resourceShadePalette;
    private static double ShadeClock=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
    private void RefreshResourceShading(bool all=false,ProcessRow? only=null) {
        if(page!="Processes"||table is null) return;
        var options=presentation.ResourceShading??new(); var now=ShadeClock;
        var color=resourceShadeColors.GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Accent);
        if(!options.UseSystemAccent) {
            var rgb=Convert.ToUInt32(options.CustomColorHex[1..],16);
            color=global::Windows.UI.Color.FromArgb(255,(byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb);
        }
        if(resourceShadeColor!=color||resourceShadePalette is null) {
            resourceShadeColor=color; var brushes=new Brush?[256];
            resourceShadePalette=alpha=>brushes[alpha]??=new SolidColorBrush(global::Windows.UI.Color.FromArgb(alpha,color.R,color.G,color.B));
        }
        IEnumerable<ProcessRow> targets=only is not null?[only]:all?rows:RealizedColumnRows().Select(g=>g.DataContext).OfType<ProcessRow>();
        var highContrast=resourceAccessibility.HighContrast;
        foreach(var row in targets) row.RefreshHighlights(options,now,resourceShadeMaximum,resourceShadePalette,highContrast);
    }
    private void ConfigureResourceShadingTimer() {
        resourceShadeTimer??=DispatcherQueue.CreateTimer();
        resourceShadeTimer.Interval=TimeSpan.FromMilliseconds(200);
        resourceShadeTimer.Tick-=ResourceShadeTick; resourceShadeTimer.Tick+=ResourceShadeTick;
        var options=presentation.ResourceShading??new();
        var fades=new[]{options.Disk,options.Network,options.Gpu}.Any(m=>m is ResourceShadingMode.RecentChanges or ResourceShadingMode.RecentSpikes);
        if(page=="Processes"&&fades&&options.MaximumOpacityPercent>options.MinimumOpacityPercent) resourceShadeTimer.Start(); else resourceShadeTimer.Stop();
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
        var editTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(400) }; var pending=false;
        async Task Flush() { editTimer.Stop(); if(!pending) return; pending=false; await Apply(presentation.ResourceShading??new()); }
        void Preview(ResourceShadingOptions next) { next.Validate(); presentation=presentation with { ResourceShading=next }; pending=true; editTimer.Stop(); editTimer.Start(); }
        editTimer.Tick+=async(_,_)=>await Flush();
        var labels=new[]{"Off","Recent value changes","Current activity","Recent spikes","Top consumers"};
        var disk=new ComboBox { Header="Disk",ItemsSource=labels,SelectedIndex=(int)options.Disk,HorizontalAlignment=HorizontalAlignment.Stretch };
        var network=new ComboBox { Header="Network",ItemsSource=labels,SelectedIndex=(int)options.Network,HorizontalAlignment=HorizontalAlignment.Stretch };
        var gpu=new ComboBox { Header="GPU",ItemsSource=labels,SelectedIndex=(int)options.Gpu,HorizontalAlignment=HorizontalAlignment.Stretch };
        async Task Modes() { var current=presentation.ResourceShading??new(); await Apply(current with {
            Disk=(ResourceShadingMode)disk.SelectedIndex,Network=(ResourceShadingMode)network.SelectedIndex,Gpu=(ResourceShadingMode)gpu.SelectedIndex }); }
        disk.SelectionChanged+=async(_,_)=>await Modes(); network.SelectionChanged+=async(_,_)=>await Modes(); gpu.SelectionChanged+=async(_,_)=>await Modes();
        var durations=new[]{2,5,10,20};
        var floors=new[]{0,3,5,7,10};
        var maximum=new NumberBox { Name="ResourceShadingRoof",Header="Maximum shading (opacity %)",Minimum=options.MinimumOpacityPercent,Maximum=100,
            Value=options.MaximumOpacityPercent,SmallChange=5,LargeChange=10,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
        var floor=new ComboBox { Header="Minimum shading",ItemsSource=new[]{"None (0%)","3%","5% (default)","7%","10%"},SelectedIndex=Array.IndexOf(floors,options.MinimumOpacityPercent) };
        floor.SelectionChanged+=async(_,_)=> { if(floor.SelectedIndex>=0) {
            var next=(presentation.ResourceShading??new()) with { MinimumOpacityPercent=floors[floor.SelectedIndex] };
            next=next with { MaximumOpacityPercent=Math.Max(next.MinimumOpacityPercent,next.MaximumOpacityPercent) };
            maximum.Minimum=next.MinimumOpacityPercent; maximum.Value=next.MaximumOpacityPercent; await Apply(next);
        } };
        maximum.ValueChanged+=(_,_)=> { if(double.IsFinite(maximum.Value)&&maximum.Value>=maximum.Minimum&&maximum.Value<=100)
            Preview((presentation.ResourceShading??new()) with { MaximumOpacityPercent=(int)Math.Round(maximum.Value) }); };
        maximum.LostFocus+=async(_,_)=>await Flush();
        var duration=new ComboBox { Header="Fade duration",ItemsSource=durations.Select(v=>$"{v} seconds").ToArray(),SelectedIndex=Array.IndexOf(durations,options.FadeSeconds) };
        duration.SelectionChanged+=async(_,_)=> { if(duration.SelectedIndex>=0) await Apply((presentation.ResourceShading??new()) with { FadeSeconds=durations[duration.SelectedIndex] }); };
        var colorSource=new ComboBox { Name="ResourceShadingColorSource",Header="Shading color",ItemsSource=new[]{"System highlight (Windows accent)","Custom color"},SelectedIndex=options.UseSystemAccent?0:1 };
        var rgb=Convert.ToUInt32(options.CustomColorHex[1..],16);
        var picker=new ColorPicker { Name="ResourceShadingColor",Color=global::Windows.UI.Color.FromArgb(255,(byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb),
            IsAlphaEnabled=false,IsAlphaSliderVisible=false,IsAlphaTextInputVisible=false,MaxWidth=340,HorizontalAlignment=HorizontalAlignment.Left,
            Visibility=options.UseSystemAccent?Visibility.Collapsed:Visibility.Visible };
        colorSource.SelectionChanged+=async(_,_)=> { picker.Visibility=colorSource.SelectedIndex==1?Visibility.Visible:Visibility.Collapsed;
            await Apply((presentation.ResourceShading??new()) with { UseSystemAccent=colorSource.SelectedIndex==0 }); };
        picker.ColorChanged+=(_,args)=>Preview((presentation.ResourceShading??new()) with { CustomColorHex=$"#{args.NewColor.R:X2}{args.NewColor.G:X2}{args.NewColor.B:X2}" });
        picker.LostFocus+=async(_,_)=>await Flush();
        var section=SettingsExpander("\uE790","Resource cell shading","Choose highlighting, opacity range and color",disk,network,gpu,duration,floor,maximum,colorSource,picker,
            new TextBlock { Text="Maximum shading is the brightest opacity, now 50% by default. The shared floor, roof and color apply to CPU, Memory, Disk, Network and GPU. Recent changes fade toward the floor; activity compares recent peaks; spikes compare the smoothed baseline; top consumers compare visible rows. Off modes, missing values and high contrast stay clear. Very strong or custom colors can reduce text contrast.",TextWrapping=TextWrapping.Wrap },feedback);
        section.Unloaded+=async(_,_)=>await Flush();
        return section;
    }
}

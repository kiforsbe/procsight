using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private readonly SystemPerformanceSampler performanceSampler=new();
    private readonly List<PerformanceSnapshot> performanceHistory=[];
    private PerformanceSubsystem performanceSubsystem;
    private Task? performanceLoop;
    private Action? updatePerformanceView;
    private PerformanceSnapshot? pendingPerformance;
    private int performanceUpdateQueued;
    private TextBlock? diskSummary,networkSummary,gpuSummary;
    private async Task RunPerformanceAsync() {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1));
        try {
            do {
                PerformanceSnapshot point;
                try { point=await performanceSampler.CaptureAsync(lifetime.Token); }
                catch(OperationCanceledException) { return; }
                catch { var missing=Observation<double>.Missing("System counters unavailable");
                    point=new(DateTimeOffset.UtcNow,missing,missing,missing,missing,missing,missing,null,null,Environment.ProcessorCount); }
                Volatile.Write(ref pendingPerformance,point);
                if(Interlocked.Exchange(ref performanceUpdateQueued,1)==0)
                    if(!DispatcherQueue.TryEnqueue(()=> {
                        Interlocked.Exchange(ref performanceUpdateQueued,0);
                        if(lifetime.IsCancellationRequested||Volatile.Read(ref pendingPerformance) is not { } latest) return;
                        performanceHistory.Add(latest); if(performanceHistory.Count>60) performanceHistory.RemoveAt(0);
                        if(page is "Processes" or "Services") RefreshResourceSummary();
                        if(page=="Performance") updatePerformanceView?.Invoke();
                    })) Interlocked.Exchange(ref performanceUpdateQueued,0);
            } while(await timer.WaitForNextTickAsync(lifetime.Token));
        } catch(OperationCanceledException) { }
    }
    private static string SubsystemName(PerformanceSubsystem subsystem)=>subsystem switch {
        PerformanceSubsystem.Cpu=>"CPU",PerformanceSubsystem.Gpu=>"GPU",_=>subsystem.ToString() };
    private static string MetricValue(Observation<double>? value,PerformanceSubsystem subsystem) {
        if(value?.State==ObservationState.Pending) return "Warming up…";
        if(value?.State!=ObservationState.Available||value.Value is not { } number) return "Unavailable";
        return subsystem==PerformanceSubsystem.Network?$"{number*8/1000000:F1} Mbps":$"{number:F1}%";
    }
    private static string MetricDescription(PerformanceSubsystem subsystem)=>subsystem switch {
        PerformanceSubsystem.Cpu=>"Total processor utilization",
        PerformanceSubsystem.Memory=>"Physical RAM in use",
        PerformanceSubsystem.Disks=>"Physical disks · aggregate busy time",
        PerformanceSubsystem.Network=>"Active adapters · send + receive",
        _=>"Busiest GPU engine" };
    private sealed class ResourceChart : Grid
    {
        private readonly Canvas canvas; private readonly Microsoft.UI.Xaml.Shapes.Path trace,grid;
        private Observation<double>[] samples=[]; private bool percentage;
        public ResourceChart(PerformanceSubsystem subsystem=PerformanceSubsystem.Cpu) {
            var brush="Performance"+(subsystem==PerformanceSubsystem.Overview?"Cpu":subsystem.ToString())+"Brush";
            canvas=(Canvas)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                  <Path Stroke="{ThemeResource DividerStrokeColorDefaultBrush}" StrokeThickness="1"/>
                  <Path Stroke="{ThemeResource CHART_BRUSH}" StrokeThickness="1.5"/>
                </Canvas>
                """.Replace("CHART_BRUSH",brush,StringComparison.Ordinal));
            grid=(Microsoft.UI.Xaml.Shapes.Path)canvas.Children[0]; trace=(Microsoft.UI.Xaml.Shapes.Path)canvas.Children[1];
            Children.Add(canvas); canvas.SizeChanged+=(_,_)=>Draw();
        }
        public void Update(Observation<double>[] values,bool percent) { samples=values; percentage=percent; Draw(); }
        private void Draw() {
            var width=canvas.ActualWidth; var height=canvas.ActualHeight; if(width<=0||height<=0) return;
            var gridGeometry=new PathGeometry();
            for(var line=0;line<=4;line++) {
                var figure=new PathFigure { StartPoint=new(0,height*line/4),IsClosed=false };
                figure.Segments.Add(new LineSegment { Point=new(width,height*line/4) }); gridGeometry.Figures.Add(figure);
            }
            grid.Data=gridGeometry;
            var maximum=percentage?100:Math.Max(1,samples.Where(s=>s.State==ObservationState.Available).Select(s=>s.Value??0).DefaultIfEmpty(0).Max());
            var geometry=new PathGeometry(); PathFigure? segment=null;
            for(var index=0;index<samples.Length;index++) {
                if(samples[index].State!=ObservationState.Available||samples[index].Value is not { } value||!double.IsFinite(value)) { segment=null; continue; }
                var point=new global::Windows.Foundation.Point(width*(60-samples.Length+index)/59,height*(1-Math.Clamp(value/maximum,0,1)));
                if(segment is null) { segment=new PathFigure { StartPoint=point,IsClosed=false }; geometry.Figures.Add(segment); }
                else segment.Segments.Add(new LineSegment { Point=point });
            }
            trace.Data=geometry;
        }
    }
}

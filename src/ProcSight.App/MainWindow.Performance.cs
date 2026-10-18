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
        private Observation<double>[] samples=[]; private bool percentage,logarithmic;
        private DateTimeOffset[] times=[];
        private readonly Canvas? axisLabels;
        private readonly List<TextBlock> tickLabels=[];
        private readonly PathGeometry traceGeometry=new();
        private readonly List<(PathFigure Figure,PolyLineSegment Line)> traceRuns=[];
        private (double Width,double Height,double Span,bool Logarithmic)? gridLayout;
        public ResourceChart(PerformanceSubsystem subsystem=PerformanceSubsystem.Cpu,bool showTimeLabels=false) {
            var brush="Performance"+(subsystem==PerformanceSubsystem.Overview?"Cpu":subsystem.ToString())+"Brush";
            canvas=(Canvas)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                  <Path Stroke="{ThemeResource DividerStrokeColorDefaultBrush}" StrokeThickness="1"/>
                  <Path Stroke="{ThemeResource CHART_BRUSH}" StrokeThickness="1.5"/>
                </Canvas>
                """.Replace("CHART_BRUSH",brush,StringComparison.Ordinal));
            grid=(Microsoft.UI.Xaml.Shapes.Path)canvas.Children[0]; trace=(Microsoft.UI.Xaml.Shapes.Path)canvas.Children[1];
            trace.Data=traceGeometry;
            Children.Add(canvas); canvas.SizeChanged+=(_,_)=>Draw();
            if(showTimeLabels) {
                RowDefinitions.Add(new()); RowDefinitions.Add(new() { Height=GridLength.Auto });
                axisLabels=new Canvas { Height=20 }; Grid.SetRow(axisLabels,1); Children.Add(axisLabels);
                for(var index=0;index<6;index++) { var label=new TextBlock { FontSize=10,Opacity=.7 }; tickLabels.Add(label); axisLabels.Children.Add(label); }
            }
        }
        public void Update(Observation<double>[] values,bool percent,bool logTime,DateTimeOffset[] timestamps) {
            // Bound reusable native geometry even if a future caller supplies a longer history.
            samples=values.Length<=60?values:values[^60..]; percentage=percent; logarithmic=logTime;
            times=timestamps.Length!=values.Length?[]:timestamps.Length<=60?timestamps:timestamps[^60..]; Draw();
        }
        private void Draw() {
            var width=canvas.ActualWidth; var height=canvas.ActualHeight; if(width<=0||height<=0) return;
            var span=times.Length>1?Math.Max(60,Math.Ceiling((times[^1]-times[0]).TotalSeconds/10)*10):60;
            var layout=(width,height,span,logarithmic);
            if(gridLayout!=layout) {
            gridLayout=layout;
            var gridGeometry=new PathGeometry();
            for(var line=0;line<=4;line++) {
                var figure=new PathFigure { StartPoint=new(0,height*line/4),IsClosed=false };
                figure.Segments.Add(new LineSegment { Point=new(width,height*line/4) }); gridGeometry.Figures.Add(figure);
            }
            if(axisLabels is not null) {
                var ticks=PerformanceTimeAxis.Ticks(span,logarithmic);
                for(var index=0;index<tickLabels.Count;index++) {
                    var label=tickLabels[index]; label.Visibility=index<ticks.Length?Visibility.Visible:Visibility.Collapsed;
                    if(index>=ticks.Length) continue;
                    var age=ticks[index]; var x=width*PerformanceTimeAxis.Position(age,span,logarithmic);
                    label.Text=age==0?"Now":age>=60?$"{age/60:0.#}m":$"{age:0.#}s";
                    label.Measure(new(double.PositiveInfinity,20));
                    Canvas.SetLeft(label,Math.Clamp(x-label.DesiredSize.Width/2,0,Math.Max(0,width-label.DesiredSize.Width)));
                    var figure=new PathFigure { StartPoint=new(x,0),IsClosed=false };
                    figure.Segments.Add(new LineSegment { Point=new(x,height) }); gridGeometry.Figures.Add(figure);
                }
            }
            grid.Data=gridGeometry;
            }
            double maximum=percentage?100:1;
            if(!percentage) foreach(var sample in samples)
                if(sample.State==ObservationState.Available&&sample.Value is { } value&&double.IsFinite(value)) maximum=Math.Max(maximum,value);
            var runIndex=-1; var pointCount=0; PathFigure? segment=null; PolyLineSegment? lineSegment=null;
            void FinishRun() {
                if(lineSegment is not null) while(lineSegment.Points.Count>pointCount) lineSegment.Points.RemoveAt(lineSegment.Points.Count-1);
                segment=null; lineSegment=null;
            }
            for(var index=0;index<samples.Length;index++) {
                if(samples[index].State!=ObservationState.Available||samples[index].Value is not { } value||!double.IsFinite(value)) { FinishRun(); continue; }
                var age=times.Length==samples.Length?(times[^1]-times[index]).TotalSeconds:samples.Length-1-index;
                var x=PerformanceTimeAxis.Position(age,span,logarithmic);
                if(!double.IsFinite(x)) { FinishRun(); continue; }
                var point=new global::Windows.Foundation.Point(width*x,height*(1-Math.Clamp(value/maximum,0,1)));
                if(segment is null) {
                    runIndex++; pointCount=0;
                    if(traceRuns.Count<=runIndex) {
                        var figure=new PathFigure { IsClosed=false }; var polyline=new PolyLineSegment(); figure.Segments.Add(polyline);
                        traceRuns.Add((figure,polyline));
                    }
                    (segment,lineSegment)=traceRuns[runIndex];
                    if(!segment.StartPoint.Equals(point)) segment.StartPoint=point;
                    if(traceGeometry.Figures.Count<=runIndex) traceGeometry.Figures.Add(segment);
                } else {
                    if(lineSegment!.Points.Count<=pointCount) lineSegment.Points.Add(point);
                    else if(!lineSegment.Points[pointCount].Equals(point)) lineSegment.Points[pointCount]=point;
                    pointCount++;
                }
            }
            FinishRun();
            while(traceGeometry.Figures.Count>runIndex+1) traceGeometry.Figures.RemoveAt(traceGeometry.Figures.Count-1);
        }
    }
    private void UpdateResourceChart(ResourceChart chart,PerformanceSubsystem section)=>chart.Update(
        performanceHistory.Select(p=>p.Metric(section)).ToArray(),section!=PerformanceSubsystem.Network,
        presentation.PerformanceLogTimeAxis,performanceHistory.Select(p=>p.At).ToArray());
}

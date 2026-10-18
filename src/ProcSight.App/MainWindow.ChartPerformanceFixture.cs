#if DEBUG
using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private async Task CheckChartPerformanceAsync(string output,bool requireReuse) {
        PageContent.Children.Clear();
        var chart=new ResourceChart(showTimeLabels:true) { Width=580,Height=240 };
        PageContent.Children.Add(chart); await Task.Delay(100); WindowRoot.UpdateLayout();
        var values=Enumerable.Range(0,60).Select(i=>Observation<double>.Available(i%40)).ToArray();
        var times=Enumerable.Range(0,60).Select(i=>DateTimeOffset.UnixEpoch.AddSeconds(i)).ToArray();
        chart.Update(values,true,false,times); WindowRoot.UpdateLayout();
        var paths=chart.Children.OfType<Canvas>().First().Children.OfType<Microsoft.UI.Xaml.Shapes.Path>().ToArray();
        var originalGrid=paths[0].Data; var originalTrace=paths[1].Data;
        var elapsed=new List<double>(); var bytes=GC.GetAllocatedBytesForCurrentThread();
        for(var i=0;i<200;i++) {
            values[59]=Observation<double>.Available(i%100);
            var at=Stopwatch.GetTimestamp(); chart.Update(values,true,false,times); WindowRoot.UpdateLayout();
            elapsed.Add(Stopwatch.GetElapsedTime(at).TotalMilliseconds);
        }
        var allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
        File.WriteAllText(output+".chart.json",JsonSerializer.Serialize(new { updates=200,meanMs=elapsed.Average(),
            p95Ms=elapsed.Order().ElementAt(190),allocatedBytesPerUpdate=allocated/200d,
            gridReused=ReferenceEquals(originalGrid,paths[0].Data),traceReused=ReferenceEquals(originalTrace,paths[1].Data) },new JsonSerializerOptions { WriteIndented=true }));
        if(requireReuse) {
            if(!ReferenceEquals(originalGrid,paths[0].Data)||!ReferenceEquals(originalTrace,paths[1].Data))
                throw new InvalidOperationException("Chart ticks replace native geometry instead of reusing it.");
            values[20]=Observation<double>.Missing("Synthetic gap"); chart.Update(values,true,false,times);
            if(paths[1].Data is not PathGeometry { Figures.Count:2 }) throw new InvalidOperationException("Missing readings did not split the trace.");
            values[20]=Observation<double>.Available(20); chart.Update(values,true,false,times);
            if(paths[1].Data is not PathGeometry { Figures.Count:1 }) throw new InvalidOperationException("Restored readings retained a gap.");
            chart.Update(values,true,true,times);
            if(ReferenceEquals(originalGrid,paths[0].Data)) throw new InvalidOperationException("Time-axis changes did not invalidate the grid.");
            var logGrid=paths[0].Data; chart.Width=520; WindowRoot.UpdateLayout();
            if(ReferenceEquals(logGrid,paths[0].Data)) throw new InvalidOperationException("Resize did not invalidate chart geometry.");
            var oversized=Enumerable.Range(0,200).Select(i=>Observation<double>.Available(i%100)).ToArray();
            chart.Update(oversized,true,false,Enumerable.Range(0,200).Select(i=>DateTimeOffset.UnixEpoch.AddSeconds(i)).ToArray());
            if(paths[1].Data is not PathGeometry bounded||bounded.Figures.Count!=1||
                ((PolyLineSegment)bounded.Figures[0].Segments[0]).Points.Count!=59)
                throw new InvalidOperationException("Reusable chart geometry exceeded the 60-sample bound.");
            chart.Update([],false,false,[]);
            if(bounded.Figures.Count!=0) throw new InvalidOperationException("Empty history retained a trace.");
            values[10]=Observation<double>.Available(double.NaN); values[20]=Observation<double>.Available(double.PositiveInfinity);
            chart.Update(values,false,false,times);
            if(bounded.Figures.Count!=3) throw new InvalidOperationException("Invalid values did not split the autoscaled trace.");
            values[10]=Observation<double>.Available(10); values[20]=Observation<double>.Available(20);
            chart.Update(values,true,false,times);
        }
        File.WriteAllText(output+".checks","Synthetic chart measurement completed.");
    }
}
#endif

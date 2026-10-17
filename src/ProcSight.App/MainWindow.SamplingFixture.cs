#if DEBUG
using System.Diagnostics;
using System.Text.Json;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    // Explicit live collector profiling; only aggregate numbers leave memory. Never capture live rows.
    private async Task CheckLocalSamplingAsync(string output) {
        var synthetic=lastSnapshot; var readings=new List<object>();
        using var process=Process.GetCurrentProcess();
        void Record(string stage,double ms,long allocated) {
            process.Refresh(); readings.Add(new { stage,ms,allocated,managedBytes=GC.GetTotalMemory(false),
                heapBytes=GC.GetGCMemoryInfo().HeapSizeBytes,privateBytes=process.PrivateMemorySize64,
                workingSet=process.WorkingSet64,handles=process.HandleCount,processes=lastSnapshot.Processes.Count,services=lastSnapshot.Services.Count });
            File.WriteAllText(output+".sampling.json",JsonSerializer.Serialize(readings,new JsonSerializerOptions { WriteIndented=true }));
        }
        try {
            Record("native-start",0,0);
            for(var i=0;i<25;i++) {
                var allocated=GC.GetTotalAllocatedBytes(); var at=Stopwatch.GetTimestamp();
                lastSnapshot=await collector.CaptureAsync(CommandLineMode.Off,default);
                Record("capture",Stopwatch.GetElapsedTime(at).TotalMilliseconds,GC.GetTotalAllocatedBytes()-allocated);
                allocated=GC.GetAllocatedBytesForCurrentThread(); at=Stopwatch.GetTimestamp();
                entities=new IdentityResolver().Resolve(lastSnapshot,[]); RefreshRows(); WindowRoot.UpdateLayout();
                Record("resolve-and-native-refresh",Stopwatch.GetElapsedTime(at).TotalMilliseconds,GC.GetAllocatedBytesForCurrentThread()-allocated);
                await Task.Delay(1000);
            }
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Task.Delay(100);
            Record("retained-live-rows",0,0);
        } finally {
            var at=Stopwatch.GetTimestamp(); await collector.DisposeAsync(); Record("collector-dispose",Stopwatch.GetElapsedTime(at).TotalMilliseconds,0);
            lastSnapshot=synthetic; entities=new IdentityResolver().Resolve(synthetic,[]); RefreshRows();
            File.WriteAllText(output+".checks","Live local collection/native UI measured; only aggregate counts/timing/memory written; synthetic rows restored before capture. No AI or saved accounts.");
        }
    }
}
#endif

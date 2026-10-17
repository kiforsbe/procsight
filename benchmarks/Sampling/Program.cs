using System.Diagnostics;
using System.Text.Json;
using ProcSight.Core.Models;
using ProcSight.Windows.Collection;

// Opt-in local measurements only: never write inventory, commands, names or credentials.
var mode=args.FirstOrDefault()??"system";
var iterations=args.Length>1?int.Parse(args[1]):15;
if(iterations is <1 or >120) throw new ArgumentOutOfRangeException(nameof(iterations));
using var process=Process.GetCurrentProcess();
var results=new List<object>();
void Record(string stage,double ms,long allocated) {
    process.Refresh();
    results.Add(new { stage,ms,allocated,managedBytes=GC.GetTotalMemory(false),
        heapBytes=GC.GetGCMemoryInfo().HeapSizeBytes,workingSet=process.WorkingSet64,
        privateBytes=process.PrivateMemorySize64,handles=process.HandleCount,threads=process.Threads.Count });
}
async Task Measure(string stage,Func<Task> action) {
    var bytes=GC.GetTotalAllocatedBytes(); var at=Stopwatch.GetTimestamp();
    await action(); Record(stage,Stopwatch.GetElapsedTime(at).TotalMilliseconds,GC.GetTotalAllocatedBytes()-bytes);
}
Record("start",0,0);
if(mode=="system") {
    var sampler=new SystemPerformanceSampler();
    for(var i=0;i<iterations;i++) { await Measure("system-sample",async()=> { _=await sampler.CaptureAsync(default); }); await Task.Delay(1000); }
    await Measure("system-dispose",async()=>await sampler.DisposeAsync());
} else if(mode=="inventory") {
    var collector=new WindowsSnapshotSource();
    for(var i=0;i<iterations;i++) { await Measure("inventory-sample",async()=> { _=await collector.CaptureAsync(CommandLineMode.Off,default); }); await Task.Delay(1000); }
    await Measure("inventory-stop",()=> { collector.Stop(); return Task.CompletedTask; });
    await Measure("inventory-dispose",async()=>await collector.DisposeAsync());
} else if(mode=="gpu") {
    var counter=new PerformanceCounterReader(@"\GPU Engine(*)\Utilization Percentage");
    for(var i=0;i<iterations;i++) { await Measure("gpu-sample",()=> { _=counter.ReadValues(); return Task.CompletedTask; }); await Task.Delay(1000); }
    await Measure("gpu-dispose",()=> { counter.Dispose(); return Task.CompletedTask; });
} else if(mode=="metadata") {
    for(var i=0;i<iterations;i++) { await Measure("command-metadata",()=> { _=ProcessMetadataReader.ReadCommandLineMetadata(); return Task.CompletedTask; }); await Task.Delay(1000); }
} else throw new ArgumentException("Use system, inventory, metadata or gpu.");
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Record("retained-after-gc",0,0);
Console.WriteLine(JsonSerializer.Serialize(results,new JsonSerializerOptions { WriteIndented=true }));

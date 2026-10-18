using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using System.Diagnostics;
using System.Net.NetworkInformation;
namespace ProcSight.Windows.Collection;
public sealed class SystemPerformanceSampler : IAsyncDisposable
{
    private readonly TrayMetricSampler disk=new(),gpu=new();
    private readonly SemaphoreSlim gate=new(1);
    private PerformanceCounterReader? diskBytes,diskRead,diskWrite;
    private Dictionary<string,(long Sent,long Received)>? priorNetwork; private long networkAt;
    private string? cpuName; private bool readCpuName;
    private (long Idle,long Kernel,long User)? previous;
    private bool disposed;
    public async Task<PerformanceSnapshot> CaptureAsync(CancellationToken ct) {
        await gate.WaitAsync(ct);
        try {
            ObjectDisposedException.ThrowIf(disposed,this);
            return await Task.Run(async()=> {
                var current=WindowsNativeMethods.SystemTimes(); double? cpu=null;
                if(current is { } now&&previous is { } old) {
                    var total=now.Kernel+now.User-old.Kernel-old.User;
                    if(total>0) cpu=Math.Clamp((1-(now.Idle-old.Idle)/(double)total)*100,0,100);
                }
                previous=current;
                var memory=WindowsNativeMethods.SystemMemory();
                var system=new SystemResourceSample(cpu,memory.Total,memory.Available);
                var diskSample=await disk.SampleAsync(TrayMetric.Disk,system,ct);
                var gpuSample=await gpu.SampleAsync(TrayMetric.Gpu,system,ct);
                diskBytes??=new(@"\PhysicalDisk(_Total)\Disk Bytes/sec");
                diskRead??=new(@"\PhysicalDisk(_Total)\Disk Read Bytes/sec");
                diskWrite??=new(@"\PhysicalDisk(_Total)\Disk Write Bytes/sec");
                var throughput=diskBytes.Read(percentage:false);
                Observation<double> Counter(PerformanceCounterReader counter) {
                    var result=counter.Read(percentage:false);
                    return result.Value is { } value?Observation<double>.Available(value):result.Warming?Observation<double>.Pending():Observation<double>.Missing("Counter unavailable");
                }
                if(!readCpuName) {
                    readCpuName=true;
                    try { using var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                        cpuName=(key?.GetValue("ProcessorNameString") as string)?.Trim(); } catch { }
                }
                var net=ReadNetwork();
                Observation<double> Percent(double? value)=>value is { } v?Observation<double>.Available(v):current is null?
                    Observation<double>.Missing("Processor counters unavailable"):Observation<double>.Pending();
                Observation<double> Observe(TraySample sample)=>sample.State switch {
                    TraySampleState.Available when sample.Value is { } v=>Observation<double>.Available(v),
                    TraySampleState.WarmingUp=>Observation<double>.Pending(),_=>Observation<double>.Missing("Counter unavailable") };
                return new PerformanceSnapshot(DateTimeOffset.UtcNow,Percent(cpu),
                    system.MemoryPercent is { } percent?Observation<double>.Available(percent):Observation<double>.Missing("Memory information unavailable"),
                    Observe(diskSample),throughput.Value is { } bytes?Observation<double>.Available(bytes):throughput.Warming?Observation<double>.Pending():Observation<double>.Missing("Disk throughput counter unavailable"),
                    net.Total,Observe(gpuSample),memory.Total,memory.Available,Environment.ProcessorCount,
                    Counter(diskRead),Counter(diskWrite),net.Sent,net.Received,cpuName);
            },ct);
        } finally { gate.Release(); }
    }
    public async ValueTask DisposeAsync() {
        await gate.WaitAsync();
        try { disposed=true; disk.Dispose(); gpu.Dispose(); diskBytes?.Dispose(); diskRead?.Dispose(); diskWrite?.Dispose(); }
        finally { gate.Release(); }
    }
    private (Observation<double> Total,Observation<double> Sent,Observation<double> Received) ReadNetwork() {
        try {
            var now=Stopwatch.GetTimestamp();
            var current=NetworkInterface.GetAllNetworkInterfaces().Take(128)
                .Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback)
                .ToDictionary(n=>n.Id,n=> { var stats=n.GetIPStatistics(); return (Sent:stats.BytesSent,Received:stats.BytesReceived); });
            var seconds=networkAt==0?0:Stopwatch.GetElapsedTime(networkAt,now).TotalSeconds;
            var result=NetworkRates.Calculate(priorNetwork,current,seconds);
            priorNetwork=current; networkAt=now; return result;
        } catch { priorNetwork=null; networkAt=0; var missing=Observation<double>.Missing("Network adapter counters unavailable"); return(missing,missing,missing); }
    }
}

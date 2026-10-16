using System.Net.NetworkInformation;
using System.Diagnostics;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public sealed class TrayMetricSampler : IDisposable
{
    private readonly object gate=new(); private PerformanceCounterReader? counter;
    private TrayMetric? selected; private Dictionary<string,long>? network; private long last;
    private TraySample? previous; private bool disposed;
    public Task<TraySample> SampleAsync(TrayMetric metric,SystemResourceSample? system,CancellationToken ct)=>Task.Run(()=> {
        lock(gate) {
            ct.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(disposed,this);
            if(selected!=metric) { counter?.Dispose(); counter=null; network=null; previous=null; last=0; selected=metric; }
            if(metric==TrayMetric.Cpu) return TraySample.FromPercent(metric,system?.CpuPercent);
            if(metric==TrayMetric.Memory) return TraySample.FromPercent(metric,system?.MemoryPercent);
            var now=Stopwatch.GetTimestamp(); var seconds=last==0?0:Stopwatch.GetElapsedTime(last,now).TotalSeconds;
            if(previous is not null&&seconds<1) return previous;
            last=now;
            try {
                if(metric==TrayMetric.Network) {
                    var current=NetworkInterface.GetAllNetworkInterfaces().Take(128)
                        .Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback)
                        .ToDictionary(n=>n.Id,n=> { var s=n.GetIPStatistics(); return checked(s.BytesReceived+s.BytesSent); });
                    var value=network is null?null:NetworkDelta(network,current,seconds); var warming=network is null; network=current;
                    return previous=new(metric,value,"B/s",warming?TraySampleState.WarmingUp:value is null?TraySampleState.Unavailable:TraySampleState.Available);
                }
                counter??=new(metric==TrayMetric.Disk?@"\PhysicalDisk(_Total)\% Idle Time":@"\GPU Engine(*)\Utilization Percentage");
                var result=counter.Read(metric==TrayMetric.Disk);
                return previous=result.Warming?new(metric,null,"%",TraySampleState.WarmingUp):TraySample.FromPercent(metric,result.Value);
            } catch { return previous=new(metric,null,metric==TrayMetric.Network?"B/s":"%",TraySampleState.Unavailable); }
        }
    },ct);
    public static double? NetworkDelta(IReadOnlyDictionary<string,long> before,IReadOnlyDictionary<string,long> after,double seconds)
    {
        if(seconds<=0||!double.IsFinite(seconds)) return null;
        double sum=0; var matched=false;
        foreach(var (id,value) in after) if(before.TryGetValue(id,out var old)&&value>=old) { sum+=value-old; matched=true; }
        return matched&&double.IsFinite(sum)?sum/seconds:null;
    }
    public void Dispose() { lock(gate) { disposed=true; counter?.Dispose(); counter=null; } }
}

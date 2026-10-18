using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public sealed record ProcessIoRates(Observation<double> Disk,Observation<double> Network);
/// <summary>Only byte totals and confirmed lifetimes; no file or connection metadata.</summary>
public sealed class ProcessIoAccumulator
{
    private sealed class Bucket(ProcessKey key,DateTimeOffset at) {
        public ProcessKey Key=key; public DateTimeOffset Since=at,Last=at; public double Disk,Network;
    }
    private readonly Dictionary<int,Bucket> buckets=[]; private readonly object gate=new();
    public int Count { get { lock(gate) return buckets.Count; } }
    public void Add(int pid,DateTimeOffset at,long disk,long network) {
        if(disk<0||network<0) return;
        lock(gate) if(buckets.TryGetValue(pid,out var bucket)&&at>=bucket.Since) {
            bucket.Disk+=disk; bucket.Network+=network;
        }
    }
    public void Invalidate(int pid,DateTimeOffset at) { lock(gate) if(buckets.TryGetValue(pid,out var b)&&at>=b.Since) buckets.Remove(pid); }
    public void Reset() { lock(gate) buckets.Clear(); }
    public IReadOnlyDictionary<ProcessKey,ProcessIoRates> Sample(IReadOnlyList<ProcessKey> live,DateTimeOffset now) {
        lock(gate) {
            var result=new Dictionary<ProcessKey,ProcessIoRates>(); var pids=new HashSet<int>();
            foreach(var key in live.Take(32768)) {
                pids.Add(key.Pid);
                if(!buckets.TryGetValue(key.Pid,out var b)||b.Key!=key||now<=b.Last) {
                    buckets[key.Pid]=new(key,now); result[key]=new(Observation<double>.Pending(),Observation<double>.Pending()); continue;
                }
                var seconds=(now-b.Last).TotalSeconds;
                result[key]=new(Observation<double>.Available(b.Disk/seconds),Observation<double>.Available(b.Network/seconds));
                b.Last=now; b.Disk=b.Network=0;
            }
            foreach(var pid in buckets.Keys.Where(pid=>!pids.Contains(pid)).ToArray()) buckets.Remove(pid);
            return result;
        }
    }
}

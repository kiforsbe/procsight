using System.Globalization;
using System.Text.RegularExpressions;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public static partial class ProcessGpuProjection
{
    [GeneratedRegex(@"^pid_(\d+)_(luid_0x[0-9a-f]+_0x[0-9a-f]+_phys_\d+_eng_\d+)_engtype_.+$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex InstancePattern();
    public static IReadOnlyDictionary<ProcessKey,Observation<double>> Build(IReadOnlyDictionary<string,double?>? counters,
        IReadOnlyList<ProcessKey> live,IReadOnlySet<ProcessKey> previous,bool warming)
    {
        var engines=new Dictionary<int,Dictionary<string,double>>(); var invalid=new HashSet<int>();
        foreach(var (name,value) in counters??new Dictionary<string,double?>()) {
            var match=InstancePattern().Match(name);
            if(!match.Success||!int.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out var pid)) continue;
            if(value is null||!double.IsFinite(value.Value)||value<0) { invalid.Add(pid); continue; }
            if(!engines.TryGetValue(pid,out var values)) engines[pid]=values=new(StringComparer.OrdinalIgnoreCase);
            var key=match.Groups[2].Value; values[key]=Math.Clamp(values.GetValueOrDefault(key)+value.Value,0,100);
        }
        return live.Take(32768).ToDictionary(key=>key,key=> {
            if(warming) return Observation<double>.Pending();
            if(counters is null||engines.Count+invalid.Count==0) return Observation<double>.Missing("GPU engine counters unavailable");
            if(!previous.Contains(key)) return Observation<double>.Pending();
            if(invalid.Contains(key.Pid)) return Observation<double>.Missing("GPU engine sample unavailable");
            return Observation<double>.Available(engines.TryGetValue(key.Pid,out var values)?values.Values.Max():0);
        });
    }
}
public sealed class ProcessGpuSampler : IDisposable
{
    private PerformanceCounterReader? reader;
    private HashSet<ProcessKey> previous=[];
    public IReadOnlyDictionary<ProcessKey,Observation<double>> Sample(IReadOnlyList<ProcessKey> live) {
        try {
            reader??=new(@"\GPU Engine(*)\Utilization Percentage");
            var result=reader.ReadValues();
            var projected=ProcessGpuProjection.Build(result.Values,live,previous,result.Warming);
            previous=live.Take(32768).ToHashSet(); return projected;
        } catch {
            previous.Clear();
            return live.Take(32768).ToDictionary(key=>key,_=>Observation<double>.Missing("GPU engine counters unavailable"));
        }
    }
    public void Retain(IReadOnlySet<ProcessKey> accepted)=>previous.IntersectWith(accepted);
    public void Dispose()=>reader?.Dispose();
}

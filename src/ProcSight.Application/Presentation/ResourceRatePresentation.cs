using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
public static class ResourceRatePresentation
{
    public static double? GpuMaximum(IEnumerable<Observation<double>> source)=>source
        .Where(v=>v.State==ObservationState.Available&&v.Value is >=0&&double.IsFinite(v.Value.Value))
        .Select(v=>(double?)Math.Clamp(v.Value!.Value,0,100)).Max();
    public static string GpuText(IEnumerable<Observation<double>> source) {
        double? maximum=null; var pending=false; var incomplete=false;
        foreach(var value in source) {
            pending|=value.State==ObservationState.Pending; incomplete|=value.State!=ObservationState.Available;
            if(value.State==ObservationState.Available&&value.Value is >=0&&double.IsFinite(value.Value.Value))
                maximum=Math.Max(maximum??0,Math.Clamp(value.Value.Value,0,100));
        }
        if(maximum is null) return pending?"…":"—";
        return (incomplete?"≥":"")+$"{maximum:F1}%";
    }
    public static string Text(IEnumerable<Observation<double>> source,bool network=false) {
        var count=0; var available=0; var pending=false; double total=0;
        foreach(var value in source) { count++; pending|=value.State==ObservationState.Pending;
            if(value.State==ObservationState.Available&&value.Value is >=0&&double.IsFinite(value.Value.Value)) { available++; total+=value.Value.Value; } }
        if(available==0) return pending?"…":"—";
        var prefix=available<count?"≥":"";
        return network?$"{prefix}{total*8/1000000:F1} Mbps":$"{prefix}{total/1000000:F1} MB/s";
    }
    public static string Details(IEnumerable<Observation<double>> source) {
        List<string>? reasons=null;
        foreach(var value in source) {
            if(value.State==ObservationState.Pending) return "Warming up; waiting for a complete sampling interval.";
            if(value.State!=ObservationState.Unavailable) continue;
            reasons??=[]; var reason=value.Reason??"Unavailable";
            if(reasons.Count<3&&!reasons.Contains(reason)) reasons.Add(reason);
        }
        return reasons is {Count:>0}?string.Join("; ",reasons):"Combined read/write or send/receive throughput. Buffered event delivery can delay samples.";
    }
}

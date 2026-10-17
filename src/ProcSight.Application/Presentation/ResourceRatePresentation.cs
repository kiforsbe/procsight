using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
public static class ResourceRatePresentation
{
    public static double? GpuMaximum(IEnumerable<Observation<double>> source)=>source
        .Where(v=>v.State==ObservationState.Available&&v.Value is >=0&&double.IsFinite(v.Value.Value))
        .Select(v=>(double?)Math.Clamp(v.Value!.Value,0,100)).Max();
    public static string GpuText(IEnumerable<Observation<double>> source) {
        var values=source.ToArray(); var maximum=GpuMaximum(values);
        if(maximum is null) return values.Any(v=>v.State==ObservationState.Pending)?"…":"—";
        return (values.Any(v=>v.State!=ObservationState.Available)?"≥":"")+$"{maximum:F1}%";
    }
    public static string Text(IEnumerable<Observation<double>> source,bool network=false) {
        var values=source.ToArray(); var available=values.Where(v=>v.State==ObservationState.Available&&v.Value is >=0&&double.IsFinite(v.Value.Value)).ToArray();
        if(available.Length==0) return values.Any(v=>v.State==ObservationState.Pending)?"…":"—";
        var total=available.Sum(v=>v.Value!.Value); var prefix=available.Length<values.Length?"≥":"";
        return network?$"{prefix}{total*8/1000000:F1} Mbps":$"{prefix}{total/1000000:F1} MB/s";
    }
    public static string Details(IEnumerable<Observation<double>> source) {
        var values=source.ToArray();
        if(values.Any(v=>v.State==ObservationState.Pending)) return "Warming up; waiting for a complete sampling interval.";
        var reasons=values.Where(v=>v.State==ObservationState.Unavailable).Select(v=>v.Reason??"Unavailable").Distinct().Take(3).ToArray();
        return reasons.Length>0?string.Join("; ",reasons):"Combined read/write or send/receive throughput. Buffered event delivery can delay samples.";
    }
}

namespace ProcSight.Core.Models;
public enum TrayMetric { Cpu,Memory,Disk,Network,Gpu }
public enum TraySampleState { Available,WarmingUp,Unavailable }
public sealed record TraySample(TrayMetric Metric,double? Value,string Unit,TraySampleState State)
{
    public static TraySample FromPercent(TrayMetric metric,double? value)=>value is { } v&&double.IsFinite(v)&&v>=0
        ?new(metric,Math.Clamp(v,0,100),"%",TraySampleState.Available):new(metric,null,"%",TraySampleState.Unavailable);
    public string Label=>Metric switch { TrayMetric.Cpu=>"CPU",TrayMetric.Memory=>"Memory",TrayMetric.Disk=>"Disk busy",TrayMetric.Network=>"Network throughput",_=>"Busiest GPU engine" };
    public string Tooltip=>"ProcSight · "+Label+": "+(State==TraySampleState.Available&&Value is { } value
        ?Unit=="B/s"?$"{value/1024:0.0} KiB/s":$"{value:0.0}%":State==TraySampleState.WarmingUp?"Warming up":"Unavailable (no accessible sample)");
}

using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public static class NetworkRates
{
    public static (Observation<double> Total,Observation<double> Sent,Observation<double> Received) Calculate(
        IReadOnlyDictionary<string,(long Sent,long Received)>? before,
        IReadOnlyDictionary<string,(long Sent,long Received)> after,double seconds)
    {
        var pending=Observation<double>.Pending();
        if(before is null||seconds<=0||!double.IsFinite(seconds)) return(pending,pending,pending);
        double sent=0,received=0;
        foreach(var (id,value) in after) {
            // Adapter replacement and counter reset require a complete new baseline.
            if(!before.TryGetValue(id,out var old)||value.Sent<old.Sent||value.Received<old.Received) return(pending,pending,pending);
            sent+=value.Sent-old.Sent; received+=value.Received-old.Received;
        }
        return(Observation<double>.Available((sent+received)/seconds),Observation<double>.Available(sent/seconds),Observation<double>.Available(received/seconds));
    }
}

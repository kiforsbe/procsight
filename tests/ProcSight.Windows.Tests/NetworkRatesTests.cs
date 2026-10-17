using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public class NetworkRatesTests
{
    [Fact] public void AggregatesDirectionsWithElapsedTimeAndIdleZero() {
        var before=new Dictionary<string,(long,long)> { ["a"]=(100,200),["b"]=(300,400) };
        var after=new Dictionary<string,(long,long)> { ["a"]=(140,260),["b"]=(320,480) };
        var rates=NetworkRates.Calculate(before,after,2);
        Assert.Equal(30,rates.Sent.Value); Assert.Equal(70,rates.Received.Value); Assert.Equal(100,rates.Total.Value);
        Assert.Equal(0,NetworkRates.Calculate(after,after,2).Total.Value);
    }
    [Fact] public void RequiresNewBaselineOnReplacementResetAndInvalidIntervals() {
        var initial=new Dictionary<string,(long,long)> { ["a"]=(100,200) };
        Assert.Equal(ObservationState.Pending,NetworkRates.Calculate(null,initial,1).Total.State);
        foreach(var seconds in new[]{0d,-1,double.NaN,double.PositiveInfinity})
            Assert.Equal(ObservationState.Pending,NetworkRates.Calculate(initial,initial,seconds).Total.State);
        Assert.Equal(ObservationState.Pending,NetworkRates.Calculate(initial,new Dictionary<string,(long,long)> { ["a"]=(0,0) },1).Total.State);
        Assert.Equal(ObservationState.Pending,NetworkRates.Calculate(initial,new Dictionary<string,(long,long)> { ["b"]=(100,200) },1).Total.State);
        Assert.Equal(0,NetworkRates.Calculate(initial,new Dictionary<string,(long,long)>(),1).Total.Value);
    }
}

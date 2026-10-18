using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public class ProcessIoTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    [Fact] public void RatesWarmUpThenUseActualIntervalAndKeepIdleZero() {
        var meter=new ProcessIoAccumulator(); var key=new ProcessKey(12,Now,null);
        Assert.Equal(ObservationState.Pending,meter.Sample([key],Now)[key].Disk.State);
        meter.Add(12,Now.AddMilliseconds(500),200,100);
        var value=meter.Sample([key],Now.AddSeconds(2))[key];
        Assert.Equal(100,value.Disk.Value); Assert.Equal(50,value.Network.Value);
        Assert.Equal(0,meter.Sample([key],Now.AddSeconds(3))[key].Network.Value);
    }
    [Fact] public void PidReuseAndLateEventsNeverCarryOldTrafficIntoNewLifetime() {
        var meter=new ProcessIoAccumulator(); var first=new ProcessKey(12,Now,null);
        meter.Sample([first],Now); meter.Add(12,Now.AddMilliseconds(500),500,500);
        var next=new ProcessKey(12,Now.AddSeconds(1),null);
        Assert.Equal(ObservationState.Pending,meter.Sample([next],Now.AddSeconds(1))[next].Disk.State);
        meter.Add(12,Now.AddMilliseconds(500),900,900);
        meter.Add(12,Now.AddMilliseconds(1500),200,400);
        var value=meter.Sample([next],Now.AddSeconds(2))[next];
        Assert.Equal(200,value.Disk.Value); Assert.Equal(400,value.Network.Value);
    }
    [Fact] public void LossAndLifecycleInvalidationRequireFreshBaseline() {
        var meter=new ProcessIoAccumulator(); var key=new ProcessKey(12,Now,null);
        meter.Sample([key],Now); meter.Add(12,Now.AddMilliseconds(100),100,100);
        meter.Reset(); Assert.Equal(ObservationState.Pending,meter.Sample([key],Now.AddSeconds(1))[key].Disk.State);
        meter.Invalidate(12,Now.AddSeconds(2));
        Assert.Equal(ObservationState.Pending,meter.Sample([key],Now.AddSeconds(3))[key].Disk.State);
        meter.Sample([],Now.AddSeconds(4));
        Assert.Equal(0,meter.Count);
    }
}

using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public class ProcessGpuTests
{
    private static readonly ProcessKey Key=new(12,DateTimeOffset.Parse("2026-01-01T00:00:00Z"),null);
    private static string Engine(int pid,int engine,string suffix="")=>$"pid_{pid}_luid_0x00000000_0x00001234_phys_0_eng_{engine}_engtype_3D{suffix}";
    [Fact] public void TakesBusiestEngineAndWarmsNewLifetimes() {
        var counters=new Dictionary<string,double?> { [Engine(12,0)]=20,[Engine(12,0,"#1")]=10,[Engine(12,1)]=45,[Engine(99,0)]=90 };
        Assert.Equal(ObservationState.Pending,ProcessGpuProjection.Build(counters,[Key],new HashSet<ProcessKey>(),false)[Key].State);
        Assert.Equal(45,ProcessGpuProjection.Build(counters,[Key],new HashSet<ProcessKey>{Key},false)[Key].Value);
        var reused=Key with { StartedAt=Key.StartedAt!.Value.AddMinutes(1) };
        Assert.Equal(ObservationState.Pending,ProcessGpuProjection.Build(counters,[reused],new HashSet<ProcessKey>{Key},false)[reused].State);
    }
    [Fact] public void MissingInvalidWarmUpAndIdleStayDistinct() {
        var known=new HashSet<ProcessKey>{Key};
        Assert.Equal(ObservationState.Unavailable,ProcessGpuProjection.Build(null,[Key],known,false)[Key].State);
        Assert.Equal(ObservationState.Pending,ProcessGpuProjection.Build(null,[Key],known,true)[Key].State);
        Assert.Equal(ObservationState.Unavailable,ProcessGpuProjection.Build(new Dictionary<string,double?> { [Engine(12,0)]=null },[Key],known,false)[Key].State);
        Assert.Equal(0,ProcessGpuProjection.Build(new Dictionary<string,double?> { [Engine(99,0)]=20 },[Key],known,false)[Key].Value);
    }
}

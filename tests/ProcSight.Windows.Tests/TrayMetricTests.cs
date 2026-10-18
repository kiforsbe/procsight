using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class TrayMetricTests
{
    [Fact] public async Task CpuAndMemoryReuseAvailableSystemSamples()
    {
        using var sampler=new TrayMetricSampler();
        Assert.Equal(37,(await sampler.SampleAsync(TrayMetric.Cpu,new(37,100,40),default)).Value);
        Assert.Equal(60,(await sampler.SampleAsync(TrayMetric.Memory,new(37,100,40),default)).Value);
        Assert.Equal(TraySampleState.Unavailable,(await sampler.SampleAsync(TrayMetric.Cpu,null,default)).State);
    }
    [Fact] public void CounterWarmupAndInvalidValuesAreNotZero()
    {
        Assert.Null(TraySample.FromPercent(TrayMetric.Gpu,double.NaN).Value);
        Assert.Equal(TraySampleState.Unavailable,TraySample.FromPercent(TrayMetric.Memory,double.PositiveInfinity).State);
        Assert.Equal(100,TraySample.FromPercent(TrayMetric.Disk,120).Value);
        Assert.Equal(TraySampleState.WarmingUp,new TraySample(TrayMetric.Network,null,"B/s",TraySampleState.WarmingUp).State);
    }
    [Fact] public void NetworkResetAndNewAdaptersCannotSpike()
    {
        Assert.Null(TrayMetricSampler.NetworkDelta(new Dictionary<string,long>{{"a",100}},new Dictionary<string,long>{{"a",50}},1));
        Assert.Null(TrayMetricSampler.NetworkDelta(new Dictionary<string,long>{{"a",100}},new Dictionary<string,long>{{"b",100000000}},1));
        Assert.Equal(150,TrayMetricSampler.NetworkDelta(new Dictionary<string,long>{{"a",100}},new Dictionary<string,long>{{"a",400}},2));
    }
}

using ProcSight.Windows.Collection;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Windows.Tests;
public class SystemPerformanceTests
{
    [Fact] public async Task LocalSystemSamplingProvidesMemoryAndWarmsCpuWithoutAiOrInventory() {
        await using var sampler=new SystemPerformanceSampler();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var first=await sampler.CaptureAsync(timeout.Token);
        Assert.Equal(ObservationState.Pending,first.Cpu.State);
        Assert.Equal(ObservationState.Available,first.Memory.State);
        Assert.True(first.TotalMemory>0);
        await Task.Delay(1100,timeout.Token);
        var second=await sampler.CaptureAsync(timeout.Token);
        Assert.Equal(ObservationState.Available,second.Cpu.State);
        Assert.InRange(second.Cpu.Value!.Value,0,100);
        Assert.Equal(first.LogicalProcessors,second.LogicalProcessors);
    }
}

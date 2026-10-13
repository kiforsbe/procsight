using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class CollectorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-13T10:00:00Z");
    [Fact] public void FirstSampleIsPending() => Assert.Equal(ObservationState.Pending,
        new ProcessSampler().Sample(new(1, Now, null), TimeSpan.Zero, 20, Now, 4).CpuPercent.State);
    [Fact] public void PidReuseResetsCpuDelta()
    {
        var sampler = new ProcessSampler();
        sampler.Sample(new(1, Now, null), TimeSpan.FromSeconds(4), 20, Now, 4);
        Assert.Equal(ObservationState.Pending, sampler.Sample(new(1, Now.AddSeconds(1), null),
            TimeSpan.Zero, 20, Now.AddSeconds(2), 4).CpuPercent.State);
    }
    [Fact] public void UnavailableNetworkIsNotZero() => Assert.Null(new ProcessSampler()
        .Sample(new(1, Now, null), TimeSpan.Zero, 20, Now, 4).NetworkBytesPerSecond.Value);
    [Fact] public void ProtectedProcessKeepsPartialRow() => Assert.Equal(ObservationState.Unavailable,
        new ProcessSampler().Sample(new(1, null, Guid.NewGuid()), null, null, Now, 4).WorkingSetBytes.State);
    [Fact] public async Task TwoServicesShareOneHost()
    {
        var snapshot = await new WindowsSnapshotSource().CaptureAsync(CommandLineMode.Off, default);
        foreach (var group in snapshot.Services.Where(s => s.HostPid > 0).GroupBy(s => s.HostPid))
        {
            var process = snapshot.Processes.FirstOrDefault(p => p.Key.Pid == group.Key);
            if (process is not null) Assert.All(group, s => Assert.Contains(s.Name, process.ServiceNames));
        }
        Assert.Contains(snapshot.Processes, p => p.Key.Pid == Environment.ProcessId);
        Assert.All(snapshot.Processes, p => Assert.Null(p.CommandLine));
    }
}

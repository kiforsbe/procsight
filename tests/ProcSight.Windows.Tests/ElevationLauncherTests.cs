using ProcSight.Windows.Security;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class ElevationLauncherTests
{
    private sealed class Lease : IDisposable { public bool Disposed; public void Dispose()=>Disposed=true; }
    [Theory] [InlineData(ElevationResult.Cancelled)] [InlineData(ElevationResult.Failed)]
    public async Task ShellFailureNeverConfirmsReplacement(ElevationResult failure)
    {
        var launcher=new ElevationLauncher(()=>new(failure,null));
        Assert.Equal(failure,await launcher.RestartAsync(default));
    }
    [Fact] public async Task ReadinessIsRequiredAndProcessLeaseAlwaysCloses()
    {
        var lease=new Lease();
        var launcher=new ElevationLauncher(()=>new(null,new(123,()=>false,()=>false,lease)),TimeSpan.FromMilliseconds(5));
        Assert.Equal(ElevationResult.TimedOut,await launcher.RestartAsync(default)); Assert.True(lease.Disposed);
        lease=new(); launcher=new(()=>new(null,new(123,()=>false,()=>true,lease)));
        Assert.Equal(ElevationResult.Ready,await launcher.RestartAsync(default)); Assert.True(lease.Disposed);
    }
    [Fact] public async Task BootstrapExitRejectsReadyWindow()
    {
        var lease=new Lease(); var launcher=new ElevationLauncher(()=>new(null,new(123,()=>true,()=>true,lease)));
        Assert.Equal(ElevationResult.Failed,await launcher.RestartAsync(default)); Assert.True(lease.Disposed);
    }
}

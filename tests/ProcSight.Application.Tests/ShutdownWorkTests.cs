using ProcSight.Application.Lifecycle;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class ShutdownWorkTests
{
    [Fact] public async Task BlockingCleanupRunsOffCallerAndRepeatedRequestsShareCompletion() {
        var work=new ShutdownWork(); using var entered=new ManualResetEventSlim(); using var release=new ManualResetEventSlim();
        var calls=0;
        Task Cleanup() { Interlocked.Increment(ref calls); entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); return Task.CompletedTask; }
        var first=work.RunAsync(Cleanup); var second=work.RunAsync(Cleanup);
        try { Assert.Same(first,second); Assert.True(entered.Wait(TimeSpan.FromSeconds(2))); Assert.False(first.IsCompleted); }
        finally { release.Set(); }
        await first; Assert.Equal(1,calls);
    }
}

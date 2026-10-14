using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class ProcessLifetimeTests
{
    [Fact] public void MissingOrChangedConfirmationRejectsKernelLifetime()
    {
        var expected=new KernelProcessStamp(1,"fixture.exe",100);
        Assert.False(KernelProcessLifetimeReader.ConfirmsLifetime(expected,null));
        Assert.False(KernelProcessLifetimeReader.ConfirmsLifetime(expected,expected with {Sequence=101}));
        Assert.True(KernelProcessLifetimeReader.ConfirmsLifetime(expected,expected));
    }
    [Fact] public void KernelSequenceKeepsRestrictedProcessScopeAcrossCollections()
    {
        var session=Guid.NewGuid();
        Assert.Equal(ProcessLifetimeScope.Create(session,42,100),ProcessLifetimeScope.Create(session,42,100));
        Assert.NotEqual(ProcessLifetimeScope.Create(session,42,100),ProcessLifetimeScope.Create(session,42,101));
        Assert.NotEqual(ProcessLifetimeScope.Create(session,42,100),ProcessLifetimeScope.Create(Guid.NewGuid(),42,100));
    }
    [Fact] public void KernelEnumerationDistinguishesCurrentProcessLifetime()
    {
        var first=KernelProcessLifetimeReader.Read(); var second=KernelProcessLifetimeReader.Read();
        // The API is optional on older supported Windows builds.
        var build=Environment.OSVersion.Version;
        if(build.Build>26100||build.Build==26100&&build.Revision>=4770) { Assert.NotNull(first); Assert.NotNull(second); }
        if(first is null||second is null) return;
        Assert.Equal(first[Environment.ProcessId].Sequence,second[Environment.ProcessId].Sequence);
        Assert.NotEmpty(first[Environment.ProcessId].Name);
    }
    [Fact] public void PermissionFlappingDoesNotForgetProvenStartForSameKernelSequence()
    {
        var tracker=new ProcessLifetimeTracker(); var start=DateTimeOffset.UtcNow;
        var first=tracker.Resolve(42,start,100,Guid.NewGuid());
        Assert.Equal(first,tracker.Resolve(42,null,100,Guid.NewGuid()));
        Assert.Null(tracker.Resolve(42,null,101,Guid.NewGuid()).StartedAt);
    }
    [Fact] public void WithoutKernelProofMissingStartRemainsSnapshotScoped()
    {
        var tracker=new ProcessLifetimeTracker();
        Assert.NotEqual(tracker.Resolve(42,null,null,Guid.NewGuid()),tracker.Resolve(42,null,null,Guid.NewGuid()));
    }
    [Fact] public void ParserRejectsTruncatedAndInvalidEntryOffsets()
    {
        Assert.Null(KernelProcessLifetimeReader.Parse(IntPtr.Zero,1));
        var buffer=System.Runtime.InteropServices.Marshal.AllocHGlobal(128);
        try {
            System.Runtime.InteropServices.Marshal.Copy(new byte[128],0,buffer,128);
            System.Runtime.InteropServices.Marshal.WriteInt32(buffer,0,1);
            Assert.Null(KernelProcessLifetimeReader.Parse(buffer,128));
        } finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }
    [Fact] public async Task RestrictedLifetimeKeysRemainStableInLiveCollector()
    {
        if(KernelProcessLifetimeReader.Read() is null) return;
        var collector=new WindowsSnapshotSource();
        var first=await collector.CaptureAsync(ProcSight.Core.Models.CommandLineMode.Off,default);
        var second=await collector.CaptureAsync(ProcSight.Core.Models.CommandLineMode.Off,default);
        var live=second.Processes.ToDictionary(p=>p.Key.Pid);
        Assert.Equal(first.Processes.Single(p=>p.Key.Pid==0).Key,live[0].Key);
        foreach(var process in first.Processes.Where(p=>p.Key.StartedAt is null&&live.TryGetValue(p.Key.Pid,out var next)&&p.Key.SnapshotScope==next.Key.SnapshotScope))
            Assert.Equal(process.Key,live[process.Key.Pid].Key);
    }
}

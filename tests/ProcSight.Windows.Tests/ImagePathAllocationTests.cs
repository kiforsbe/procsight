using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class ImagePathAllocationTests
{
    [Fact] public void LongPathsGrowWithoutTruncationAndFailuresKeepTheirReason() {
        var expected=new string('x',17000); var calls=0;
        var result=WindowsNativeMethods.ReadImagePathBuffer(buffer=> {
            calls++; if(buffer.Length<=expected.Length) return(false,0,122);
            expected.CopyTo(0,buffer,0,expected.Length); return(true,expected.Length,0);
        });
        Assert.Equal(expected,result.Path); Assert.Null(result.Reason); Assert.InRange(calls,2,7);
        calls=0;
        result=WindowsNativeMethods.ReadImagePathBuffer(_=> { calls++; return(false,0,5); });
        Assert.Null(result.Path); Assert.Equal(ProcSight.Core.Models.MetadataUnavailableReason.AccessDenied,result.Reason); Assert.Equal(1,calls);
    }
    [Fact] public void OrdinaryImagePathReadsDoNotAllocateMaximumPathBuffers() {
        _=WindowsNativeMethods.ReadImagePath(Environment.ProcessId);
        var before=GC.GetAllocatedBytesForCurrentThread();
        for(var i=0;i<50;i++) Assert.NotNull(WindowsNativeMethods.ReadImagePath(Environment.ProcessId));
        Assert.True(GC.GetAllocatedBytesForCurrentThread()-before<512*1024,"Short path reads allocated maximum-length buffers.");
    }
}

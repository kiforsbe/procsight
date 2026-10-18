using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class MetadataAvailabilityTests
{
    [Fact] public void AccessDeniedShowsReasonWhilePreservingAvailableMetadata()
    {
        var process=Samples.Process(name:"fixture.exe",product:"Fixture") with {
            Executable=Samples.Process().Executable with { Name="fixture.exe",FullPath=null,ContentHash=null,MetadataIssues=[new("FileMetadata",MetadataUnavailableReason.AccessDenied)] },
            MetadataIssues=[new("ExecutablePath",MetadataUnavailableReason.AccessDenied),new("CommandLine",MetadataUnavailableReason.AccessDenied)] };
        var entity=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(process),[]));
        var text=ProcessDetails.FormatIdentity(entity,CommandLineMode.Off);
        Assert.Contains("fixture.exe",text); Assert.Contains("access denied by Windows",text); Assert.Contains("Command line:",text);
        Assert.DoesNotContain("another user",text);
    }
    [Fact] public void MissingFieldsDoNotInferPermissionDenial()
    {
        var entity=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(Samples.Process() with {CommandLine=null}),[]));
        Assert.Contains("not provided by Windows",ProcessDetails.FormatIdentity(entity,CommandLineMode.Off));
        Assert.DoesNotContain("access denied",ProcessDetails.FormatIdentity(entity,CommandLineMode.Off));
    }
}

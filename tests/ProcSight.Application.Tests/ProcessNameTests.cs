using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class ProcessNameTests
{
    private static ResolvedEntity Entity(ProcessSnapshot process)=>Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(process),[]));
    [Fact] public void EstablishedAppNameIsNotReplacedByAssessmentProse()
    {
        var process=Samples.Process(name:"example.exe",product:"Example App") with { Windows=[new("Example App","Fixture",Minimized:true)] };
        Assert.Equal("Example App",ProcessRoleLabels.DisplayName(Entity(process),"Example App application — metadata identifies Example as signer"));
    }
    [Fact] public void SpecificInvocationWinsOverAnyAssessedLabel()
    {
        var process=Samples.Process(name:"python.exe",product:"Python") with { CommandLine=new("python.exe worker.py",Samples.Now) };
        Assert.Equal("Python — worker.py",ProcessRoleLabels.DisplayName(Entity(process),"Python application — metadata identifies its publisher"));
    }
    [Fact] public void UnknownExecutableCanUseAConciseHelpfulAiName()
    {
        Assert.Equal("Example updater",ProcessRoleLabels.DisplayName(Entity(Samples.Process(name:"helper.exe")),"Example updater"));
    }
    [Fact] public void UnknownExecutableRejectsProseInsteadOfTruncatingIt()
    {
        var entity=Entity(Samples.Process(name:"helper.exe"));
        Assert.Equal("helper.exe",ProcessRoleLabels.DisplayName(entity,"Background process — metadata identifies a signer"));
    }
    [Fact] public void KnownAppCanRetainAnAllowlistedAssessedRole()
    {
        var entity=Entity(Samples.Process(name:"example.exe",product:"Example App"));
        Assert.Equal("Example App — Updater",ProcessRoleLabels.DisplayName(entity,"Example App — Updater"));
    }
}

using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class StructuredDetailsTests
{
    [Fact] public void StructuredDetailsKeepLocalCommandAndCaptionContext()
    {
        var process=Samples.Process() with { CommandLine=new("python.exe private_script.py",Samples.Now),Windows=[new("Private document","FixtureClass")] };
        var entity=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(process),[]));
        var sections=ProcessDetails.Sections(entity);
        Assert.Contains("private_script.py",Assert.Single(sections,s=>s.Key=="command").Fields.Single().Value);
        Assert.Contains("Private document",Assert.Single(sections,s=>s.Key=="windows").Fields.Single().Value);
        Assert.Contains(sections,s=>s.Key=="signature");
    }
    [Fact] public void StructuredDetailsPreserveRestrictedReadReasons()
    {
        var process=Samples.Process() with {CommandLine=null,MetadataIssues=[new("CommandLine",MetadataUnavailableReason.AccessDenied)]};
        var entity=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(process),[]));
        Assert.Contains("access denied",ProcessDetails.Sections(entity).Single(s=>s.Key=="command").Fields.Single().Value);
    }
    [Fact] public void LocalFieldValuesAreExcludedFromSerializationAndDiagnosticText()
    {
        var field=new LocalDetailField("Local invocation","PRIVATE_COMMAND");
        Assert.DoesNotContain("PRIVATE_COMMAND",System.Text.Json.JsonSerializer.Serialize(field));
        Assert.DoesNotContain("PRIVATE_COMMAND",field.ToString());
    }
    [Fact] public void ServiceDetailsKeepHostSignatureSeparateFromImplementation()
    {
        var service=new ServiceSnapshot("fixture","Fixture service","Running","Auto",42,"host.exe --fixture",ObservationState.Available,
            new("fixture.dll",Signature:SignatureStatus.Unavailable),new("host.exe",Signature:SignatureStatus.Verified));
        var snapshot=new MachineSnapshot(Guid.NewGuid(),Samples.Now,[],[service]);
        var entity=Assert.Single(new IdentityResolver().Resolve(snapshot,[]));
        var sections=ProcessDetails.Sections(entity);
        Assert.Contains("does not authenticate",sections.Single(s=>s.Key=="host").Fields.Last().Value);
        Assert.Equal("Unavailable",sections.Single(s=>s.Key=="signature").Fields.First().Value);
    }
}

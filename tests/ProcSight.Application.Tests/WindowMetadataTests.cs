using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Core.Privacy;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class WindowMetadataTests
{
    private static ResolvedEntity Entity(string title)
    {
        var process=Samples.Process(name:"notepad.exe",product:"Notepad") with { Windows=[new(title,"FixtureClass",true)] };
        return Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(process),[]));
    }
    [Fact] public void VisibleCaptionExplainsTheProcessWithoutRepeatingItsAppSuffix()=>
        Assert.Equal("Notepad — document.txt",ProcessRoleLabels.DisplayName(Entity("document.txt - Notepad")));
    [Fact] public void TitleChangesDoNotChangeAnalysisIdentityOrEnterPayloadsOrSerialization()
    {
        var entity=Entity("PRIVATE_DOCUMENT - Notepad");
        Assert.Equal(entity.Identity,Entity("another document - Notepad").Identity);
        Assert.DoesNotContain("PRIVATE_DOCUMENT",System.Text.Json.JsonSerializer.Serialize(entity));
        foreach(var destination in Enum.GetValues<AnalysisDestination>())
            Assert.DoesNotContain("PRIVATE_DOCUMENT",System.Text.Json.JsonSerializer.Serialize(new AnalysisProjection().Build(entity,
                new(CommandLineMode:CommandLineMode.OnlineFull,ShareIdentifyingMetadata:true),destination)));
        Assert.DoesNotContain("PRIVATE_DOCUMENT",entity.Process!.Windows![0].ToString());
        Assert.DoesNotContain("PRIVATE_DOCUMENT",System.Text.Json.JsonSerializer.Serialize(entity.Process.Windows[0]));
    }
    [Fact] public void ForegroundCaptionWinsWhileDetailsRetainOtherWindows()
    {
        var entity=Entity("inactive - Notepad");
        entity=entity with { Process=entity.Process! with { Windows=[new("inactive - Notepad","FixtureClass",Minimized:true),new("active - Notepad","FixtureClass",true)] } };
        Assert.Contains("active",ProcessRoleLabels.DisplayName(entity));
        Assert.Contains("inactive",ProcessDetails.FormatIdentity(entity,CommandLineMode.Off));
        Assert.Contains("FixtureClass",ProcessDetails.FormatIdentity(entity,CommandLineMode.Off));
    }
}

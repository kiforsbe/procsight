using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Core.Privacy;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Core.Tests;
public sealed class PrivacyTests
{
    [Fact] public void GeneratedRuleKeepsApplicationOwnershipContext()
    {
        var payload=new AnalysisPayload(new("fixture"),new(){["ownerIdentity"]="owner-a"},
            [new("owner-evidence",DataOrigin.ShareableMetadata,Samples.Now,null,"LocalMetadata",new(){["ownerIdentity"]="owner-a"})],null,0,AnalysisDestination.CloudModel);
        var rule=new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,Samples.Now,Samples.Now.AddDays(7),Samples.Now.AddDays(7),[],[],[],0);
        var projected=OutputPrivacyFilter.Project(new([], [rule]),payload);
        Assert.Contains(projected.Rules[0].Predicates,p=>p.Field=="ownerIdentity"&&p.Value=="owner-a");
    }
    private static ResolvedEntity Entity() => new IdentityResolver().Resolve(Samples.Snapshot(
        Samples.Process() with { CommandLine=new("\"C:\\private app\\node.exe\" --token=SYNTHETIC_SECRET",Samples.Now) }),[])[0];
    [Theory] [InlineData(CommandLineMode.Off)] [InlineData(CommandLineMode.LocalOnly)]
    public void DefaultAndLocalOnlyCloudOmitFullLine(CommandLineMode mode) => Assert.Null(
        new AnalysisProjection().Build(Entity(),new(CommandLineMode:mode),AnalysisDestination.CloudModel).CommandLine);
    [Fact] public void OnlineFullPreservesQuotedCommandVerbatim() => Assert.Equal(Entity().Process!.CommandLine!.Raw,
        new AnalysisProjection().Build(Entity(),new(CommandLineMode:CommandLineMode.OnlineFull),AnalysisDestination.CloudModel).CommandLine);
    [Fact] public void LocalOnlyCloudCannotSeeDerivedText()
    {
        var entity=Entity(); entity.Evidence.Add(new("local-command",DataOrigin.LocalCommandLine,Samples.Now,null,"Command",new(){["role"]="SYNTHETIC_SECRET"}));
        Assert.DoesNotContain(new AnalysisProjection().Build(entity,new(CommandLineMode:CommandLineMode.LocalOnly),AnalysisDestination.CloudModel).Evidence,e=>e.Id=="local-command");
    }
    [Fact] public void ResearchNeverSeesCommandLine() => Assert.DoesNotContain("SYNTHETIC_SECRET",
        System.Text.Json.JsonSerializer.Serialize(new AnalysisProjection().BuildResearchRequest(Entity())));
    [Fact] public void UserPathsNormalize() => Assert.DoesNotContain("Alice",MetadataSanitizer.Normalize(@"C:\Users\Alice\Documents\app.exe"));
    [Fact] public void EchoedSecretIsNotPersistable()
    {
        var request=new AnalysisProjection().Build(Entity(),new(CommandLineMode:CommandLineMode.OnlineFull),AnalysisDestination.CloudModel);
        var result=OutputPrivacyFilter.Project(new([new(AssessmentAxis.Purpose,"DeveloperTool","SYNTHETIC_SECRET",Confidence.Low,[],Samples.Now,[DataOrigin.ShareableMetadata])],[]),request);
        Assert.DoesNotContain("SYNTHETIC_SECRET",System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Contains(DataOrigin.OnlineCommandLine,result.Assessments[0].Origins);
    }
    [Fact] public void PolicyRevisionChangeRejectsPreparedPayload() => Assert.False(CommandLinePolicy.CanDispatch(
        new AnalysisProjection().Build(Entity(),new(Revision:1),AnalysisDestination.CloudModel),new(Revision:2)));
    [Fact] public void RejectedCommandRuleCannotPersistEchoedArgument()
    {
        var request=new AnalysisProjection().Build(Entity(),new(CommandLineMode:CommandLineMode.OnlineFull),AnalysisDestination.CloudModel);
        var rule=new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,Samples.Now,Samples.Now.AddDays(7),Samples.Now.AddDays(7),
            [new(PredicateKind.Exact,"commandRole","SYNTHETIC_SECRET",["SYNTHETIC_SECRET"])],[],["SYNTHETIC_SECRET"],0);
        var result=OutputPrivacyFilter.Project(new([],[rule]),request);
        Assert.DoesNotContain("SYNTHETIC_SECRET",System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Equal(RuleState.Rejected,result.Rules[0].State);
    }
}

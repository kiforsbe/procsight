using System.Text.Json;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Core.Tests;
public sealed class RuleContributionTests
{
    private static ResolvedEntity Entity()=>new IdentityResolver().Resolve(Samples.Snapshot(Samples.Process()),[])[0];
    private static RuleDefinition Rule(ResolvedEntity entity,string value="DeveloperTool",Confidence confidence=Confidence.High)
    {
        var evidence=entity.Evidence[0].Id;
        return new(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,Samples.Now,Samples.Now.AddDays(7),Samples.Now.AddDays(30),
            [new(PredicateKind.Exact,"hash","hash-a",[evidence])],
            [new(AssessmentAxis.Purpose,value,"Fixture",confidence,[evidence],Samples.Now.AddDays(30),[DataOrigin.ShareableMetadata])],[evidence],100);
    }
    private static List<Guid> Applied(LocalAnalysis result)
    {
        return Assert.IsType<List<Guid>>(result.AppliedRuleIds);
    }
    [Fact] public void OnlySelectedAssessmentOwnerIsApplied()
    {
        var entity=Entity(); var low=Rule(entity,confidence:Confidence.Low); var high=Rule(entity);
        var knowledge=KnowledgeEntry.Empty(entity.Identity) with { Rules=[low,high],Evidence=entity.Evidence };
        var result=new LocalAnalysisEngine().Evaluate(entity,knowledge,Samples.Now);
        Assert.Equal(new[]{low.Id,high.Id},result.MatchingRuleIds); Assert.Equal(new[]{high.Id},Applied(result));
    }
    [Fact] public void EqualAssessmentValueDoesNotAttributeCorrectionToRule()
    {
        var entity=Entity(); var rule=Rule(entity);
        var correction=rule.Assessments[0] with { };
        var knowledge=KnowledgeEntry.Empty(entity.Identity) with { Rules=[rule],Evidence=entity.Evidence,
            Corrections=[new(Guid.NewGuid(),CorrectionScope.Executable,"fixture",correction)] };
        var result=new LocalAnalysisEngine().Evaluate(entity,knowledge,Samples.Now);
        Assert.Single(result.MatchingRuleIds); Assert.Empty(Applied(result)); Assert.Same(correction,Assert.Single(result.Assessments));
    }
    [Fact] public void CorrectionReusingAssessmentInstanceStillOverridesRule()
    {
        var entity=Entity(); var rule=Rule(entity); var correction=rule.Assessments[0];
        var knowledge=KnowledgeEntry.Empty(entity.Identity) with { Rules=[rule],Evidence=entity.Evidence,
            Corrections=[new(Guid.NewGuid(),CorrectionScope.Executable,"fixture",correction)] };
        var result=new LocalAnalysisEngine().Evaluate(entity,knowledge,Samples.Now);
        Assert.Single(result.MatchingRuleIds); Assert.Empty(Applied(result));
    }
    [Fact] public void ConflictingRulesAreMatchedButNotApplied()
    {
        var entity=Entity(); var knowledge=KnowledgeEntry.Empty(entity.Identity) with { Rules=[Rule(entity),Rule(entity,"Updater")],Evidence=entity.Evidence };
        var result=new LocalAnalysisEngine().Evaluate(entity,knowledge,Samples.Now);
        Assert.Equal(2,result.MatchingRuleIds.Count); Assert.Empty(Applied(result));
    }
    [Fact] public void CachedAssessmentAndDisplayLabelDoNotImplyRuleContribution()
    {
        var entity=Entity(); var rule=Rule(entity,confidence:Confidence.Low) with { IdentityLabel="Fixture label" };
        var cached=Rule(entity).Assessments[0];
        var knowledge=KnowledgeEntry.Empty(entity.Identity) with { Rules=[rule],Evidence=entity.Evidence,Assessments=[cached],RuleDependencies=[rule.Id] };
        var result=new LocalAnalysisEngine().Evaluate(entity,knowledge,Samples.Now);
        Assert.Same(cached,Assert.Single(result.Assessments)); Assert.Equal("Fixture label",result.IdentityLabel);
        Assert.Single(result.MatchingRuleIds); Assert.Empty(Applied(result));
    }
    [Fact] public void IndependentSecurityConflictKeepsActualSelectedRuleContribution()
    {
        var entity=Entity(); var purpose=Rule(entity); var evidence=entity.Evidence[0].Id;
        var security=new Assessment(AssessmentAxis.Security,"Suspicious","Fixture",Confidence.High,[evidence],Samples.Now.AddDays(7),[DataOrigin.ShareableMetadata]);
        var rule=purpose with { Assessments=[security] };
        var correction=security with { Value="NoConcernObserved",Origins=[DataOrigin.UserCorrection] };
        var knowledge=KnowledgeEntry.Empty(entity.Identity) with { Rules=[rule],Evidence=entity.Evidence,
            Corrections=[new(Guid.NewGuid(),CorrectionScope.Executable,"fixture",correction)] };
        var result=new LocalAnalysisEngine().Evaluate(entity,knowledge,Samples.Now);
        Assert.Contains("User correction conflicts with independent security evidence",result.Issues);
        Assert.Same(security,Assert.Single(result.Assessments)); Assert.Equal(new[]{rule.Id},Applied(result));
    }
    [Fact] public void LegacyRuleJsonHasNoProviderHistory()
    {
        var json=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Rule(Entity())))!.AsObject();
        json.Remove("Provenance");
        var restored=JsonSerializer.Deserialize<RuleDefinition>(json.ToJsonString())!;
        Assert.Null(restored.Provenance);
    }
    [Fact] public void LegacyAnalysisJsonDoesNotInventAppliedRules()
    {
        var original=new LocalAnalysis([],null,[Guid.NewGuid()],[],Enum.GetValues<AssessmentAxis>().ToHashSet());
        var json=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(original))!.AsObject(); json.Remove("AppliedRuleIds");
        Assert.Null(JsonSerializer.Deserialize<LocalAnalysis>(json.ToJsonString())!.AppliedRuleIds);
    }
    [Fact] public void PublisherOnlyDefinitionIsMatchedWithoutBeingApplied()
    {
        var process=Samples.Process(name:"codex.exe") with { Executable=new("codex.exe",Signature:SignatureStatus.Verified,VerifiedSigner:"OpenAI OpCo, LLC") };
        var entity=new IdentityResolver().Resolve(Samples.Snapshot(process),[])[0];
        var common=KnownTaskCatalog.Evaluate(entity,Samples.Now)!; Assert.Single(common.MatchingRuleIds); Assert.Empty(Applied(common));
        var result=KnownTaskCatalog.Supplement(entity,new([],null,[],[],Enum.GetValues<AssessmentAxis>().ToHashSet()),KnowledgeEntry.Empty(entity.Identity),Samples.Now);
        Assert.Single(result.MatchingRuleIds); Assert.Empty(Applied(result));
    }
    [Fact] public void BundledPurposeIsAppliedOnlyWhenItContributes()
    {
        var process=Samples.Process() with { Executable=new("svchost.exe",@"C:\Windows\System32\svchost.exe",Signature:SignatureStatus.Verified,
            VerifiedSigner:"Microsoft Windows",OriginalName:"svchost.exe") };
        var entity=new IdentityResolver().Resolve(Samples.Snapshot(process),[])[0];
        var common=KnownTaskCatalog.Evaluate(entity,Samples.Now)!; Assert.Equal(common.MatchingRuleIds,Applied(common));
        var local=new LocalAnalysis([],null,[],["Conflicting Purpose assessments"],Enum.GetValues<AssessmentAxis>().ToHashSet());
        Assert.Empty(Applied(KnownTaskCatalog.Supplement(entity,local,KnowledgeEntry.Empty(entity.Identity),Samples.Now)));
    }
}

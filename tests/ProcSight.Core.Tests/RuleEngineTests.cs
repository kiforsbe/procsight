using ProcSight.Core.Models;
using ProcSight.Core.Identity;
using ProcSight.Core.Rules;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Core.Tests;
public sealed class RuleEngineTests
{
    private static ResolvedEntity Entity()=>new IdentityResolver().Resolve(Samples.Snapshot(Samples.Process()),[])[0];
    private static RuleDefinition Rule(AssessmentAxis axis=AssessmentAxis.Purpose,string value="DeveloperTool")
    {
        var entity=Entity(); var evidence=entity.Evidence[0].Id;
        return new(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,Samples.Now,Samples.Now.AddDays(7),Samples.Now.AddDays(30),
            [new(PredicateKind.Exact,"hash","hash-a",[evidence])],
            [new(axis,value,"Fixture evidence",Confidence.High,[evidence],Samples.Now.AddDays(axis==AssessmentAxis.Purpose?30:7),[DataOrigin.ShareableMetadata])],
            [evidence],100);
    }
    [Fact] public void NameOnlyRuleRejected()
    {
        var rule=Rule() with { Predicates=[new(PredicateKind.Exact,"name","node.exe",[Entity().Evidence[0].Id])] };
        Assert.False(new RuleValidator().Validate(rule,Entity().Evidence,Samples.Now).Accepted);
    }
    [Fact] public void MissingSignerDoesNotMatch()
    {
        var rule=Rule() with { Predicates=[new(PredicateKind.Exact,"signer","missing",[]),new(PredicateKind.Exact,"product","Unknown",[])] };
        Assert.Empty(new LocalAnalysisEngine().Evaluate(Entity(),KnowledgeEntry.Empty(Entity().Identity) with { Rules=[rule] },Samples.Now).Assessments);
    }
    [Theory] [InlineData(AssessmentAxis.Purpose,"DeveloperTool")] [InlineData(AssessmentAxis.Security,"Suspicious")] [InlineData(AssessmentAxis.Optionality,"Optional")]
    public void AllAxesActivateAfterValidation(AssessmentAxis axis,string value)
    {
        var entity=Entity(); var rule=Rule(axis,value);
        Assert.True(new RuleValidator().Validate(rule,entity.Evidence,Samples.Now).Accepted);
        Assert.Single(new LocalAnalysisEngine().Evaluate(entity,KnowledgeEntry.Empty(entity.Identity) with { Rules=[rule],Evidence=entity.Evidence },Samples.Now).Assessments);
    }
    [Fact] public void ConflictingRulesQueueUnresolvedAxis()
    {
        var entity=Entity(); var knowledge=KnowledgeEntry.Empty(entity.Identity) with { Rules=[Rule(),Rule(value:"Updater")],Evidence=entity.Evidence };
        Assert.Contains(AssessmentAxis.Purpose,new LocalAnalysisEngine().Evaluate(entity,knowledge,Samples.Now).UnresolvedAxes);
    }
    [Fact] public void PurposeExpiresAfterThirtyDays() => Assert.Empty(new LocalAnalysisEngine().Evaluate(Entity(),
        KnowledgeEntry.Empty(Entity().Identity) with { Rules=[Rule()],Evidence=Entity().Evidence },Samples.Now.AddDays(31)).Assessments);
    [Fact] public void DisabledRuleInvalidatesDependents() => Assert.Empty(new LocalAnalysisEngine().Evaluate(Entity(),
        KnowledgeEntry.Empty(Entity().Identity) with { Rules=[Rule() with { State=RuleState.Disabled }],Evidence=Entity().Evidence },Samples.Now).Assessments);
    [Fact] public void HashThreatDoesNotGeneralizeToFamily() => Assert.False(new RuleValidator().Validate(
        Rule(AssessmentAxis.Security,"KnownThreat"),Entity().Evidence,Samples.Now).Accepted);
    [Fact] public void CachedIdentityLabelExplainsGenericExecutable()
    {
        var entity=Entity(); var rule=Rule() with { IdentityLabel="Example developer tool",Role="Worker" };
        var result=new LocalAnalysisEngine().Evaluate(entity,KnowledgeEntry.Empty(entity.Identity) with { Rules=[rule],Evidence=entity.Evidence },Samples.Now);
        Assert.Equal("Example developer tool",result.IdentityLabel); Assert.Equal("Worker",result.Role);
    }
    [Fact] public void ExpiredPredicateEvidenceCannotHideOutsideRuleEvidenceList()
    {
        var entity=Entity();
        var expired=entity.Evidence[0] with { Id="expired",ExpiresAt=Samples.Now.AddSeconds(-1) };
        var rule=Rule() with { Predicates=[new(PredicateKind.Exact,"hash","hash-a",["expired"])] };
        Assert.False(new RuleValidator().Validate(rule,[..entity.Evidence,expired],Samples.Now).Accepted);
    }
    [Fact] public void ExactCacheDoesNotReuseChangedSupportingMetadata()
    {
        var entity=Entity(); entity.Facts["version"]="2";
        var source=new EvidenceRecord("old-version",DataOrigin.ShareableMetadata,Samples.Now,null,"LocalMetadata",new(){["version"]="1"});
        var assessment=new Assessment(AssessmentAxis.Security,"NoConcernObserved","Version-specific fixture",Confidence.Low,[source.Id],Samples.Now.AddDays(7),[DataOrigin.ShareableMetadata]);
        var cached=KnowledgeEntry.Empty(entity.Identity) with { Evidence=[source],Assessments=[assessment] };
        Assert.Empty(new LocalAnalysisEngine().Evaluate(entity,cached,Samples.Now).Assessments);
    }
}

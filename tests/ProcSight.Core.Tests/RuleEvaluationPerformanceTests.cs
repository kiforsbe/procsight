using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Core.Tests;
public sealed class RuleEvaluationPerformanceTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public void RulesShareOneEvidenceIndexPerEvaluation(bool matching)
    {
        var entity=new IdentityResolver().Resolve(Samples.Snapshot(Samples.Process()),[])[0];
        var evidence=Enumerable.Range(0,200).Select(i=>new EvidenceRecord("fixture-"+i,DataOrigin.ShareableMetadata,
            Samples.Now,Samples.Now.AddDays(30),"LocalMetadata",new(){["hash"]=matching?"hash-a":"other-"+i})).ToList();
        var rules=evidence.Select(e=>new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,
            Samples.Now,Samples.Now.AddDays(7),Samples.Now.AddDays(30),[new(PredicateKind.Exact,"hash",e.Facts["hash"],[e.Id])],
            [new(AssessmentAxis.Purpose,"DeveloperTool","Synthetic fixture",Confidence.High,[e.Id],Samples.Now.AddDays(30),[DataOrigin.ShareableMetadata])],[e.Id],100)).ToList();
        var knowledge=KnowledgeEntry.Empty(entity.Identity) with { Evidence=evidence,Rules=rules };
        var engine=new LocalAnalysisEngine();
        engine.Evaluate(entity,knowledge,Samples.Now);
        var before=GC.GetAllocatedBytesForCurrentThread();
        var result=engine.Evaluate(entity,knowledge,Samples.Now);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(matching?rules.Count:0,result.MatchingRuleIds.Count);
        // A single evaluation must not allocate a complete evidence map for each rule.
        Assert.True(allocated<1_000_000,$"Rule evaluation allocated {allocated} bytes.");
    }

    [Fact] public void CurrentEntityEvidenceOverridesStoredEvidenceWithTheSameId()
    {
        var entity=new IdentityResolver().Resolve(Samples.Snapshot(Samples.Process()),[])[0];
        var current=entity.Evidence[0];
        var stored=current with { Facts=new(){["hash"]="stale-hash"},ExpiresAt=Samples.Now.AddSeconds(-1) };
        var rule=new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,
            Samples.Now,Samples.Now.AddDays(7),Samples.Now.AddDays(30),[new(PredicateKind.Exact,"hash","hash-a",[current.Id])],
            [new(AssessmentAxis.Purpose,"DeveloperTool","Synthetic fixture",Confidence.High,[current.Id],Samples.Now.AddDays(30),[DataOrigin.ShareableMetadata])],[current.Id],100);
        var result=new LocalAnalysisEngine().Evaluate(entity,KnowledgeEntry.Empty(entity.Identity) with { Evidence=[stored],Rules=[rule] },Samples.Now);
        Assert.Equal(rule.Id,Assert.Single(result.MatchingRuleIds));
        Assert.Equal(rule.Id,Assert.Single(result.AppliedRuleIds!));
    }
}

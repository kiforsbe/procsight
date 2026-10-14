using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class RuleInspectionTests
{
    private static RuleDefinition Rule()=>new(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,Samples.Now,
        Samples.Now.AddDays(7),Samples.Now.AddDays(7),[new(PredicateKind.Exact,"hash","fixture-hash",["fixture-evidence"])],
        [new(AssessmentAxis.Purpose,"Application","Fixture reason",Confidence.Medium,["fixture-evidence"],Samples.Now.AddDays(7),[DataOrigin.ShareableMetadata])],["fixture-evidence"],100);
    [Fact] public void LegacyRulesDoNotInventAnAiProvider()=>Assert.Contains("not recorded",RuleInspection.Source(Rule()));
    [Fact] public void AiRuleShowsReadableSourceAndConditions()
    {
        var rule=Rule() with { Provenance=new("chatgpt","fixture-model",AnalysisDestination.CloudModel,Samples.Now) };
        var text=RuleInspection.Describe(rule,[],Samples.Now);
        Assert.Contains("AI-generated",text); Assert.Contains("ChatGPT plan",text); Assert.Contains("fixture-model",text);
        Assert.Contains("File hash",text); Assert.Contains("fixture-hash",text); Assert.Contains("Fixture reason",text);
    }
    [Fact] public void MatchingAndAppliedAreDifferentStatuses()
    {
        var rule=Rule();
        var analysis=new LocalAnalysis([],null,[rule.Id],[],[],AppliedRuleIds:[]);
        Assert.Equal("Matched, not contributing",RuleInspection.ApplicationStatus(rule.Id,analysis));
        Assert.Equal("Applied",RuleInspection.ApplicationStatus(rule.Id,analysis with { AppliedRuleIds=[rule.Id] }));
    }
    [Fact] public void PrivateInvocationScopeIsReadableWithoutExposingItsFingerprint()
    {
        var rule=Rule() with { Predicates=[new(PredicateKind.Exact,"commandContext",new string('a',64),["fixture-evidence"])] };
        var text=RuleInspection.Describe(rule,[],Samples.Now);
        Assert.Contains("Invocation context",text); Assert.DoesNotContain(new string('a',64),text);
    }
}

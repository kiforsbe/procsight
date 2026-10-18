using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class RuleInspectionTests
{
    [Fact] public void RejectedRuleExplainsOriginalDecisionSeparatelyFromCurrentEvidence()
    {
        var rule=Rule() with { State=RuleState.Rejected,RejectionReasons=[RuleRejectionReason.PrivatePredicateOmitted] };
        var text=RuleInspection.Rejection(rule,[],Samples.Now);
        Assert.Contains("not supported by submitted metadata",text);
        Assert.Contains("Analyze again",text);
        Assert.Contains("Current evidence",text);
    }
    [Fact] public void LegacyRejectionDoesNotInventHistoricalCause()=>Assert.Contains("original rejection reason was not recorded",
        RuleInspection.Rejection(Rule() with { State=RuleState.Rejected },[],Samples.Now));
    [Fact] public void ResearchHistoryDistinguishesMissingConfigurationFromNoSources()
    {
        Assert.Contains("not run",RuleInspection.Research(ResearchOutcome.Unavailable));
        Assert.Contains("no attributable",RuleInspection.Research(ResearchOutcome.NoSources));
        Assert.Contains("not recorded",RuleInspection.Research(null));
    }
    private static RuleDefinition Rule()=>new(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,Samples.Now,
        Samples.Now.AddDays(7),Samples.Now.AddDays(7),[new(PredicateKind.Exact,"hash","fixture-hash",["fixture-evidence"])],
        [new(AssessmentAxis.Purpose,"Application","Fixture reason",Confidence.Medium,["fixture-evidence"],Samples.Now.AddDays(7),[DataOrigin.ShareableMetadata])],["fixture-evidence"],100);
    [Fact] public void LegacyRulesDoNotInventAnAiProvider()=>Assert.Contains("not recorded",RuleInspection.Source(Rule()));
    [Fact] public void BundledWindowsInspectionIncludesDefaultLocationGates()
    {
        var definition=ProcSight.Core.Rules.KnownTaskCatalog.Definitions.First(d=>d.Executable=="svchost.exe"&&d.ServiceName is null);
        Assert.Equal("System32, SysWOW64, SystemApps",RuleInspection.Locations(definition));
    }
    [Fact] public void FileScopedRuleUsesAssociatedMetadataForReadableName()
    {
        var rule=Rule();
        var evidence=new EvidenceRecord("fixture-evidence",DataOrigin.ShareableMetadata,Samples.Now,null,"File",new(){["hash"]="fixture-hash",["name"]="worker.exe",["product"]="Example editor"});
        Assert.Equal("Example editor",RuleInspection.Name(rule,[evidence]));
    }
    [Fact] public void UnrelatedMetadataCannotNameAHashScopedRule()
    {
        var rule=Rule();
        var evidence=new EvidenceRecord("fixture-evidence",DataOrigin.ShareableMetadata,Samples.Now,null,"File",new(){["hash"]="different-hash",["name"]="unrelated.exe"});
        Assert.Equal("File-specific rule",RuleInspection.Name(rule,[evidence]));
    }
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

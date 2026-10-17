using ProcSight.Core.Models;
using ProcSight.Core.Rules;
namespace ProcSight.Application.Analysis;
internal static class ExactAnalysisRule
{
    // A provider may return a useful assessment but no reusable rule. Retain it for this exact
    // binary, never infer a broader identity or rescue a rejected provider rule.
    public static AnalysisProposal Ensure(AnalysisProposal projected,AnalysisPayload payload,DateTimeOffset now)
    {
        if(projected.Rules.Count!=0||!projected.Assessments.Any(a=>a.Axis==AssessmentAxis.Purpose&&a.Value!="Unknown")||
            !payload.Metadata.TryGetValue("hash",out var hash)||hash.Length is <1 or >512) return projected;
        var proof=payload.Evidence.Where(e=>e.Facts.TryGetValue("hash",out var value)&&
            PredicateMatcher.Matches(new(PredicateKind.Exact,"hash",hash,[]),value)).Select(e=>e.Id).Distinct().ToList();
        if(proof.Count==0) return projected;
        // Unknown axes may legitimately have no evidence. Keep them in the entry, not in a
        // reusable rule that would otherwise fail evidence validation for its known purpose.
        var outputs=projected.Assessments.Where(a=>a.Value!="Unknown").ToList();
        var expires=outputs.Min(a=>a.ExpiresAt);
        if(expires<=now) return projected;
        var origin=payload.CommandLine is null?DataOrigin.ShareableMetadata:payload.Destination==AnalysisDestination.LocalModel?
            DataOrigin.LocalCommandLine:DataOrigin.OnlineCommandLine;
        var rule=new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,origin,now,expires,expires,
            [new(PredicateKind.Exact,"hash",hash,proof)],outputs,
            proof.Concat(outputs.SelectMany(a=>a.EvidenceIds)).Distinct().ToList(),100,
            IdentityLabel:projected.IdentityLabel,Role:projected.Role);
        // The coordinator adds local ownership/service boundaries and, when needed, private
        // invocation HMAC scope, then applies full validation and trusted provider provenance.
        return projected with { Rules=[rule] };
    }
}

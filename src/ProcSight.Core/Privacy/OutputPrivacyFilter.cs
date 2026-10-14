using ProcSight.Core.Models;
namespace ProcSight.Core.Privacy;
public static class OutputPrivacyFilter
{
    public static AnalysisProposal Project(AnalysisProposal proposal,AnalysisPayload request)
    {
        var origin=request.CommandLine is null ? DataOrigin.ShareableMetadata : request.Destination==AnalysisDestination.CloudModel
            ? DataOrigin.OnlineCommandLine : DataOrigin.LocalCommandLine;
        Assessment ProjectAssessment(Assessment assessment) => assessment with {
            Reason=request.CommandLine is null ? MetadataSanitizer.Normalize(assessment.Reason) :
                "Assessment uses permitted command-line context; raw explanation is not persisted.",
            Origins=[..assessment.Origins,origin] };
        var label=request.CommandLine is null ? proposal.IdentityLabel is null?null:MetadataSanitizer.Normalize(proposal.IdentityLabel):
            request.Metadata.GetValueOrDefault("owner") ?? request.Metadata.GetValueOrDefault("product") ?? request.Metadata.GetValueOrDefault("name");
        var role=proposal.Role is "Renderer" or "Utility" or "Worker" or "CLI" or "Updater" or "Service" or "Background helper" or "Unknown"?proposal.Role:null;
        var knownIds=request.Evidence.Select(e=>e.Id).ToHashSet();
        RuleDefinition ProjectRule(RuleDefinition rule)
        {
            var scoped=rule.Predicates.ToList();
            if(request.Metadata.TryGetValue("ownerIdentity",out var owner) && !scoped.Any(p=>p.Field=="ownerIdentity"))
                scoped.Add(new(PredicateKind.Exact,"ownerIdentity",owner,request.Evidence.Where(e=>e.Facts.GetValueOrDefault("ownerIdentity")==owner).Select(e=>e.Id).ToList()));
            var context=request.Evidence.FirstOrDefault(e=>e.Origin==origin && e.Facts.ContainsKey("commandRole"));
            if(request.CommandLine is not null && context is not null && !scoped.Any(p=>p.Field=="commandRole"))
                scoped.Add(new(PredicateKind.Exact,"commandRole",context.Facts["commandRole"],[context.Id]));
            var unsafePredicate=request.CommandLine is not null && rule.Predicates.Any(p=>
                !request.Evidence.Any(e=>e.Facts.TryGetValue(p.Field,out var observed)&&observed==p.Value));
            return rule with {
            Origin=origin, Assessments=rule.Assessments.Select(ProjectAssessment).ToList(),
            IdentityLabel=label,Role=role,
            Predicates=scoped.Select(p=>unsafePredicate?new Predicate(PredicateKind.Exact,"invalid","Private proposal omitted",[]):
                p with { Value=MetadataSanitizer.Normalize(p.Value),EvidenceIds=p.EvidenceIds.Where(knownIds.Contains).ToList() }).ToList(),
            EvidenceIds=rule.EvidenceIds.Where(knownIds.Contains).ToList(),
            State=unsafePredicate ? RuleState.Rejected : rule.State };
        }
        return new(proposal.Assessments.Select(ProjectAssessment).ToList(),proposal.Rules.Select(ProjectRule).ToList(),label,role);
    }
}

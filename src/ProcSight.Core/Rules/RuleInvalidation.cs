using ProcSight.Core.Models;
namespace ProcSight.Core.Rules;
public static class RuleInvalidation
{
    public static IReadOnlySet<Guid> FindAffected(EvidenceChange change,IReadOnlyList<RuleDefinition> rules) => rules
        .Where(r=>r.EvidenceIds.Any(change.EvidenceIds.Contains) || r.Predicates.Any(p=>
            change.OldFacts.GetValueOrDefault(p.Field)!=change.NewFacts.GetValueOrDefault(p.Field)))
        .Select(r=>r.Id).ToHashSet();
}

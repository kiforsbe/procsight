using ProcSight.Core.Models;
namespace ProcSight.Core.Rules;
public sealed class RuleValidator
{
    public static readonly Dictionary<AssessmentAxis,HashSet<string>> Values=new() {
        [AssessmentAxis.Purpose]=["OSComponent","Application","BackgroundHelper","Updater","DeveloperTool","Unknown"],
        [AssessmentAxis.Security]=["NoConcernObserved","Suspicious","KnownThreat","Unknown"],
        [AssessmentAxis.Optionality]=["Essential","Optional","PotentiallyUnwanted","PossibleLeftover","Unknown"] };
    public RuleValidationResult Validate(RuleDefinition rule,IReadOnlyList<EvidenceRecord> evidence,DateTimeOffset now)
    {
        var reasons=new List<string>(); var byId=evidence.GroupBy(e=>e.Id).ToDictionary(g=>g.Key,g=>g.Last());
        if(rule.Version<1 || rule.Predicates.Count is <1 or >64 || rule.Assessments.Count==0) reasons.Add("Invalid structure");
        var fields=rule.Predicates.Select(p=>p.Field).ToHashSet();
        if(!fields.Contains("hash") && fields.Intersect(new[]{"signer","product","path","package","owner","service"}).Count()<2)
            reasons.Add("Identity requires hash or independent identifying fields");
        foreach(var predicate in rule.Predicates)
        {
            if(predicate.Field is not ("name" or "hash" or "signer" or "product" or "path" or "package" or "owner" or "ownerIdentity" or "service" or "commandRole") ||
                predicate.Value.Length is <1 or >512 || !Enum.IsDefined(predicate.Kind)) reasons.Add("Invalid predicate");
            if(predicate.EvidenceIds.Count==0 || !predicate.EvidenceIds.Any(id=>byId.TryGetValue(id,out var e) &&
                e.Facts.TryGetValue(predicate.Field,out var observed) && PredicateMatcher.Matches(predicate,observed)))
                reasons.Add("Predicate lacks supporting evidence");
        }
        foreach(var assessment in rule.Assessments)
        {
            if(!Values.TryGetValue(assessment.Axis,out var values) || !values.Contains(assessment.Value)) reasons.Add("Invalid assessment value");
            var days=assessment.Axis==AssessmentAxis.Purpose?30:7;
            if(assessment.ExpiresAt<=now || assessment.ExpiresAt>rule.CreatedAt.AddDays(days)) reasons.Add("Assessment freshness invalid");
            if(assessment.EvidenceIds.Count==0 || assessment.EvidenceIds.Any(id=>!byId.ContainsKey(id))) reasons.Add("Assessment evidence unavailable");
            if(assessment.Value=="KnownThreat" && (!fields.Contains("hash") || !assessment.EvidenceIds.Any(id=>
                byId.TryGetValue(id,out var e) && e.SourceUrl is not null && e.Kind=="ThreatIntelligence" &&
                e.Facts.TryGetValue("threatHash",out var hash) && rule.Predicates.Any(p=>p.Field=="hash"&&p.Value==hash))))
                reasons.Add("Known threat needs attributable exact-hash evidence");
        }
        var required=rule.EvidenceIds.Concat(rule.Predicates.SelectMany(p=>p.EvidenceIds))
            .Concat(rule.Assessments.SelectMany(a=>a.EvidenceIds));
        if(required.Any(id=>!byId.TryGetValue(id,out var e) || e.ExpiresAt<=now)) reasons.Add("Required evidence expired or absent");
        return new(reasons.Count==0,reasons);
    }
}

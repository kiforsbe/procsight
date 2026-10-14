using ProcSight.Core.Models;
using ProcSight.Core.Rules;
using ProcSight.Core.Privacy;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ProcSight.Infrastructure.Providers;
public static class ProviderResponseValidator
{
    private sealed record WireAssessment(AssessmentAxis Axis,string Value,string Reason,Confidence Confidence,List<string> EvidenceIds);
    private sealed record WireRule(List<Predicate> Predicates,List<int> AssessmentIndexes);
    private sealed record WireDocument(List<WireAssessment> Assessments,List<WireRule> Rules,string? IdentityLabel=null,string? Role=null);
    private static readonly JsonSerializerOptions Json=new() { PropertyNameCaseInsensitive=true,
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new JsonStringEnumConverter(allowIntegerValues:false)} };
    public static AnalysisProposal Parse(string json,AnalysisPayload payload,DateTimeOffset now)
    {
        try
        {
            var wire=JsonSerializer.Deserialize<WireDocument>(json,Json) ?? throw new InvalidDataException("Empty response");
            if(wire.Assessments is null || wire.Rules is null || wire.Assessments.Count>3 || wire.Rules.Count>32)
                throw new InvalidDataException("Response exceeds allowed structure");
            if(wire.IdentityLabel?.Length>120) throw new InvalidDataException("Identity label too long");
            var evidence=payload.Evidence.ToDictionary(e=>e.Id);
            var assessments=wire.Assessments.Select(a=>
            {
                if(!RuleValidator.Values.TryGetValue(a.Axis,out var values)||!values.Contains(a.Value)||a.Reason is null ||
                    a.EvidenceIds is null || a.EvidenceIds.Any(id=>!evidence.ContainsKey(id)) || (a.Value!="Unknown"&&a.EvidenceIds.Count==0))
                    throw new InvalidDataException("Assessment has invalid evidence or value");
                if(a.Value=="KnownThreat" && !a.EvidenceIds.Any(id=>evidence[id].Kind=="ThreatIntelligence" &&
                    evidence[id].SourceUrl is not null && evidence[id].Facts.GetValueOrDefault("threatHash")==payload.Metadata.GetValueOrDefault("hash")))
                    throw new InvalidDataException("Known threat lacks exact attributable evidence");
                return new Assessment(a.Axis,a.Value,a.Reason,a.Confidence,a.EvidenceIds,
                    now.AddDays(a.Axis==AssessmentAxis.Purpose?30:7),a.EvidenceIds.Select(id=>evidence[id].Origin).ToHashSet());
            }).ToList();
            if(assessments.Select(a=>a.Axis).Distinct().Count()!=assessments.Count) throw new InvalidDataException("Duplicate axes");
            var rules=wire.Rules.Select(r=>
            {
                if(r.Predicates is null || r.AssessmentIndexes is null || r.AssessmentIndexes.Any(i=>i<0||i>=assessments.Count))
                    throw new InvalidDataException("Invalid rule indexes");
                var outputs=r.AssessmentIndexes.Select(i=>assessments[i]).ToList();
                var expires=outputs.Count>0?outputs.Min(a=>a.ExpiresAt):now;
                return new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,now,expires,expires,
                    r.Predicates,outputs,outputs.SelectMany(a=>a.EvidenceIds).Concat(r.Predicates.SelectMany(p=>p.EvidenceIds)).Distinct().ToList(),
                    r.Predicates.Any(p=>p.Field=="hash")?100:r.Predicates.Count*10);
            }).ToList();
            var projected=OutputPrivacyFilter.Project(new(assessments,rules,wire.IdentityLabel,wire.Role),payload);
            return projected with { Rules=projected.Rules.Select(r=>r with { State=r.State==RuleState.Rejected ||
                !new RuleValidator().Validate(r,payload.Evidence,now).Accepted?RuleState.Rejected:RuleState.Active }).ToList() };
        }
        catch(Exception e) when(e is JsonException or ArgumentException or NullReferenceException)
        { throw new InvalidDataException("Provider returned malformed structured data.",e); }
    }
}

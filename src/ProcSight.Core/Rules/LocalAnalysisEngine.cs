using ProcSight.Core.Models;
namespace ProcSight.Core.Rules;
public sealed class LocalAnalysisEngine
{
    public LocalAnalysis Evaluate(ResolvedEntity entity,KnowledgeEntry knowledge,DateTimeOffset now)
    {
        var evidence=new Dictionary<string,EvidenceRecord>();
        foreach(var record in knowledge.Evidence) evidence[record.Id]=record;
        foreach(var record in entity.Evidence) evidence[record.Id]=record;
        var validator=new RuleValidator();
        var matches=knowledge.Rules.Where(r=>r.State==RuleState.Active && r.ReviewAt>now && r.ExpiresAt>now &&
            (r.Origin is not (DataOrigin.LocalCommandLine or DataOrigin.OnlineCommandLine) || entity.Origins.Contains(r.Origin)) &&
            r.Predicates.All(p=>entity.Facts.TryGetValue(p.Field,out var value) && PredicateMatcher.Matches(p,value)) &&
            validator.Validate(r,evidence,now).Accepted).ToList();
        var outputs=matches.SelectMany(r=>r.Assessments.Where(a=>a.ExpiresAt>now)).ToList();
        var dependencies=knowledge.RuleDependencies ?? [];
        bool SupportsCache(string id)
        {
            var record=knowledge.Evidence.FirstOrDefault(e=>e.Id==id);
            if(record is null || record.ExpiresAt<=now) return false;
            if(record.Kind is "LocalMetadata" or "ServiceMetadata")
                return record.Facts.All(f=>entity.Facts.TryGetValue(f.Key,out var current) &&
                    PredicateMatcher.Matches(new(PredicateKind.Exact,f.Key,f.Value,[]),current));
            return record.Origin is not (DataOrigin.LocalCommandLine or DataOrigin.OnlineCommandLine) || entity.Origins.Contains(record.Origin);
        }
        if(knowledge.Identity==entity.Identity && dependencies.All(id=>matches.Any(r=>r.Id==id)))
            outputs.AddRange(knowledge.Assessments.Where(a=>a.ExpiresAt>now && a.EvidenceIds.All(id=>
                SupportsCache(id))));
        var issues=new List<string>(); var assessments=new List<Assessment>(); var ruleSelections=new List<Assessment>();
        foreach(var axis in Enum.GetValues<AssessmentAxis>())
        {
            var eligible=outputs.Where(a=>a.Axis==axis).ToList();
            var correctionSelected=false;
            var corrections=knowledge.Corrections.Where(c=>c.Assessment.Axis==axis && c.Assessment.ExpiresAt>now).ToList();
            if(corrections.Count>0)
            {
                var correction=corrections.OrderByDescending(c=>c.Scope==CorrectionScope.InvocationContext?3:c.Scope==CorrectionScope.Executable?2:1).First().Assessment;
                if(axis==AssessmentAxis.Security && eligible.Any(a=>a.Value is "Suspicious" or "KnownThreat" && a.Value!=correction.Value))
                    issues.Add("User correction conflicts with independent security evidence");
                else { eligible=[correction]; correctionSelected=true; }
            }
            if(eligible.Select(a=>a.Value).Distinct().Count()>1) { issues.Add($"Conflicting {axis} assessments"); continue; }
            if(eligible.Count>0)
            {
                var selected=eligible.OrderByDescending(a=>a.Confidence).First();
                assessments.Add(selected);
                if(!correctionSelected) ruleSelections.Add(selected);
            }
        }
        var labelRule=matches.OrderByDescending(r=>r.Specificity).FirstOrDefault(r=>r.IdentityLabel is not null);
        // SQLite reconstructs the evidence/origin collections separately for entries and rules.
        // Record equality compares those collection references, losing older entry-only names.
        bool SameAssessment(Assessment cached,Assessment selected)=>cached.Axis==selected.Axis&&cached.Value==selected.Value&&
            cached.Reason==selected.Reason&&cached.Confidence==selected.Confidence&&cached.ExpiresAt==selected.ExpiresAt&&
            cached.EvidenceIds.SequenceEqual(selected.EvidenceIds)&&cached.Origins.SetEquals(selected.Origins);
        var cachedLabel=knowledge.Identity==entity.Identity && knowledge.Assessments.Any(cached=>cached.Value!="Unknown"&&
            cached.EvidenceIds.Count>0&&outputs.Any(a=>ReferenceEquals(a,cached))&&assessments.Any(selected=>SameAssessment(cached,selected))) &&
            dependencies.All(id=>matches.Any(r=>r.Id==id))?knowledge.IdentityLabel:null;
        // Structural equality can also match a correction or cached copy. Only the selected
        // assessment instance owned by a rule establishes that rule's contribution.
        var applied=matches.Where(r=>r.Assessments.Any(a=>ruleSelections.Any(selected=>ReferenceEquals(a,selected))))
            .Select(r=>r.Id).Distinct().ToList();
        return new(assessments,entity.Owner,matches.Select(r=>r.Id).ToList(),issues,
            Enum.GetValues<AssessmentAxis>().Where(axis=>!assessments.Any(a=>a.Axis==axis && a.Value!="Unknown")).ToHashSet(),
            labelRule?.IdentityLabel ?? cachedLabel,labelRule?.Role ?? (cachedLabel is not null?knowledge.Role:null),applied);
    }
}

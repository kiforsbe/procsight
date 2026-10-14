using ProcSight.Core.Models;
using ProcSight.Core.Rules;
namespace ProcSight.Application.Presentation;
public static class RuleInspection
{
    private static string Words(string text)=>System.Text.RegularExpressions.Regex.Replace(text,"(?<=[a-z])(?=[A-Z])"," ");
    public static string Field(string field)=>field switch {
        "hash"=>"File hash","name"=>"Executable","product"=>"Product","signer"=>"Verified signer","path"=>"File location",
        "owner"=>"Application owner","ownerIdentity"=>"Application scope","service"=>"Service","package"=>"Package identity",
        "commandContext"=>"Invocation context","commandRole"=>"Invocation category",_=>Words(field) };
    private static string Value(string field,string value)=>field is "commandContext" or "ownerIdentity"?"Private matching fingerprint":value;
    public static string Source(RuleDefinition rule)=>rule.Provenance is { } p?
        $"AI-generated · {p.ProviderId switch { "chatgpt"=>"ChatGPT plan","openai"=>"OpenAI API","compatible"=>"Compatible API","local"=>"Local model",_=>p.ProviderId }} · {p.Model}":
        rule.Origin==DataOrigin.UserCorrection?"User correction":"Learned rule · provider/model not recorded";
    public static string Title(RuleDefinition rule)=>
        (rule.Predicates.FirstOrDefault(p=>p.Field=="product")?.Value??rule.Predicates.FirstOrDefault(p=>p.Field=="name")?.Value??"Rule "+rule.Id.ToString()[..8])+
        " · "+string.Join(" / ",rule.Assessments.Select(a=>Words(a.Value)));
    public static string State(RuleDefinition rule,DateTimeOffset now)=>rule.State!=RuleState.Active?rule.State.ToString():
        rule.ExpiresAt<=now?"Expired":rule.ReviewAt<=now?"Review overdue":"Active";
    public static string ApplicationStatus(Guid id,LocalAnalysis? analysis)=>analysis?.AppliedRuleIds?.Contains(id)==true?"Applied":
        analysis?.MatchingRuleIds.Contains(id)==true?analysis.AppliedRuleIds is null?"Matched; contribution not recorded":"Matched, not contributing":"Not matched";
    public static string Describe(RuleDefinition rule,IReadOnlyList<EvidenceRecord> evidence,DateTimeOffset now,
        ResolvedEntity? entity=null,LocalAnalysis? analysis=null)
    {
        var lines=new List<string> { Title(rule),Source(rule),"State: "+State(rule,now),"Rule ID: "+rule.Id,
            $"Created: {rule.CreatedAt.LocalDateTime:g}",$"Review: {rule.ReviewAt.LocalDateTime:g}",$"Expires: {rule.ExpiresAt.LocalDateTime:g}" };
        if(rule.Provenance is { } p) lines.Add($"Generated: {p.GeneratedAt.LocalDateTime:g} · {(p.Destination==AnalysisDestination.LocalModel?"Local model":"Online provider")}");
        if(entity is not null) lines.Add("For selected process/service: "+ApplicationStatus(rule.Id,analysis));
        lines.Add("\nConditions — all must match");
        foreach(var predicate in rule.Predicates) {
            lines.Add($"{Field(predicate.Field)} {(predicate.Kind==PredicateKind.PathRoot?"under location":"equals")} {Value(predicate.Field,predicate.Value)}");
            if(entity is not null) lines.Add(entity.Facts.TryGetValue(predicate.Field,out var actual)?
                $"Observed: {Value(predicate.Field,actual)} · {(PredicateMatcher.Matches(predicate,actual)?"matches":"does not match")}":"Observed: unavailable");
            lines.Add($"Supporting evidence: {predicate.EvidenceIds.Count} record(s)");
        }
        lines.Add("\nClassifications supplied by this rule");
        foreach(var a in rule.Assessments) lines.Add($"{a.Axis}: {Words(a.Value)} ({a.Confidence})\n{a.Reason}\nReview by {a.ExpiresAt.LocalDateTime:g}\n");
        if(rule.IdentityLabel is { } label) lines.Add("Suggested identity: "+label);
        if(rule.Role is { } role) lines.Add("Suggested role: "+role);
        var validation=new RuleValidator().Validate(rule,evidence,now);
        lines.Add("\nEvidence validation: "+(validation.Accepted?"Supported by current stored evidence":string.Join("; ",validation.Reasons.Distinct())));
        lines.Add("Data origin: "+Words(rule.Origin.ToString())+" (privacy origin, separate from the AI provider)");
        lines.Add("A matching rule may be superseded by a user correction or conflict. Applied means its classification contributed to the current result.");
        return string.Join("\n",lines);
    }
    public static string Describe(KnownTaskDefinition definition)=>$"{definition.Label}\nBundled definition · {definition.Id}\n"+
        $"Executable: {definition.Executable}\nService: {definition.ServiceName??"Process definition"}\nProduct: {definition.Product??"Windows component / publisher gate"}\n"+
        $"Original file: {definition.OriginalName??definition.Executable}\nRequires verified signer: {string.Join(", ",definition.Signers)}\n"+
        (definition.PathScopes is { } scopes?"Allowed locations: "+string.Join(", ",scopes)+"\n":"")+
        (definition.PublisherOnly?"Publisher reference only; purpose remains unknown.":"Purpose: "+Words(definition.Purpose)+". Security and optionality remain unresolved.")+"\nReference: "+definition.SourceUrl;
    public static string Describe(EvidenceRecord record)=>$"{Words(record.Kind)} · {Words(record.Origin.ToString())}\nObserved: {record.ObservedAt.LocalDateTime:g}\n"+
        $"Expires: {(record.ExpiresAt is { } expires?expires.LocalDateTime.ToString("g"):"No fixed expiry; current matching still required")}\n\n"+
        string.Join("\n",record.Facts.Select(f=>Field(f.Key)+": "+Value(f.Key,f.Value)))+
        (record.SourceUrl is { } source?"\n\nReference: "+source:"");
}

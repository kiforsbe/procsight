using ProcSight.Core.Models;
using ProcSight.Core.Rules;
namespace ProcSight.Application.Presentation;
public static class RuleInspection
{
    public static string Research(ResearchOutcome? outcome)=>outcome switch {
        ResearchOutcome.Disabled=>"Web research was not run: disabled for this analysis.",
        ResearchOutcome.LocalAnalysis=>"Web research was not run: local analysis.",
        ResearchOutcome.Unavailable=>"Web research was not run: no enabled supported research connection was available.",
        ResearchOutcome.SourcesFound=>"Web research returned attributable sources for this analysis.",
        ResearchOutcome.NoSources=>"Web research completed but returned no attributable sources.",
        ResearchOutcome.Failed=>"Web research failed. No successful research result was available; see AI activity for the failed request.",
        ResearchOutcome.Searching=>"Web search is enabled within this analysis request; waiting for the completed result.",
        _=>"Web research history was not recorded for this analysis." };
    public static string Rejection(RuleDefinition rule,IReadOnlyList<EvidenceRecord> evidence,DateTimeOffset now)
    {
        var recorded=rule.RejectionReasons is { Count:>0 } reasons?string.Join(" ",reasons.Distinct().Select(RejectionText)):
            "The original rejection reason was not recorded for this older rule.";
        var validation=new RuleValidator().Validate(rule,evidence,now);
        return recorded+"\nCurrent evidence: "+(validation.Accepted?"passes validation; this does not undo the original rejection.":string.Join("; ",validation.Reasons.Distinct()))+
            "\nAnalyze again to generate a fresh rule. Rejected rules do not contribute assessments and cannot be enabled.";
    }
    private static string RejectionText(RuleRejectionReason reason)=>reason switch {
        RuleRejectionReason.PrivatePredicateOmitted=>"A proposed condition was not supported by submitted metadata. It was omitted to prevent private command text entering stored rules.",
        RuleRejectionReason.InvalidStructure=>"The proposed rule had an invalid structure.",
        RuleRejectionReason.MissingInvocationScope=>"The command-derived rule lacked a private invocation scope.",
        RuleRejectionReason.InsufficientIdentity=>"The proposed identity lacked a file hash or independent identifying fields.",
        RuleRejectionReason.InvalidPredicate=>"A proposed matching condition was invalid.",
        RuleRejectionReason.UnsupportedPredicate=>"A matching condition lacked supporting evidence.",
        RuleRejectionReason.InvalidAssessment=>"A proposed classification was invalid.",
        RuleRejectionReason.InvalidFreshness=>"A classification's review period was expired or outside its permitted limit.",
        RuleRejectionReason.MissingAssessmentEvidence=>"A classification referenced unavailable evidence.",
        RuleRejectionReason.UnsupportedThreat=>"The threat claim lacked attributable evidence for this exact file hash.",
        RuleRejectionReason.MissingEvidence=>"Required evidence was absent or expired at validation.",
        _=>"The proposal was rejected; a specific reason was unavailable." };
    private static string Words(string text)=>System.Text.RegularExpressions.Regex.Replace(text,"(?<=[a-z])(?=[A-Z])"," ");
    public static string Field(string field)=>field switch {
        "hash"=>"File hash","name"=>"Executable","product"=>"Product","signer"=>"Verified signer","path"=>"File location",
        "owner"=>"Application owner","ownerIdentity"=>"Application scope","service"=>"Service","package"=>"Package identity",
        "commandContext"=>"Invocation context","commandRole"=>"Invocation category",_=>Words(field) };
    public static string Value(string field,string value)=>field is "commandContext" or "ownerIdentity"?"Private matching fingerprint":value;
    public static string Name(RuleDefinition rule,IReadOnlyList<EvidenceRecord>? evidence=null)
    {
        foreach(var field in new[]{"product","service","name"})
            if(rule.Predicates.FirstOrDefault(p=>p.Field==field)?.Value is { Length:>0 } value) return value;
        var hash=rule.Predicates.FirstOrDefault(p=>p.Field=="hash")?.Value;
        if(hash is not null) {
            var record=evidence?.FirstOrDefault(e=>rule.EvidenceIds.Concat(rule.Predicates.SelectMany(p=>p.EvidenceIds)).Contains(e.Id)&&e.Facts.GetValueOrDefault("hash")==hash);
            foreach(var field in new[]{"product","name"})
                if(record?.Facts.GetValueOrDefault(field) is { Length:>0 } value) return value;
            return "File-specific rule";
        }
        return "Scoped rule";
    }
    public static string Classifications(RuleDefinition rule)=>string.Join(" / ",rule.Assessments.Select(a=>Words(a.Value)));
    public static string Locations(KnownTaskDefinition definition)=>definition.PublisherOnly?"Publisher reference; location gate does not apply":
        definition.WindowsComponent?string.Join(", ",definition.PathScopes??["System32","SysWOW64","SystemApps"]):"No additional location constraint";
    public static string Source(RuleDefinition rule)=>rule.Provenance is { } p?
        $"AI-generated · {p.ProviderId switch { "chatgpt"=>"ChatGPT plan","openai"=>"OpenAI API","compatible"=>"Compatible API","local"=>"Local model",_=>p.ProviderId }} · {p.Model}":
        rule.Origin==DataOrigin.UserCorrection?"User correction":"Learned rule · provider/model not recorded";
    public static string Title(RuleDefinition rule,IReadOnlyList<EvidenceRecord>? evidence=null)=>Name(rule,evidence)+" · "+Classifications(rule);
    public static string State(RuleDefinition rule,DateTimeOffset now)=>rule.State!=RuleState.Active?rule.State.ToString():
        rule.ExpiresAt<=now?"Expired":rule.ReviewAt<=now?"Review overdue":"Active";
    public static string ApplicationStatus(Guid id,LocalAnalysis? analysis)=>analysis?.AppliedRuleIds?.Contains(id)==true?"Applied":
        analysis?.MatchingRuleIds.Contains(id)==true?analysis.AppliedRuleIds is null?"Matched; contribution not recorded":"Matched, not contributing":"Not matched";
    public static string Describe(RuleDefinition rule,IReadOnlyList<EvidenceRecord> evidence,DateTimeOffset now,
        ResolvedEntity? entity=null,LocalAnalysis? analysis=null)
    {
        var lines=new List<string> { Title(rule,evidence),Source(rule),"State: "+State(rule,now),"Rule ID: "+rule.Id,
            $"Created: {rule.CreatedAt.LocalDateTime:g}",$"Review: {rule.ReviewAt.LocalDateTime:g}",$"Expires: {rule.ExpiresAt.LocalDateTime:g}" };
        if(rule.Provenance is { } p) lines.Add($"Generated: {p.GeneratedAt.LocalDateTime:g} · {(p.Destination==AnalysisDestination.LocalModel?"Local model":"Online provider")}");
        if(rule.State==RuleState.Rejected) lines.Add(Rejection(rule,evidence,now));
        lines.Add(Research(rule.Provenance?.Research));
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

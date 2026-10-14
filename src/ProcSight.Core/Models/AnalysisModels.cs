namespace ProcSight.Core.Models;
public enum DataOrigin { ShareableMetadata, LocalCommandLine, OnlineCommandLine, Research, UserCorrection }
public enum AssessmentAxis { Purpose, Security, Optionality }
public enum Confidence { Low, Medium, High }
public enum AnalysisState { Local, Queued, Analyzing, Unknown, Stale, BudgetPaused, Failed }
public sealed record EvidenceRecord(string Id, DataOrigin Origin, DateTimeOffset ObservedAt,
    DateTimeOffset? ExpiresAt, string Kind, Dictionary<string, string> Facts, string? SourceUrl = null);
public sealed record Assessment(AssessmentAxis Axis, string Value, string Reason,
    Confidence Confidence, List<string> EvidenceIds, DateTimeOffset ExpiresAt,
    HashSet<DataOrigin> Origins);
public sealed record LocalAnalysis(List<Assessment> Assessments, string? Owner,
    List<Guid> MatchingRuleIds, List<string> Issues, HashSet<AssessmentAxis> UnresolvedAxes,
    string? IdentityLabel = null,string? Role = null);
public sealed record AnalysisUpdate(AnalysisIdentity Identity, AnalysisState State,
    LocalAnalysis Result, string? Error = null);
public enum AnalysisDestination { LocalRules, LocalModel, CloudModel }
public sealed record AnalysisPayload(AnalysisIdentity Identity, Dictionary<string, string> Metadata,
    List<EvidenceRecord> Evidence, string? CommandLine, long PolicyRevision,
    AnalysisDestination Destination);
public sealed record AnalysisProposal(List<Assessment> Assessments, List<RuleDefinition> Rules,
    string? IdentityLabel = null,string? Role = null);
public sealed record ResearchRequest(Dictionary<string, string> IdentityTerms);
public sealed record ProviderCapabilities(bool StructuredOutput, bool Research);
public enum ProviderErrorKind { Auth, RateLimited, Transient, Unsupported, Malformed, Refused, Cancelled }
public sealed record ProviderError(ProviderErrorKind Kind, string Message, TimeSpan? RetryAfter = null);
public sealed record ProviderResult(AnalysisProposal? Proposal, ProviderError? Error = null,
    int BillableCalls = 1, int ToolUses = 0);
public sealed record ResearchResult(List<EvidenceRecord> Evidence, ProviderError? Error = null,
    int BillableCalls = 1, int ToolUses = 0);

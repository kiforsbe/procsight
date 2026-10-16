namespace ProcSight.Core.Models;
public enum RuleState { Active, Disabled, Rejected }
public enum PredicateKind { Exact, PathRoot }
public sealed record Predicate(PredicateKind Kind, string Field, string Value, List<string> EvidenceIds);
public enum ResearchOutcome { Disabled, LocalAnalysis, Unavailable, SourcesFound, NoSources, Failed, Searching }
public enum RuleRejectionReason { PrivatePredicateOmitted, InvalidStructure, MissingInvocationScope, InsufficientIdentity,
    InvalidPredicate, UnsupportedPredicate, InvalidAssessment, InvalidFreshness, MissingAssessmentEvidence, UnsupportedThreat, MissingEvidence, Unspecified }
public sealed record RuleProvenance(string ProviderId,string Model,AnalysisDestination Destination,DateTimeOffset GeneratedAt,
    ResearchOutcome? Research = null);
public sealed record RuleDefinition(Guid Id, int Version, RuleState State, DataOrigin Origin,
    DateTimeOffset CreatedAt, DateTimeOffset ReviewAt, DateTimeOffset ExpiresAt,
    List<Predicate> Predicates, List<Assessment> Assessments, List<string> EvidenceIds,
    int Specificity, Guid? SupersededId = null,string? IdentityLabel = null,string? Role = null,
    RuleProvenance? Provenance = null,List<RuleRejectionReason>? RejectionReasons = null);
public sealed record RuleValidationResult(bool Accepted, List<string> Reasons);
public sealed record EvidenceChange(HashSet<string> EvidenceIds, Dictionary<string, string> OldFacts,
    Dictionary<string, string> NewFacts);

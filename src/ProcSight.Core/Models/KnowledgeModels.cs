namespace ProcSight.Core.Models;
public enum ClearScope { GeneratedKnowledge, UserCorrections }
public enum CorrectionScope { Executable, ApplicationFamily, InvocationContext }
public sealed record UserCorrection(Guid Id, CorrectionScope Scope, string ScopeValue, Assessment Assessment);
public sealed record KnowledgeEntry(AnalysisIdentity Identity, DateTimeOffset UpdatedAt,
    List<Assessment> Assessments, List<EvidenceRecord> Evidence, List<RuleDefinition> Rules,
    List<UserCorrection> Corrections)
{
    public static KnowledgeEntry Empty(AnalysisIdentity id) => new(id, DateTimeOffset.MinValue, [], [], [], []);
}

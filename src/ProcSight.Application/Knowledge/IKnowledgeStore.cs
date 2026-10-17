using ProcSight.Core.Models;
namespace ProcSight.Application.Knowledge;
public interface IKnowledgeStore
{
    Task<KnowledgeEntry> LoadAsync(AnalysisIdentity id,CancellationToken ct);
    Task<KnowledgeEntry> ReadCandidatesAsync(ResolvedEntity entity,CancellationToken ct);
    async Task<IReadOnlyDictionary<AnalysisIdentity,KnowledgeEntry>> ReadCandidateBatchAsync(IReadOnlyList<ResolvedEntity> entities,CancellationToken ct) {
        var result=new Dictionary<AnalysisIdentity,KnowledgeEntry>();
        foreach(var entity in entities) { ct.ThrowIfCancellationRequested(); result[entity.Identity]=await ReadCandidatesAsync(entity,ct); }
        return result;
    }
    Task SaveAsync(KnowledgeEntry entry,CancellationToken ct);
    Task SetRuleStateAsync(Guid id,RuleState state,CancellationToken ct);
    Task ClearAsync(ClearScope scope,CancellationToken ct);
    Task PruneAsync(DateTimeOffset now,CancellationToken ct);
    Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken ct);
    Task DeleteRuleAsync(Guid id,CancellationToken ct);
}

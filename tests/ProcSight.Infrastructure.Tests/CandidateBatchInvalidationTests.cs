using ProcSight.Application.Analysis;
using ProcSight.Application.Knowledge;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class CandidateBatchInvalidationTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task InvalidationDuringBatchReadCannotRestoreAnOldFingerprint(bool retry) {
        var folder=Path.Combine(Path.GetTempPath(),"procsight-batch-"+Guid.NewGuid());
        try {
            var sql=new SqliteKnowledgeStore(Path.Combine(folder,"test.db")); var store=new HeldBatch(sql);
            await using var coordinator=new AnalysisCoordinator(store,sql,(_,_)=>throw new InvalidOperationException("No AI allowed"));
            var snapshot=Samples.Snapshot(Samples.Process()); var policy=new AnalysisPolicy();
            var first=coordinator.ObserveAsync(snapshot,policy,default);
            await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if(retry) coordinator.RetryConnection("synthetic"); else coordinator.InvalidateKnowledge();
            store.Release.SetResult(); await first;
            await coordinator.ObserveAsync(snapshot,policy,default);
            Assert.Equal(2,store.Reads);
        } finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
    private sealed class HeldBatch(SqliteKnowledgeStore inner) : IKnowledgeStore {
        public int Reads; public TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyDictionary<AnalysisIdentity,KnowledgeEntry>> ReadCandidateBatchAsync(IReadOnlyList<ResolvedEntity> entities,CancellationToken ct) {
            Reads++; var result=await inner.ReadCandidateBatchAsync(entities,ct);
            if(Reads==1) { Entered.SetResult(); await Release.Task.WaitAsync(ct); } return result;
        }
        public Task<KnowledgeEntry> LoadAsync(AnalysisIdentity id,CancellationToken ct)=>inner.LoadAsync(id,ct);
        public Task<KnowledgeEntry> ReadCandidatesAsync(ResolvedEntity e,CancellationToken ct)=>inner.ReadCandidatesAsync(e,ct);
        public Task SaveAsync(KnowledgeEntry e,CancellationToken ct)=>inner.SaveAsync(e,ct);
        public Task SetRuleStateAsync(Guid id,RuleState state,CancellationToken ct)=>inner.SetRuleStateAsync(id,state,ct);
        public Task ClearAsync(ClearScope scope,CancellationToken ct)=>inner.ClearAsync(scope,ct);
        public Task PruneAsync(DateTimeOffset now,CancellationToken ct)=>inner.PruneAsync(now,ct);
        public Task<IReadOnlyList<RuleDefinition>> GetRulesAsync(CancellationToken ct)=>inner.GetRulesAsync(ct);
        public Task DeleteRuleAsync(Guid id,CancellationToken ct)=>inner.DeleteRuleAsync(id,ct);
    }
}

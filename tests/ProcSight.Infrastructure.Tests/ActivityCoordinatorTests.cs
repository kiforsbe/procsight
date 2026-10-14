using ProcSight.Application.Analysis;
using ProcSight.Application.Research;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class ActivityCoordinatorTests : IDisposable
{
    private readonly string folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
    private static AnalysisPolicy Policy()=>new(true,Provider:new("fixture",new("https://example.com"),"model"));
    private AnalysisCoordinator Coordinator(CountingProvider provider,AnalysisActivityLog log,IResearchProvider? research=null)
    {
        var store=new SqliteKnowledgeStore(Path.Combine(folder,"knowledge.db"));
        return new(store,store,(_,_)=>provider,research:_=>research,activity:log);
    }
    [Fact] public async Task EveryRetryRecordsDistinctCompletedAttemptAndUnchangedSnapshotAddsNothing()
    {
        var log=new AnalysisActivityLog(); var provider=new CountingProvider { Kind=ProviderErrorKind.Transient };
        await using var coordinator=Coordinator(provider,log);
        var snapshot=Samples.Snapshot(Samples.Process(42));
        await coordinator.ObserveAsync(snapshot,Policy(),default); await coordinator.WaitForIdleAsync(default);
        var rows=log.Snapshot(); Assert.Equal(3,rows.Count);
        Assert.Equal(new[]{3,2,1},rows.Select(r=>r.Attempt));
        Assert.All(rows,r=> { Assert.Equal(42,r.ProcessId); Assert.Equal(AnalysisActivityStatus.Failed,r.Status); Assert.NotNull(r.Elapsed); });
        await coordinator.ObserveAsync(snapshot,Policy(),default); await coordinator.WaitForIdleAsync(default);
        Assert.Equal(3,log.Snapshot().Count);
    }
    [Fact] public async Task PolicyCancellationCompletesRunningEntryAndObserversCannotFailProviderWork()
    {
        var log=new AnalysisActivityLog(); log.Changed+=(_,_)=>throw new Exception("observer");
        var provider=new CountingProvider { Hold=new() };
        await using var coordinator=Coordinator(provider,log);
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process()),Policy() with {Revision=1},default);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(AnalysisActivityStatus.Running,Assert.Single(log.Snapshot()).Status);
        await coordinator.ChangePolicyAsync(new(Revision:2),default); await coordinator.WaitForIdleAsync(default);
        Assert.Equal(AnalysisActivityStatus.Cancelled,Assert.Single(log.Snapshot()).Status);
    }
    private sealed class FixtureResearch : IResearchProvider
    {
        public Task<ResearchResult> ResearchAsync(ResearchRequest request,CancellationToken ct)=>Task.FromResult(new ResearchResult([
            new("fixture",DataOrigin.Research,Samples.Now,null,"snippet",new(){["text"]="PRIVATE_SNIPPET"})]));
    }
    [Fact] public async Task ResearchRecordsOnlyEvidenceCount()
    {
        var log=new AnalysisActivityLog(); await using var coordinator=Coordinator(new(),log,new FixtureResearch());
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process()),Policy() with {ResearchEnabled=true},default);
        await coordinator.WaitForIdleAsync(default);
        var row=Assert.Single(log.Snapshot(),r=>r.Kind==AnalysisActivityKind.Research);
        Assert.Equal(1,row.ReturnedEvidenceCount); Assert.Equal(AnalysisActivityStatus.Succeeded,row.Status);
        Assert.DoesNotContain("PRIVATE_SNIPPET",System.Text.Json.JsonSerializer.Serialize(log.Snapshot()));
    }
    public void Dispose()
    { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if(Directory.Exists(folder)) Directory.Delete(folder,true); }
}

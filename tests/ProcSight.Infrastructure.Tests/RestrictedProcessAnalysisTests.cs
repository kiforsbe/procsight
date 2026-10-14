using ProcSight.Application.Analysis;
using ProcSight.Application.Providers;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class RestrictedProcessAnalysisTests
{
    private sealed class WaitingProvider : IAnalysisProvider
    {
        public TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct)=>Task.FromResult(new ProviderCapabilities(true,false));
        public async Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct)
        {
            Calls++; Entered.TrySetResult(); await Release.Task.WaitAsync(ct);
            var evidence=payload.Evidence.First().Id;
            return new(new(Enum.GetValues<AssessmentAxis>().Select(axis=>new Assessment(axis,"Unknown","Only limited metadata is available.",Confidence.Low,[evidence],DateTimeOffset.UtcNow.AddDays(1),[DataOrigin.ShareableMetadata])).ToList(),[]));
        }
    }
    [Fact] public async Task LimitedManualAnalysisSurvivesCollectionWithSameLifetimeScope()
    {
        var db=Path.Combine(Path.GetTempPath(),"procsight-restricted-test-"+Guid.NewGuid()+".db");
        try {
            var store=new SqliteKnowledgeStore(db); var provider=new WaitingProvider();
            var policy=new AnalysisPolicy(true,Provider:new("fixture",new("https://example.com"),"fixture"));
            await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider);
            var process=Samples.Process() with { Key=new(42,null,Guid.NewGuid()),Executable=new("restricted.exe"),CommandLine=null,ServiceNames=[] };
            var first=Samples.Snapshot(process); var identity=Assert.Single(coordinator.ResolveEntities(first,policy)).Identity;
            await coordinator.ObserveAsync(first,policy,default); Assert.Equal(0,provider.Calls);
            await coordinator.RequestReanalysisAsync(identity,default);
            await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await coordinator.ObserveAsync(Samples.Snapshot(process),policy,default);
            provider.Release.TrySetResult(); await coordinator.WaitForIdleAsync(default);
            Assert.Equal(1,provider.Calls); Assert.Equal(3,(await store.LoadAsync(identity,default)).Assessments.Count);
        } finally {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach(var file in new[]{db,db+"-wal",db+"-shm"}) if(File.Exists(file)) File.Delete(file);
        }
    }
}

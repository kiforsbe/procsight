using ProcSight.Application.Analysis;
using ProcSight.Application.Providers;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class ExplainingProvider : IAnalysisProvider
{
    public int Calls;
    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct)=>Task.FromResult(new ProviderCapabilities(true,false));
    public Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct)
    {
        Calls++; var now=DateTimeOffset.UtcNow; var id=payload.Evidence.First(e=>e.Facts.ContainsKey("hash")).Id;
        var outputs=new List<Assessment> {
            new(AssessmentAxis.Purpose,"DeveloperTool","Synthetic fixture",Confidence.Medium,[id],now.AddDays(30),[DataOrigin.ShareableMetadata]),
            new(AssessmentAxis.Security,"NoConcernObserved","Synthetic fixture",Confidence.Low,[id],now.AddDays(7),[DataOrigin.ShareableMetadata]),
            new(AssessmentAxis.Optionality,"Optional","Synthetic fixture",Confidence.Medium,[id],now.AddDays(7),[DataOrigin.ShareableMetadata]) };
        var rule=new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,now,now.AddDays(7),now.AddDays(7),
            [new(PredicateKind.Exact,"hash",payload.Metadata["hash"],[id]),..
                (payload.Metadata.TryGetValue("path",out var path)?new[]{new ProcSight.Core.Models.Predicate(PredicateKind.Exact,"path",path,[id])}:Array.Empty<ProcSight.Core.Models.Predicate>())],
            outputs,[id],100,IdentityLabel:"Example developer tool",Role:"Worker");
        return Task.FromResult(new ProviderResult(new(outputs,[rule],"Example developer tool","Worker")));
    }
}
public sealed class EndToEndTests : IDisposable
{
    private readonly string folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
    private readonly ExplainingProvider provider=new();
    private SqliteKnowledgeStore Store()=>new(Path.Combine(folder,"knowledge.db"));
    private static AnalysisPolicy Policy()=>new(true,Provider:new("fixture",new("https://example.com/v1"),"fixture"));
    [Fact] public async Task FirstUnknownThenStoredRuleThenZeroCallsAfterRestart()
    {
        var store=Store();
        await using(var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider))
        {
            await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process()),Policy(),default);
            await coordinator.WaitForIdleAsync(default); Assert.Equal(1,provider.Calls);
            Assert.Single(await store.GetRulesAsync(default));
        }
        provider.Calls=0; var restarted=Store();
        await using(var coordinator=new AnalysisCoordinator(restarted,restarted,(_,_)=>provider))
        {
            await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process(99)),Policy(),default);
            await coordinator.WaitForIdleAsync(default); Assert.Equal(0,provider.Calls);
        }
    }
    [Fact] public async Task ChangedOwnerForcesFreshAnalysis()
    {
        var store=Store(); await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider);
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process()),Policy(),default);
        await coordinator.WaitForIdleAsync(default);
        var changed=Samples.Process() with { Executable=Samples.Process().Executable with { FullPath=@"C:\Different\node.exe" } };
        await coordinator.ObserveAsync(Samples.Snapshot(changed),Policy(),default);
        await coordinator.WaitForIdleAsync(default); Assert.Equal(2,provider.Calls);
    }
    [Fact] public async Task CommandDerivedRulesCannotTransferBetweenDifferentScripts()
    {
        var store=Store(); await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider);
        var policy=new AnalysisPolicy(true,LocalModel:new("local",new("http://127.0.0.1:1234/v1"),"fixture"));
        var first=Samples.Process(name:"python.exe",product:"Python") with { CommandLine=new("python.exe first.py --token PRIVATE",Samples.Now) };
        await coordinator.ObserveAsync(Samples.Snapshot(first),policy,default); await coordinator.WaitForIdleAsync(default);
        Assert.Single(await store.GetRulesAsync(default));
        var second=first with { CommandLine=new("python.exe second.py --token PRIVATE",Samples.Now) };
        await coordinator.ObserveAsync(Samples.Snapshot(second),policy,default); await coordinator.WaitForIdleAsync(default);
        Assert.Equal(2,provider.Calls);
        Assert.DoesNotContain("PRIVATE",System.Text.Json.JsonSerializer.Serialize(await store.GetRulesAsync(default)));
    }
    [Fact] public async Task DisabledRuleStopsCachedClassification()
    {
        var store=Store(); await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider);
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process()),Policy(),default); await coordinator.WaitForIdleAsync(default);
        var rule=Assert.Single(await store.GetRulesAsync(default));
        await store.SetRuleStateAsync(rule.Id,RuleState.Disabled,default); coordinator.InvalidateKnowledge();
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process()),Policy(),default); await coordinator.WaitForIdleAsync(default);
        Assert.Equal(2,provider.Calls);
    }
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    [Fact] public async Task UnchangedLiveProcessReanalyzesWhenKnowledgeExpires()
    {
        var clock=new ManualClock(); var store=Store();
        await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider,clock:clock);
        var snapshot=Samples.Snapshot(Samples.Process());
        await coordinator.ObserveAsync(snapshot,Policy(),default); await coordinator.WaitForIdleAsync(default);
        Assert.Equal(1,provider.Calls);
        clock.Now=clock.Now.AddDays(8);
        await coordinator.ObserveAsync(snapshot,Policy(),default); await coordinator.WaitForIdleAsync(default);
        Assert.Equal(2,provider.Calls);
    }
    public void Dispose()
    { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if(Directory.Exists(folder)) Directory.Delete(folder,true); }
}

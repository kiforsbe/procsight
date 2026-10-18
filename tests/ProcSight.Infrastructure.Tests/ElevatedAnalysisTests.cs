using ProcSight.Application.Analysis;
using ProcSight.Application.Settings;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class ElevatedAnalysisTests
{
    [Fact] public async Task ElevatedSessionBlocksAutomaticManualFallbackAndExplicitOnlineTest()
    {
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
        try {
            var provider=new CountingProvider(); var store=new SqliteKnowledgeStore(Path.Combine(folder,"knowledge.db"));
            var profile=new ConnectionProfile("cloud",new("openai",new("https://api.openai.com/v1"),"test"),true,0);
            var saved=new AnalysisPolicy(true,true,CommandLineMode.OnlineFull,Connections:[profile],FallbackEnabled:true);
            var effective=SessionPolicy.Effective(saved,true,new());
            await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider);
            var snapshot=Samples.Snapshot(Samples.Process());
            await coordinator.ObserveAsync(snapshot,effective,default); await coordinator.WaitForIdleAsync(default);
            await coordinator.RequestReanalysisAsync(coordinator.ResolveEntities(snapshot,effective)[0].Identity,default); await coordinator.WaitForIdleAsync(default);
            var result=await new ConnectionTester((_,_)=>provider,store,new()).TestAsync(profile,effective,default);
            Assert.NotNull(result.Error); Assert.Equal(0,provider.Calls);
            await coordinator.ChangePolicyAsync(SessionPolicy.Effective(saved with { Revision=1 },true,new(true)),default);
            await coordinator.ObserveAsync(snapshot,SessionPolicy.Effective(saved with { Revision=1 },true,new(true)),default);
            await coordinator.WaitForIdleAsync(default); Assert.Equal(1,provider.Calls);
        } finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
}

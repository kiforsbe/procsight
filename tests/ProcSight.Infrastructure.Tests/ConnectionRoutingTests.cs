using ProcSight.Application.Analysis;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class ConnectionRoutingTests
{
    private static ConnectionProfile P(string id,int priority=0,bool enabled=true,string kind="openai")=>new(id,new(kind,new(kind=="local"?"http://127.0.0.1:1234/v1":"https://api.openai.com/v1"),id),enabled,priority);
    [Fact] public void RoutingHonorsEnabledOrderAndLocalPreference()
    {
        var policy=new AnalysisPolicy(Connections:[P("disabled",0,false),P("cloud",1),P("local",2,kind:"local")]);
        Assert.Equal(new[]{"cloud","local"},ConnectionRouter.Resolve(policy).Select(p=>p.ProfileId));
        Assert.Equal("local",ConnectionRouter.Resolve(policy with { CommandLineMode=CommandLineMode.LocalOnly })[0].ProfileId);
        Assert.Empty(ConnectionRouter.Resolve(policy with { Connections=[] }));
    }
    [Theory] [InlineData(false,ProviderErrorKind.Auth,0)] [InlineData(true,ProviderErrorKind.Auth,1)]
    [InlineData(true,ProviderErrorKind.Malformed,0)] [InlineData(true,ProviderErrorKind.Refused,0)] [InlineData(true,ProviderErrorKind.Transient,1)]
    public async Task FallbackIsExplicitAndOnlyForEligibleFailure(bool fallback,ProviderErrorKind failure,int secondCalls)
    {
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
        try {
            var a=new CountingProvider { Kind=failure }; var b=new CountingProvider();
            var store=new SqliteKnowledgeStore(Path.Combine(folder,"knowledge.db"));
            await using var coordinator=new AnalysisCoordinator(store,store,(p,_)=>p.Model=="a"?a:b);
            var policy=new AnalysisPolicy(true,Connections:[P("a"),P("b",1)],FallbackEnabled:fallback);
            await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process()),policy,default);
            await coordinator.WaitForIdleAsync(default);
            Assert.Equal(secondCalls,b.Calls); Assert.Equal(failure==ProviderErrorKind.Transient?3:1,a.Calls);
        } finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
    [Fact] public async Task PausedPrimaryDoesNotPreventOptedInFallbackForNextJob()
    {
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
        try {
            var a=new CountingProvider { Kind=ProviderErrorKind.Auth }; var b=new CountingProvider();
            var store=new SqliteKnowledgeStore(Path.Combine(folder,"knowledge.db"));
            await using var coordinator=new AnalysisCoordinator(store,store,(p,_)=>p.Model=="a"?a:b);
            await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process(1,hash:"a"),Samples.Process(2,hash:"b")),
                new(true,Connections:[P("a"),P("b",1)],FallbackEnabled:true),default);
            await coordinator.WaitForIdleAsync(default); Assert.Equal(1,a.Calls); Assert.Equal(2,b.Calls);
        } finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
}

using ProcSight.Application.Analysis;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class ConnectionTesterTests
{
    private sealed class HeldLedger : ProcSight.Application.Budget.IUsageLedger
    {
        public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<bool> TryReserveAsync(UsageKind kind,DateOnly day,UsageLimits limits,CancellationToken ct) { Started.SetResult(); await Release.Task; return true; }
        public Task<(int,int)> GetUsageAsync(DateOnly day,CancellationToken ct)=>Task.FromResult((0,0));
    }
    [Fact] public async Task RevokedPolicyAfterBudgetWaitPreventsDispatch()
    {
        var ledger=new HeldLedger(); var provider=new CountingProvider(); var policy=new AnalysisPolicy();
        var profile=new ConnectionProfile("test",new("openai",new("https://api.openai.com/v1"),"test"),true,0);
        var test=new ConnectionTester((_,_)=>provider,ledger,new(),()=>policy).TestAsync(profile,policy,default);
        await ledger.Started.Task; policy=policy with { Revision=1,ExternalAnalysisAllowed=false }; ledger.Release.SetResult();
        var result=await test; Assert.Equal(0,provider.Calls); Assert.Equal(ProviderErrorKind.Cancelled,result.Error!.Kind);
    }
    [Fact] public async Task ExplicitTestUsesSelectedDisabledProfileAndOnlySyntheticMetadata()
    {
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
        try {
            var provider=new CountingProvider(); var store=new SqliteKnowledgeStore(Path.Combine(folder,"usage.db"));
            var log=new AnalysisActivityLog(); string? selected=null;
            var tester=new ConnectionTester((p,_)=> { selected=p.Model; return provider; },store,log);
            var profile=new ConnectionProfile("selected",new("openai",new("https://api.openai.com/v1"),"selected-model"),false,1);
            await tester.TestAsync(profile,new(FallbackEnabled:true),default);
            Assert.Equal("selected-model",selected); Assert.Equal(1,provider.Calls);
            var entry=Assert.Single(log.Snapshot()); Assert.Equal(AnalysisActivityKind.ConnectionTest,entry.Kind);
            Assert.False(entry.CommandIncluded); Assert.Equal("synthetic-connection-test.exe",entry.Metadata["name"]);
            Assert.Single(entry.Metadata);
        } finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
}

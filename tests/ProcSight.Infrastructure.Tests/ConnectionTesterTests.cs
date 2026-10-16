using ProcSight.Application.Analysis;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class ConnectionTesterTests
{
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

using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class KnowledgeStoreTests : IDisposable
{
    private readonly string folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
    private SqliteKnowledgeStore Store() => new(Path.Combine(folder,"knowledge.db"));
    [Fact] public async Task RestartPreservesCorrections()
    {
        var id=new AnalysisIdentity("fixture");
        var entry=KnowledgeEntry.Empty(id) with { UpdatedAt=Samples.Now,Corrections=[new(Guid.NewGuid(),CorrectionScope.InvocationContext,"fixture",
            new(AssessmentAxis.Purpose,"DeveloperTool","User correction",Confidence.High,[],Samples.Now.AddDays(30),[DataOrigin.UserCorrection]))] };
        await Store().SaveAsync(entry,default);
        Assert.Single((await Store().LoadAsync(id,default)).Corrections);
    }
    [Fact] public async Task ClearKnowledgeDoesNotResetUsage()
    {
        var store=Store(); var date=DateOnly.FromDateTime(Samples.Now.Date);
        Assert.True(await store.TryReserveAsync(UsageKind.BillableCall,date,new(BillableCalls:1),default));
        await store.ClearAsync(ClearScope.GeneratedKnowledge,default);
        Assert.False(await Store().TryReserveAsync(UsageKind.BillableCall,date,new(BillableCalls:1),default));
    }
    [Fact] public async Task ConcurrentSixtyFirstCallDenied()
    {
        var store=Store(); var date=DateOnly.FromDateTime(Samples.Now.Date);
        var results=await Task.WhenAll(Enumerable.Range(0,61).Select(_=>store.TryReserveAsync(UsageKind.BillableCall,date,new(),default)));
        Assert.Equal(60,results.Count(r=>r));
    }
    [Fact] public async Task RawCommandNeverStored()
    {
        var entry=KnowledgeEntry.Empty(new("fixture")) with { Evidence=[new("bad",DataOrigin.LocalCommandLine,Samples.Now,null,"command",
            new(){["commandLine"]="secret raw text"})] };
        await Assert.ThrowsAsync<InvalidDataException>(()=>Store().SaveAsync(entry,default));
    }
    [Fact] public async Task NinetyDayPruneKeepsActiveEvidence()
    {
        var store=Store(); var old=DateTimeOffset.UtcNow.AddDays(-100); var id=new AnalysisIdentity("expired-fixture");
        await store.SaveAsync(KnowledgeEntry.Empty(id) with { UpdatedAt=old,Evidence=[new("old",DataOrigin.ShareableMetadata,old,null,"LocalMetadata",new(){["name"]="old.exe"})] },default);
        await store.PruneAsync(DateTimeOffset.UtcNow,default);
        Assert.Empty((await store.LoadAsync(id,default)).Evidence);
    }
    [Fact] public async Task NewerSchemaIsNotSilentlyDowngraded()
    {
        Directory.CreateDirectory(folder);
        using(var db=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(folder,"knowledge.db")}"))
        { db.Open(); using var command=db.CreateCommand(); command.CommandText="PRAGMA user_version=99"; command.ExecuteNonQuery(); }
        await Assert.ThrowsAsync<InvalidDataException>(()=>Store().LoadAsync(new("fixture"),default));
    }
    [Fact] public async Task LocalOriginsSurviveRestart()
    {
        var store=Store(); var id=new AnalysisIdentity("local");
        await store.SaveAsync(KnowledgeEntry.Empty(id) with { Evidence=[new("local-role",DataOrigin.LocalCommandLine,DateTimeOffset.UtcNow,null,"CommandRole",new(){["commandRole"]="Worker"})] },default);
        Assert.Equal(DataOrigin.LocalCommandLine,Assert.Single((await Store().LoadAsync(id,default)).Evidence).Origin);
    }
    [Fact] public async Task PrivateCommandEvidenceRejectsArbitraryValues()
    {
        var entry=KnowledgeEntry.Empty(new("local")) with { Evidence=[new("bad-role",DataOrigin.LocalCommandLine,DateTimeOffset.UtcNow,null,"CommandRole",new(){["commandRole"]="a full command --secret arbitrary"})] };
        await Assert.ThrowsAsync<InvalidDataException>(()=>Store().SaveAsync(entry,default));
    }
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(folder)) Directory.Delete(folder,true);
    }
}

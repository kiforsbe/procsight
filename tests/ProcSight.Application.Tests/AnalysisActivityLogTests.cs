using System.Text.Json;
using ProcSight.Application.Analysis;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class AnalysisActivityLogTests
{
    private static readonly ProviderSettings Provider=new("fixture",new("https://example.com"),"model");
    private static AnalysisPayload Payload(string? command=null)=>new(new("identity"),new(){["name"]="tool.exe",["path"]=@"C:\private\SECRET.exe",["commandRole"]="private-script",["product"]="Product"},[],command,1,AnalysisDestination.CloudModel);
    [Fact] public void KeepsBoundedHistoryAndClearDoesNotRestoreInflightEntries()
    {
        var log=new AnalysisActivityLog();
        for(var i=0;i<201;i++) log.Begin(Payload(),Provider,"tool.exe",i);
        Assert.Equal(200,log.Snapshot().Count);
        Assert.Equal(200,log.Snapshot()[0].ProcessId);
        var id=log.Snapshot()[0].Id; log.Clear(); log.Fail(id);
        Assert.Empty(log.Snapshot());
    }
    [Fact] public void PausingStopsNewRowsButCompletesExistingRows()
    {
        var log=new AnalysisActivityLog(); var id=log.Begin(Payload(),Provider,"tool.exe",10);
        log.Enabled=false; Assert.Null(log.Begin(Payload(),Provider,"other.exe"));
        log.Fail(id,true); Assert.Equal(AnalysisActivityStatus.Cancelled,Assert.Single(log.Snapshot()).Status);
        Assert.NotNull(log.Snapshot()[0].CompletedAt);
    }
    [Fact] public void RequestAndOutputNeverRetainRawCommandsPathsOrSecrets()
    {
        var log=new AnalysisActivityLog(); var payload=Payload("tool.exe --token=SECRET");
        var id=log.Begin(payload,Provider,"tool.exe",10);
        var assessment=new Assessment(AssessmentAxis.Purpose,"Worker",@"SECRET C:\private\SECRET.exe",Confidence.High,[],DateTimeOffset.UtcNow,[]);
        log.Complete(id,new(new([assessment],[])),payload);
        var row=Assert.Single(log.Snapshot());
        Assert.True(row.CommandIncluded); Assert.DoesNotContain("path",row.Metadata.Keys);
        Assert.DoesNotContain("commandRole",row.Metadata.Keys);
        Assert.Contains("path",row.MetadataFields);
        Assert.DoesNotContain("SECRET",JsonSerializer.Serialize(row));
        Assert.Single(row.Assessments); Assert.Equal(AnalysisActivityStatus.Succeeded,row.Status);
    }
    [Fact] public void SnapshotCannotMutateHistoryAndNotificationFailuresAreIsolated()
    {
        var log=new AnalysisActivityLog(); log.Changed+=(_,_)=>throw new InvalidOperationException("Observer failure");
        log.Begin(Payload(),Provider,"tool.exe");
        Assert.Throws<NotSupportedException>(()=>((IDictionary<string,string>)log.Snapshot()[0].Metadata).Add("name","tampered"));
        Assert.Equal("tool.exe",log.Snapshot()[0].Metadata["name"]);
    }
    [Fact] public void ModelEchoesInAssessmentValuesAndPrivateLocationsAreOmitted()
    {
        var payload=Payload(); var log=new AnalysisActivityLog(); var id=log.Begin(payload,Provider,"tool.exe");
        var assessment=new Assessment(AssessmentAxis.Purpose,"tool.exe --PRIVATE_SCRIPT",
            @"Reads C:\Users\Someone\PRIVATE_FILE and https://example.com/oauth?state=PRIVATE_STATE",Confidence.Low,[],DateTimeOffset.UtcNow,[]);
        log.Complete(id,new(new([assessment],[])),payload);
        var serialized=JsonSerializer.Serialize(log.Snapshot());
        Assert.DoesNotContain("PRIVATE_",serialized);
        Assert.Equal("Unknown",log.Snapshot()[0].Assessments[0].Value);
    }
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now=DateTimeOffset.Parse("2026-10-14T10:00:00Z");
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    [Fact] public void RecordsTimingForServiceAndBoundsConcurrentWriters()
    {
        var clock=new ManualClock(); var log=new AnalysisActivityLog(clock);
        var id=log.Begin(Payload(),Provider,"Synthetic service",serviceIdentity:"FixtureService");
        clock.Now+=TimeSpan.FromSeconds(2); log.Fail(id);
        var row=Assert.Single(log.Snapshot());
        Assert.Equal(TimeSpan.FromSeconds(2),row.Elapsed); Assert.Equal("FixtureService",row.ServiceIdentity); Assert.Null(row.ProcessId);
        Parallel.For(0,500,i=> { var attempt=log.Begin(Payload(),Provider,"tool.exe",i); log.Fail(attempt); });
        Assert.Equal(200,log.Snapshot().Count);
        Assert.All(log.Snapshot(),r=>Assert.Equal(AnalysisActivityStatus.Failed,r.Status));
    }
}

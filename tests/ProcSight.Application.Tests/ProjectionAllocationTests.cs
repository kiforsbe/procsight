using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class ProjectionAllocationTests
{
    [Fact] public void StableFlatProjectionDoesNotDuplicateRowsAndAggregationWork() {
        var now=DateTimeOffset.UnixEpoch;
        var resources=new ResourceSample(Observation<double>.Available(1),Observation<long>.Available(1024),
            Observation<double>.Available(0),Observation<double>.Available(0),now,null);
        var processes=Enumerable.Range(1,1000).Select(i=>new ProcessSnapshot(new(i,now,null),null,new("synthetic.exe"),resources,[])).ToArray();
        var snapshot=new MachineSnapshot(Guid.NewGuid(),now,processes,[]);
        var entities=new IdentityResolver().Resolve(snapshot,[]);
        var options=new ViewOptions(PreviousOrder:entities.Select(ProcessListProjection.RowKey).ToArray());
        _=ProcessListProjection.Build(snapshot,entities,[],options);
        var bytes=GC.GetAllocatedBytesForCurrentThread();
        var result=ProcessListProjection.Build(snapshot,entities,[],options);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
        Assert.Equal(1000,result.Rows.Count); Assert.All(result.Rows,r=>Assert.Equal(1024,r.MemoryBytes));
        // Baseline was 2.47 MB; reserve headroom while requiring a substantial reduction.
        Assert.True(allocated<1_500_000,$"Stable 1,000-row projection allocated {allocated} bytes.");
    }
}

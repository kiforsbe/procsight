using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class PresentationTests
{
    [Fact] public void SearchMatchesPublisherPidAndService()
    {
        var snapshot=Samples.Snapshot(Samples.Process(123));
        var entities=new IdentityResolver().Resolve(snapshot,[]);
        Assert.Single(ProcessListProjection.Build(snapshot,entities,[],new(Search:"123")).Rows);
        Assert.Single(ProcessListProjection.Build(snapshot,entities,[],new(Search:"Example signer")).Rows);
    }
    [Fact] public void IncompleteGroupDoesNotClaimFullTotal()
    {
        var first=Samples.Process(1,product:"App");
        var second=Samples.Process(2,product:"App") with { Resources=first.Resources with { WorkingSetBytes=Observation<long>.Missing("Access denied") } };
        var snapshot=Samples.Snapshot(first,second);
        var state=ProcessListProjection.Build(snapshot,new IdentityResolver().Resolve(snapshot,[]),[],new(Grouped:true));
        var group=Assert.Single(state.Rows); Assert.True(group.IsGroup); Assert.False(group.CompleteMemory);
        Assert.Equal(1024,group.MemoryBytes);
    }
    [Fact] public void Churn500KeepsIdentityAndSelectionStable()
    {
        var snapshot=Samples.Snapshot(Enumerable.Range(1,500).Select(i=>Samples.Process(i,product:"App "+i)).ToArray());
        var entities=new IdentityResolver().Resolve(snapshot,[]);
        var state=ProcessListProjection.Build(snapshot,entities,[],new(Sort:ProcessSort.Memory));
        Assert.Equal(500,state.Rows.Count); Assert.Equal(500,state.Rows.Select(r=>r.Key).Distinct().Count());
        var selected=state.Rows.Single(r=>r.Entity.Process!.Key.Pid==100).Key;
        var reused=state.Rows.Single(r=>r.Entity.Process!.Key.Pid==1).Key;
        var next=Samples.Snapshot(snapshot.Processes.Where(p=>p.Key.Pid>20).Concat(
            Enumerable.Range(1,20).Select(i=>Samples.Process(i,product:"New app "+i,started:Samples.Now.AddMinutes(1)))).ToArray());
        var refreshed=ProcessListProjection.Build(next,new IdentityResolver().Resolve(next,[]),[],new(Sort:ProcessSort.Name));
        Assert.Equal(500,refreshed.Rows.Count);
        Assert.Contains(refreshed.Rows,r=>r.Key==selected);
        Assert.DoesNotContain(refreshed.Rows,r=>r.Key==reused);
    }
}

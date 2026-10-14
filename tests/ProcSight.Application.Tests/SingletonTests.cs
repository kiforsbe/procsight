using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class SingletonTests
{
    [Fact] public void SelectedChildBecomesSelectedStandaloneProcessWhenItsSiblingExits()
    {
        var a=Samples.Process(1,product:"App"); var b=Samples.Process(2,product:"App");
        var snapshot=Samples.Snapshot(a,b); var entities=new IdentityResolver().Resolve(snapshot,[]);
        var collapsed=ProcessListProjection.Build(snapshot,entities,[],new(Grouped:true));
        var expanded=ProcessListProjection.Build(snapshot,entities,[],new(Grouped:true,ExpandedGroups:new(){collapsed.Rows[0].Key}));
        var child=expanded.Rows.Single(r=>!r.IsGroup&&r.Entity.Process!.Key.Pid==2);
        var remaining=Samples.Snapshot(b);
        var next=ProcessListProjection.Build(remaining,new IdentityResolver().Resolve(remaining,[]),[],new(Grouped:true));
        Assert.Equal(next.Rows[0].Key,ProcessListProjection.SelectionKey(next,child));
    }
    [Fact] public void BlankWindowsDescriptionFallsBackToExecutableName()
    {
        var process=Samples.Process(name:"TextInputHost.exe",product:"Microsoft Windows Operating System") with {
            Executable=new("TextInputHost.exe",Product:"Microsoft Windows Operating System",Description:" ") };
        var entity=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(process),[]));
        Assert.Equal("TextInputHost.exe",entity.Owner);
        var row=Assert.Single(ProcessListProjection.Build(Samples.Snapshot(process),[entity],[],new(Grouped:true)).Rows);
        Assert.False(row.IsGroup); Assert.False(row.HasChildren);
    }
    [Fact] public void SingleToMultipleToSingleKeepsTopLevelOrder()
    {
        var a=Samples.Process(1,product:"Z App"); var b=Samples.Process(2,product:"Z App"); var other=Samples.Process(3,product:"A App");
        ProcessListState Project(MachineSnapshot s,IReadOnlyList<string>? order=null)=>ProcessListProjection.Build(s,
            new IdentityResolver().Resolve(s,[]),[],new(Grouped:true,PreviousOrder:order));
        var single=Project(Samples.Snapshot(a,other)); Assert.All(single.Rows,r=>Assert.False(r.IsGroup));
        var keys=single.Rows.Select(r=>r.Key).ToArray();
        var multiple=Project(Samples.Snapshot(a,b,other),keys);
        Assert.Equal(keys,multiple.Rows.Select(r=>r.Key)); Assert.Single(multiple.Rows,r=>r.IsGroup);
        var again=Project(Samples.Snapshot(b,other),keys);
        Assert.Equal(keys,again.Rows.Select(r=>r.Key)); Assert.All(again.Rows,r=>Assert.False(r.IsGroup));
    }
}

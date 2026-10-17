using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class LiveListTests
{
    private static ProcessListState Project(MachineSnapshot s,ViewOptions o)=>ProcessListProjection.Build(s,new IdentityResolver().Resolve(s,[]),[],o);
    [Fact] public void ExistingSiblingsKeepSlotsWhileNewSiblingsAppendInRequestedOrder()
    {
        var root=Samples.Process(1,name:"root.exe",started:Samples.Now.AddMinutes(-1));
        var a=Samples.Process(2,parent:1,name:"a.exe"); var z=Samples.Process(3,parent:1,name:"z.exe");
        var initial=Project(Samples.Snapshot(root,z,a),new(Descending:true));
        var next=Project(Samples.Snapshot(root,a,z, Samples.Process(4,parent:1,name:"c.exe"),Samples.Process(5,parent:1,name:"b.exe")),
            new(PreviousOrder:initial.Rows.Select(r=>r.Key).ToArray()));
        Assert.Equal(new[]{1,3,2,5,4},next.Rows.Select(r=>r.Entity.Process!.Key.Pid));
        Assert.Equal(new[]{0,1,1,1,1},next.Rows.Select(r=>r.Depth));
    }
    [Fact] public void CollapsedTreeDoesNotLeakDescendantsOrAttachAnUnverifiedParent()
    {
        var root=Samples.Process(1,started:Samples.Now.AddMinutes(-2));
        var child=Samples.Process(2,parent:1,started:Samples.Now.AddMinutes(-1));
        var grandchild=Samples.Process(3,parent:2);
        var reusedParent=Samples.Process(4,parent:1,started:Samples.Now.AddMinutes(-3));
        var snapshot=Samples.Snapshot(root,child,grandchild,reusedParent);
        var rows=Project(snapshot,new(CollapsedTree:[ProcessListProjection.RowKey(new IdentityResolver().Resolve(snapshot,[])[0])])).Rows;
        Assert.Equal(new[]{1,4},rows.Select(r=>r.Entity.Process!.Key.Pid));
        Assert.All(rows,r=>Assert.Equal(0,r.Depth));
        Assert.True(rows[0].HasChildren); Assert.False(rows[0].Expanded);
    }
    [Fact] public void StableOrderRefreshDoesNotRebuildEveryWorkloadSortLabel()
    {
        var snapshot=Samples.Snapshot(Enumerable.Range(1,1000).Select(i=>Samples.Process(i,name:"python.exe") with {
            CommandLine=new("python.exe synthetic_workload.py --synthetic",Samples.Now) }).ToArray());
        var entities=new IdentityResolver().Resolve(snapshot,[]);
        var prior=ProcessListProjection.Build(snapshot,entities,[],new()).Rows.Select(r=>r.Key).ToArray();
        ProcessListProjection.Build(snapshot,entities,[],new(PreviousOrder:prior));
        var before=GC.GetAllocatedBytesForCurrentThread();
        var refreshed=ProcessListProjection.Build(snapshot,entities,[],new(PreviousOrder:prior));
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.Equal(prior,refreshed.Rows.Select(r=>r.Key));
        Assert.True(allocated<3_000_000,$"Stable refresh allocated {allocated} bytes for 1000 synthetic rows.");
    }
    [Fact] public void MetricRefreshPreservesSiblingOrder()
    {
        var a=Samples.Process(1); var b=Samples.Process(2);
        a=a with { Resources=a.Resources with { WorkingSetBytes=Observation<long>.Available(100) } };
        b=b with { Resources=b.Resources with { WorkingSetBytes=Observation<long>.Available(200) } };
        var first=Project(Samples.Snapshot(a,b),new(Sort:ProcessSort.Memory));
        a=a with { Resources=a.Resources with { WorkingSetBytes=Observation<long>.Available(300) } };
        var next=Project(Samples.Snapshot(a,b),new(Sort:ProcessSort.Memory,PreviousOrder:first.Rows.Select(r=>r.Key).ToArray()));
        Assert.Equal(first.Rows.Select(r=>r.Key),next.Rows.Select(r=>r.Key));
    }
    [Fact] public void GroupBecomesOrdinaryProcessWithoutMovingItsSlotWhenOneMemberExits()
    {
        var a=Samples.Process(1,product:"App"); var b=Samples.Process(2,product:"App");
        var first=Assert.Single(Project(Samples.Snapshot(a,b),new(Grouped:true)).Rows);
        var next=Assert.Single(Project(Samples.Snapshot(a),new(Grouped:true)).Rows);
        Assert.Equal(first.Key,next.Key); Assert.False(next.IsGroup); Assert.False(next.HasChildren);
        Assert.Null(next.GroupName); Assert.Single(next.Members); Assert.Equal(1,next.Entity.Process!.Key.Pid);
    }
    [Fact] public void UnknownCreationTimeHasStableVisualSlotButSeparateAnalysisIdentity()
    {
        var a=Samples.Process() with { Key=new(20,null,Guid.NewGuid()) };
        var b=a with { Key=new(20,null,Guid.NewGuid()) };
        var first=Assert.Single(Project(Samples.Snapshot(a),new()).Rows);
        var next=Assert.Single(Project(Samples.Snapshot(b),new()).Rows);
        Assert.Equal(first.Key,next.Key);
    }
    [Fact] public void RawTreeCanCollapseAndExpandWithoutSelectionChange()
    {
        var s=Samples.Snapshot(Samples.Process(1,started:Samples.Now.AddMinutes(-1)),Samples.Process(2,parent:1));
        var open=Project(s,new()); var parent=open.Rows[0]; Assert.True(parent.HasChildren);
        var closed=Project(s,new(CollapsedTree:new(){parent.Key})); Assert.Single(closed.Rows);
        Assert.Equal(2,Project(s,new()).Rows.Count);
    }
    [Theory] [InlineData("--type=renderer","Page renderer")] [InlineData("--type=gpu-process","GPU rendering")]
    [InlineData("--utility-sub-type=network.mojom.NetworkService","Network service")]
    public void GenericTasksGetProvisionalLocalRole(string arguments,string role)
    {
        var p=Samples.Process(product:"Browser") with { CommandLine=new("app.exe "+arguments,Samples.Now) };
        var e=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(p),[]));
        Assert.Contains(role,ProcessRoleLabels.Describe(e).Label);
    }
    [Fact] public void GenericAssessmentLabelKeepsServiceSpecificName()
    {
        var p=Samples.Process(name:"svchost.exe") with { ServiceNames=["Dhcp"],Executable=new("svchost.exe",@"C:\Windows\System32\svchost.exe",
            Signature:SignatureStatus.Verified,VerifiedSigner:"Microsoft Windows",OriginalName:"svchost.exe") };
        var e=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(p),[]));
        Assert.Contains("Dhcp",ProcessRoleLabels.DisplayName(e,"Windows service host"));
    }
    [Fact] public void UnknownNodeScriptGetsLocalDisplayLabelWithoutInventingItsPurpose()
    {
        var p=Samples.Process() with { CommandLine=new("node.exe \"C:\\Example App\\server.mjs\" --token=secret",Samples.Now) };
        var e=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(p),[]));
        var label=ProcessRoleLabels.Describe(e);
        Assert.Contains(" — server.mjs",label.Label); Assert.DoesNotContain("secret",label.Label);
        Assert.True(label.UsesCommandContext);
    }
}

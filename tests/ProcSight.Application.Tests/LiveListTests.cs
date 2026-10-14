using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class LiveListTests
{
    private static ProcessListState Project(MachineSnapshot s,ViewOptions o)=>ProcessListProjection.Build(s,new IdentityResolver().Resolve(s,[]),[],o);
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

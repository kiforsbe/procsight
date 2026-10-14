using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class SortTests
{
    [Theory] [InlineData(ProcessSort.Name)] [InlineData(ProcessSort.Pid)] [InlineData(ProcessSort.Cpu)] [InlineData(ProcessSort.Memory)]
    public void AscendingAndDescendingOrderAreOpposite(ProcessSort column)
    {
        var a=Samples.Process(1,name:"a.exe"); var b=Samples.Process(2,name:"b.exe");
        a=a with { Resources=a.Resources with { CpuPercent=Observation<double>.Available(1),WorkingSetBytes=Observation<long>.Available(10) } };
        b=b with { Resources=b.Resources with { CpuPercent=Observation<double>.Available(2),WorkingSetBytes=Observation<long>.Available(20) } };
        var snapshot=Samples.Snapshot(a,b); var entities=new IdentityResolver().Resolve(snapshot,[]);
        var ascending=ProcessListProjection.Build(snapshot,entities,[],new(Sort:column)).Rows;
        var descending=ProcessListProjection.Build(snapshot,entities,[],new(Sort:column,Descending:true)).Rows;
        Assert.Equal(new[]{1,2},ascending.Select(r=>r.Entity.Process!.Key.Pid));
        Assert.Equal(new[]{2,1},descending.Select(r=>r.Entity.Process!.Key.Pid));
    }
    [Fact] public void NameSortingUsesVisibleWorkloadRatherThanExecutableName()
    {
        var a=Samples.Process(1,name:"python.exe") with { CommandLine=new("python.exe zulu.py",Samples.Now) };
        var b=Samples.Process(2,name:"python.exe") with { CommandLine=new("python.exe alpha.py",Samples.Now) };
        var snapshot=Samples.Snapshot(a,b);
        var rows=ProcessListProjection.Build(snapshot,new IdentityResolver().Resolve(snapshot,[]),[],new()).Rows;
        Assert.Equal(new[]{2,1},rows.Select(r=>r.Entity.Process!.Key.Pid));
    }
}

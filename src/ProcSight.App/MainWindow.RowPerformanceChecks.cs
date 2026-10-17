#if DEBUG
using ProcSight.App.ViewModels;
using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    // Synthetic UI-thread assertions: no real inventory, provider requests or persisted context.
    private static void CheckProcessRowPerformanceContracts()
    {
        static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
        var now=DateTimeOffset.Parse("2026-10-13T10:00:00Z");
        var process=new ProcessSnapshot(new(123,now,null),null,new("python.exe"),
            new(Observation<double>.Available(1),Observation<long>.Available(1048576),
                Observation<double>.Pending(),Observation<double>.Pending(),now,null),[],new("python.exe alpha.py",now));
        ResolvedEntity Resolve(ProcessSnapshot p)=>new IdentityResolver().Resolve(new(Guid.NewGuid(),now,[p],[]),[]).Single();
        ProcessListRow Project(ResolvedEntity e)=>new("synthetic",e,false,[e],null,e.Process!.Resources.WorkingSetBytes.Value??0,true,
            e.Process.Resources.CpuPercent.Value??0,true);
        var row=new ProcessRow { Key="synthetic" };
        row.Update(Project(Resolve(process)));
        var changed=new List<string>(); row.PropertyChanged+=(_,e)=>changed.Add(e.PropertyName!);
        var originalName=row.Name; var originalOwner=row.Owner; var originalCommand=row.CommandLine;
        row.Update(Project(Resolve(process with { Resources=process.Resources with { CpuPercent=Observation<double>.Available(2) } })));
        Check(row.Name==originalName&&row.Owner==originalOwner&&row.CommandLine==originalCommand,"Resource refresh changed local metadata.");
        Check(changed.Order().SequenceEqual(new[]{"Cpu","CpuBrush"}),"Resource refresh notified unrelated cells.");
        changed.Clear(); row.TotalMemoryBytes=10485760;
        Check(changed.Contains(nameof(ProcessRow.MemoryBrush)),"Memory denominator changes must notify the heat cell.");
        changed.Clear(); row.TotalMemoryBytes=10485760;
        Check(changed.Count==0,"Unchanged memory denominator notified the heat cell.");
        process=process with { CommandLine=new("python.exe beta.py",now),Windows=[new("Synthetic caption","SyntheticClass")] };
        row.Update(Project(Resolve(process)));
        Check(row.Name.Contains("beta.py")&&row.Name.Contains("Synthetic caption")&&row.CommandLine.Contains("beta.py")&&row.Windows=="Synthetic caption",
            "Changed command and captions were not reflected in cached cells.");
        Check(changed.Contains(nameof(ProcessRow.Name))&&changed.Contains(nameof(ProcessRow.Owner)),"Local presentation change missed notifications.");
        var plain=process with { Executable=new("unknown.exe"),CommandLine=null,Windows=null,MetadataIssues=[new("command",MetadataUnavailableReason.AccessDenied)] };
        row.Update(Project(Resolve(plain)));
        Check(!row.Name.Contains("beta.py")&&row.Windows=="—"&&row.Availability.Contains("AccessDenied"),"Metadata transition retained stale cells.");
        var analysis=new AnalysisUpdate(row.Entity.Identity,AnalysisState.Local,new([],null,[],[],[],"Synthetic assessed name","Worker"));
        row.SetAnalysis(analysis);
        Check(row.Name=="Synthetic assessed name"&&row.Owner.Contains("Worker"),"Analysis did not update name and role.");
        row.Update(Project(Resolve(plain with { Executable=new("another.exe",ContentHash:"synthetic-different-identity") })));
        Check(row.Name!="Synthetic assessed name"&&!row.Owner.Contains("Worker"),"Identity transition retained prior analysis.");
        var single=row.Projection; row.Update(single with { IsGroup=true,GroupName="Synthetic group",Members=[single.Entity,single.Entity],HasChildren=true });
        Check(row.Name=="Synthetic group (2)"&&row.CommandLine=="—","Group transition retained individual cells.");
        row.Update(single);
        Check(row.Name!="Synthetic group (2)"&&row.CommandLine=="—","Singleton transition retained group display.");
    }
}
#endif

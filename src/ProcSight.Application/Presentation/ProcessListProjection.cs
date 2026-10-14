using ProcSight.Core.Models;
using ProcSight.Core.Identity;
namespace ProcSight.Application.Presentation;
public enum ProcessSort { Name, Pid, Cpu, Memory }
public sealed record ViewOptions(string Search="",bool Grouped=false,bool Services=false,ProcessSort Sort=ProcessSort.Name,
    HashSet<string>? ExpandedGroups=null,string? ClassificationFilter=null);
public sealed record ProcessListRow(string Key,ResolvedEntity Entity,bool IsGroup,List<ResolvedEntity> Members,
    string? GroupName,long MemoryBytes,bool CompleteMemory,double CpuPercent,bool CompleteCpu,int Depth=0);
public sealed record ProcessListState(List<ProcessListRow> Rows);
public static class ProcessListProjection
{
    public static ProcessListState Build(MachineSnapshot snapshot,IReadOnlyList<ResolvedEntity> entities,
        IReadOnlyList<AnalysisUpdate> analysis,ViewOptions options)
    {
        var findings=analysis.GroupBy(a=>a.Identity).ToDictionary(g=>g.Key,g=>g.Last());
        var visible=entities.Where(e=>options.Services?e.Service is not null:e.Process is not null)
            .Where(e=>string.IsNullOrWhiteSpace(options.Search)||string.Join(" ",e.Facts.Values.Append(e.DisplayName)
                .Append(e.Process?.Executable.Company??"").Append(e.Process?.Key.Pid.ToString()??e.Service?.HostPid?.ToString()??""))
                .Contains(options.Search,StringComparison.OrdinalIgnoreCase))
            .Where(e=>options.ClassificationFilter is null || (options.ClassificationFilter=="Unknown"?
                !findings.TryGetValue(e.Identity,out var f)||f.Result.UnresolvedAxes.Count>0:
                findings.TryGetValue(e.Identity,out var found)&&found.Result.Assessments.Any(a=>a.Value==options.ClassificationFilter))).ToList();
        ProcessListRow Row(ResolvedEntity e,List<ResolvedEntity>? members=null,string? group=null)
        {
            members ??=[e];
            return new(group is null?e.Process?.Key.ToString()??e.Identity.Value:group,e,group is not null,members,
                group is null?null:e.Owner,members.Sum(m=>m.Process?.Resources.WorkingSetBytes.Value??0),
                members.All(m=>m.Process?.Resources.WorkingSetBytes.Value is not null),
                members.Sum(m=>m.Process?.Resources.CpuPercent.Value??0),
                members.All(m=>m.Process?.Resources.CpuPercent.Value is not null));
        }
        List<ProcessListRow> Sort(IEnumerable<ProcessListRow> rows)=>options.Sort switch {
            ProcessSort.Pid=>rows.OrderBy(r=>r.Entity.Process?.Key.Pid??r.Entity.Service?.HostPid??0).ToList(),
            ProcessSort.Cpu=>rows.OrderByDescending(r=>r.CpuPercent).ThenBy(r=>r.Entity.DisplayName).ToList(),
            ProcessSort.Memory=>rows.OrderByDescending(r=>r.MemoryBytes).ThenBy(r=>r.Entity.DisplayName).ToList(),
            _=>rows.OrderBy(r=>r.GroupName??r.Entity.DisplayName,StringComparer.CurrentCultureIgnoreCase).ToList() };
        if(options.Grouped&&!options.Services)
        {
            var top=new List<ProcessListRow>();
            foreach(var group in visible.GroupBy(e=>e.Owner is null?e.Process!.Key.ToString():e.Owner+"|"+e.Facts.GetValueOrDefault("ownerIdentity",e.Owner)))
            {
                var members=group.ToList();
                top.Add(members.Count>1&&members[0].Owner is not null?Row(members[0],members,"group:"+group.Key):Row(members[0]));
            }
            var rows=new List<ProcessListRow>();
            foreach(var row in Sort(top))
            {
                rows.Add(row);
                if(row.IsGroup&&options.ExpandedGroups?.Contains(row.Key)==true)
                    rows.AddRange(Sort(row.Members.Select(e=>Row(e))).Select(r=>r with { Depth=1 }));
            }
            return new(rows);
        }
        if(options.Services) return new(Sort(visible.Select(e=>Row(e))));
        var byPid=visible.ToDictionary(e=>e.Process!.Key.Pid);
        bool HasParent(ResolvedEntity e)=>e.Process!.ParentPid is { } pid && byPid.TryGetValue(pid,out var parent)&&
            ParentLinkValidator.IsVerified(e.Process,parent.Process!);
        var seen=new HashSet<int>(); var tree=new List<ProcessListRow>();
        void Add(ResolvedEntity entity,int depth)
        {
            if(!seen.Add(entity.Process!.Key.Pid)) return;
            tree.Add(Row(entity) with { Depth=depth });
            foreach(var child in Sort(visible.Where(e=>e.Process!.ParentPid==entity.Process.Key.Pid &&
                ParentLinkValidator.IsVerified(e.Process,entity.Process)).Select(e=>Row(e)))) Add(child.Entity,Math.Min(depth+1,12));
        }
        foreach(var root in Sort(visible.Where(e=>!HasParent(e)).Select(e=>Row(e)))) Add(root.Entity,0);
        foreach(var rest in visible.Where(e=>!seen.Contains(e.Process!.Key.Pid))) Add(rest,0);
        return new(tree);
    }
}

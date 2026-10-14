using ProcSight.Core.Models;
using ProcSight.Core.Identity;
namespace ProcSight.Application.Presentation;
public enum ProcessSort { Name, Pid, Cpu, Memory, Assessment, ServiceState, Startup, Disk, Network }
public sealed record ViewOptions(string Search="",bool Grouped=false,bool Services=false,ProcessSort Sort=ProcessSort.Name,
    HashSet<string>? ExpandedGroups=null,string? ClassificationFilter=null,IReadOnlyList<string>? PreviousOrder=null,HashSet<string>? CollapsedTree=null,bool Descending=false);
public sealed record ProcessListRow(string Key,ResolvedEntity Entity,bool IsGroup,List<ResolvedEntity> Members,
    string? GroupName,long MemoryBytes,bool CompleteMemory,double CpuPercent,bool CompleteCpu,int Depth=0,bool HasChildren=false,bool Expanded=false);
public sealed record ProcessListState(List<ProcessListRow> Rows);
public static class ProcessListProjection
{
    public static string? SelectionKey(ProcessListState state,ProcessListRow? selected)
    {
        if(selected is null) return null;
        var exact=state.Rows.FirstOrDefault(row=>row.Key==selected.Key);
        if(exact is not null) return exact.Key;
        if(selected.Entity.Process is not { } process) return null;
        return state.Rows.FirstOrDefault(row=>!row.IsGroup&&row.Entity.Process?.Key==process.Key)?.Key ??
            state.Rows.FirstOrDefault(row=>row.IsGroup&&row.Members.Any(m=>m.Process?.Key==process.Key))?.Key;
    }
    public static ProcessListState Build(MachineSnapshot snapshot,IReadOnlyList<ResolvedEntity> entities,
        IReadOnlyList<AnalysisUpdate> analysis,ViewOptions options)
    {
        var findings=analysis.GroupBy(a=>a.Identity).ToDictionary(g=>g.Key,g=>g.Last());
        var visible=entities.Where(e=>options.Services?e.Service is not null:e.Process is not null)
            .Where(e=>string.IsNullOrWhiteSpace(options.Search)||string.Join(" ",e.Facts.Values.Append(e.DisplayName).Append(ProcessRoleLabels.DisplayName(e,findings.GetValueOrDefault(e.Identity)?.Result.IdentityLabel))
                .Append(e.Process?.Executable.Company??"").Append(e.Process?.Key.Pid.ToString()??e.Service?.HostPid?.ToString()??""))
                .Contains(options.Search,StringComparison.OrdinalIgnoreCase))
            .Where(e=>options.ClassificationFilter is null || (options.ClassificationFilter=="Unknown"?
                !findings.TryGetValue(e.Identity,out var f)||f.Result.UnresolvedAxes.Count>0:
                findings.TryGetValue(e.Identity,out var found)&&found.Result.Assessments.Any(a=>a.Value==options.ClassificationFilter))).ToList();
        ProcessListRow Row(ResolvedEntity e,List<ResolvedEntity>? members=null,string? group=null)
        {
            members ??=[e];
            return new(group is null?RowKey(e):group,e,group is not null,members,
                group is null?null:e.Owner,members.Sum(m=>m.Process?.Resources.WorkingSetBytes.Value??0),
                members.All(m=>m.Process?.Resources.WorkingSetBytes.Value is not null),
                members.Sum(m=>m.Process?.Resources.CpuPercent.Value??0),
                members.All(m=>m.Process?.Resources.CpuPercent.Value is not null));
        }
        var prior=(options.PreviousOrder ?? []).Select((key,index)=>(key,index)).ToDictionary(p=>p.key,p=>p.index);
        List<ProcessListRow> Sort(IEnumerable<ProcessListRow> rows) {
            string Display(ProcessListRow row)=>row.GroupName ?? ProcessRoleLabels.DisplayName(row.Entity,findings.GetValueOrDefault(row.Entity.Identity)?.Result.IdentityLabel);
            string Assessment(ProcessListRow row) {
                var finding=findings.GetValueOrDefault(row.Entity.Identity);
                return finding?.Result.Assessments.FirstOrDefault(a=>a.Value is "Suspicious" or "KnownThreat" or "PotentiallyUnwanted" or "PossibleLeftover")?.Value ??
                    finding?.Result.Assessments.FirstOrDefault(a=>a.Axis==AssessmentAxis.Purpose)?.Value ?? finding?.State.ToString() ?? row.Entity.Service?.State ?? "Unknown";
            }
            IOrderedEnumerable<ProcessListRow> Order<T>(Func<ProcessListRow,T> value,IComparer<T>? comparer=null)=>
                options.Descending?rows.OrderByDescending(value,comparer):rows.OrderBy(value,comparer);
            var ordered=options.Sort switch {
                ProcessSort.Pid=>Order(r=>r.Members.Min(m=>m.Process?.Key.Pid??m.Service?.HostPid??0)),
                ProcessSort.Cpu=>Order(r=>r.CpuPercent),ProcessSort.Memory=>Order(r=>r.MemoryBytes),
                ProcessSort.Assessment=>Order(Assessment,StringComparer.CurrentCultureIgnoreCase),
                ProcessSort.ServiceState=>Order(r=>r.Entity.Service?.State ?? "",StringComparer.CurrentCultureIgnoreCase),
                ProcessSort.Startup=>Order(r=>r.Entity.Service?.StartMode ?? "",StringComparer.CurrentCultureIgnoreCase),
                ProcessSort.Disk=>Order(r=>r.Members.Sum(m=>m.Process?.Resources.DiskBytesPerSecond.Value??0)),
                ProcessSort.Network=>Order(r=>r.Members.Sum(m=>m.Process?.Resources.NetworkBytesPerSecond.Value??0)),
                _=>Order(Display,StringComparer.CurrentCultureIgnoreCase)
            };
            bool Missing(ProcessListRow row)=>options.Sort switch {
                ProcessSort.Cpu=>!row.CompleteCpu,ProcessSort.Memory=>!row.CompleteMemory,
                ProcessSort.Disk=>row.Members.All(m=>m.Process?.Resources.DiskBytesPerSecond.Value is null),
                ProcessSort.Network=>row.Members.All(m=>m.Process?.Resources.NetworkBytesPerSecond.Value is null),
                _=>false };
            var sorted=ordered.ThenBy(Display,StringComparer.CurrentCultureIgnoreCase).ThenBy(r=>r.Key,StringComparer.Ordinal).OrderBy(Missing).ToList();
            return prior.Count==0?sorted:sorted.OrderBy(r=>prior.GetValueOrDefault(r.Key,int.MaxValue)).ToList();
        }
        if(options.Grouped&&!options.Services)
        {
            var top=new List<ProcessListRow>();
            foreach(var group in visible.GroupBy(e=>e.Owner is null?e.Process!.Key.ToString():e.Owner+"|"+e.Facts.GetValueOrDefault("ownerIdentity",e.Owner)))
            {
                var members=group.ToList();
                var representative=members.FirstOrDefault(e=>e.Process?.Executable.Product==e.Owner) ?? members[0];
                var slot="group:"+group.Key;
                top.Add(representative.Owner is not null && members.Count>1?Row(representative,members,slot) with {
                    HasChildren=true,Expanded=options.ExpandedGroups?.Contains(slot)==true }:
                    Row(representative) with { Key=representative.Owner is not null?slot:RowKey(representative) });
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
            var children=Sort(visible.Where(e=>e.Process!.ParentPid==entity.Process.Key.Pid &&
                ParentLinkValidator.IsVerified(e.Process,entity.Process)).Select(e=>Row(e)));
            var expanded=options.CollapsedTree?.Contains(RowKey(entity))!=true;
            tree.Add(Row(entity) with { Depth=depth,HasChildren=children.Count>0,Expanded=expanded });
            foreach(var child in children)
                if(expanded) Add(child.Entity,Math.Min(depth+1,12)); else Hide(child.Entity);
        }
        void Hide(ResolvedEntity entity) {
            if(!seen.Add(entity.Process!.Key.Pid)) return;
            foreach(var child in visible.Where(e=>e.Process!.ParentPid==entity.Process.Key.Pid&&ParentLinkValidator.IsVerified(e.Process,entity.Process))) Hide(child);
        }
        foreach(var root in Sort(visible.Where(e=>!HasParent(e)).Select(e=>Row(e)))) Add(root.Entity,0);
        foreach(var rest in visible.Where(e=>!seen.Contains(e.Process!.Key.Pid))) Add(rest,0);
        return new(tree);
    }
    public static string RowKey(ResolvedEntity entity)=>entity.Process is not { } p?entity.Identity.Value:
        p.Key.StartedAt is not null?p.Key.ToString():$"unverified:{p.Key.Pid}:{p.Executable.FullPath ?? p.Executable.Name}";
}

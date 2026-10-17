using System.Globalization;
using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
/// <summary>Local-only table/export projection. Not a provider payload or a diagnostic object.</summary>
public static class TableValues
{
    public static IReadOnlyDictionary<string,string?> Read(ResolvedEntity entity,AnalysisUpdate? finding=null,bool commands=true,bool identifying=true,bool windows=true)
    {
        var p=entity.Process; var s=entity.Service; var exe=p?.Executable??s?.Implementation;
        var safeEntity=p is null?entity:entity with { Process=p with { CommandLine=commands?p.CommandLine:null,Windows=windows?p.Windows:null } };
        var assessments=finding?.Result.Assessments;
        string? Numeric<T>(T? value) where T:struct=>value is IFormattable f?f.ToString(null,CultureInfo.InvariantCulture):value?.ToString();
        return new Dictionary<string,string?> {
            ["Name"]=ProcessRoleLabels.DisplayName(safeEntity,finding?.Result.IdentityLabel),
            ["Assessment"]=assessments is { Count:>0 }?string.Join(" / ",assessments.Select(a=>a.Axis+": "+a.Value)):"Unknown",
            ["Pid"]=(p?.Key.Pid??s?.HostPid)?.ToString(CultureInfo.InvariantCulture),
            ["Cpu"]=Numeric(p?.Resources.CpuPercent.Value),["Memory"]=Numeric(p?.Resources.WorkingSetBytes.Value),
            ["Disk"]=Numeric(p?.Resources.DiskBytesPerSecond.Value),["Network"]=Numeric(p?.Resources.NetworkBytesPerSecond.Value),
            ["NativeName"]=exe?.Name,["CommandLine"]=commands?p?.CommandLine?.Raw??s?.BinaryPath:null,
            ["Path"]=identifying?exe?.FullPath:null,["ParentPid"]=p?.ParentPid?.ToString(CultureInfo.InvariantCulture),
            ["Publisher"]=exe?.Signature==SignatureStatus.Verified?exe.VerifiedSigner:null,["Signature"]=exe?.Signature.ToString(),
            ["Started"]=p?.Key.StartedAt?.ToString("O"),["Owner"]=identifying?entity.Owner:null,
            ["Windows"]=windows&&p?.Windows is { } captions?string.Join("\n",captions.Select(w=>w.Title)):null,
            ["ServiceName"]=s?.Name,["ServiceState"]=s?.State,["Startup"]=s?.StartMode,
            ["Availability"]=string.Join("; ",(p?.MetadataIssues??s?.MetadataIssues??[]).Concat(exe?.MetadataIssues??[]).Distinct().Select(i=>i.Field+": "+i.Reason))
        };
    }
}

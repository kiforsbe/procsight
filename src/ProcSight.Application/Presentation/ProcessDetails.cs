using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
/// <summary>Transient detail text for local display only; never feed it to storage or providers.</summary>
public static class ProcessDetails
{
    public static string FormatIdentity(ResolvedEntity entity,CommandLineMode mode)
    {
        string Missing(string field,IReadOnlyList<MetadataIssue>? issues,bool fileFallback=false) {
            var reason=(issues?.FirstOrDefault(i=>i.Field==field)??(fileFallback?issues?.FirstOrDefault(i=>i.Field=="FileMetadata"):null))?.Reason;
            return "Unavailable ("+(reason switch {
                MetadataUnavailableReason.AccessDenied=>"access denied by Windows",
                MetadataUnavailableReason.ProcessExited=>"process exited before collection",
                MetadataUnavailableReason.NotFound=>"file or metadata source not found",
                MetadataUnavailableReason.ReadFailed=>"Windows could not complete the read",
                _=>"not provided by Windows"
            })+")";
        }
        string Value(string? value,string field,ExecutableMetadata exe)=>string.IsNullOrWhiteSpace(value)?Missing(field,exe.MetadataIssues,true):value;
        string File(ExecutableMetadata? exe)=>exe is null?"Implementation metadata: Unavailable":
            $"Executable: {exe.Name}\nPath: {Value(exe.FullPath,"ExecutablePath",exe)}\nOriginal filename: {Value(exe.OriginalName,"OriginalName",exe)}\n"+
            $"Product metadata: {Value(exe.Product,"Product",exe)}\nVersion: {Value(exe.Version,"Version",exe)}\nCompany metadata: {Value(exe.Company,"Company",exe)}\n"+
            $"Signature: {exe.Signature}"+(exe.MetadataIssues?.Any(i=>i.Field=="Signature")==true?" · "+Missing("Signature",exe.MetadataIssues):"")+"\n"+
            $"Verified signer: {Value(exe.Signature==SignatureStatus.Verified?exe.VerifiedSigner:null,"Signature",exe)}\n"+
            $"SHA-256: {Value(exe.ContentHash,"ContentHash",exe)}\nPackage: {Value(exe.PackageIdentity,"Package",exe)}";
        if(entity.Process is { } process) return
            $"PID: {process.Key.Pid}\nReported parent PID: {process.ParentPid?.ToString() ?? "Unavailable"}\nStarted: {process.Key.StartedAt?.ToString("O") ?? "Unavailable"}\n"+
            File(process.Executable)+$"\nServices: {string.Join(", ",process.ServiceNames)}\n"+
            (process.MetadataIssues is { Count:>0 }?"\nCollection availability:\n"+string.Join("\n",process.MetadataIssues.Select(i=>i.Field+": "+Missing(i.Field,process.MetadataIssues)))+"\n":"")+
            $"\nCommand line:\n{process.CommandLine?.Raw??Missing("CommandLine",process.MetadataIssues)}\n\nVisible windows:\n"+
            (process.Windows is null?"Unavailable":process.Windows.Count==0?"None observed":string.Join("\n",process.Windows.Select(w=>
                $"{(string.IsNullOrWhiteSpace(w.Title)?"(untitled or unavailable)":w.Title)} · class {w.ClassName}"+(w.Foreground?" · foreground":"")+(w.Minimized?" · minimized":""))));
        var service=entity.Service!;
        return $"Service: {service.Name}\nState: {service.State}\nStart mode: {service.StartMode}\nHost PID: {service.HostPid}\n\nConfigured implementation:\n"+
            File(service.Implementation)+$"\nConfigured host: {service.HostExecutable?.FullPath ?? "Unavailable"}\nHost signature: {service.HostExecutable?.Signature.ToString() ?? "Unavailable"}\n"+
            "Configuration identifies a file; loaded-module state has not been verified. Host signature does not authenticate its service DLL."+
            $"\n\nConfigured command line:\n{service.BinaryPath??Missing("PathName",service.MetadataIssues)}"+
            (service.MetadataIssues is { Count:>0 }?"\n\nCollection availability:\n"+string.Join("\n",service.MetadataIssues.Select(i=>i.Field+": "+Missing(i.Field,service.MetadataIssues))):"");
    }
}

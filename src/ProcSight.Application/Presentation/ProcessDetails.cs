using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
/// <summary>Transient detail text for local display only; never feed it to storage or providers.</summary>
public static class ProcessDetails
{
    public static string FormatIdentity(ResolvedEntity entity,CommandLineMode mode)
    {
        string Command(string? raw)=>raw ?? "Unavailable (Windows did not permit collection)";
        string File(ExecutableMetadata? exe)=>exe is null?"Implementation metadata: Unavailable":
            $"Executable: {exe.Name}\nPath: {exe.FullPath ?? "Unavailable"}\nOriginal filename: {exe.OriginalName ?? "Unavailable"}\n"+
            $"Product metadata: {exe.Product ?? "Unavailable"}\nVersion: {exe.Version ?? "Unavailable"}\nCompany metadata: {exe.Company ?? "Unavailable"}\n"+
            $"Signature: {exe.Signature}\nVerified signer: {(exe.Signature==SignatureStatus.Verified?exe.VerifiedSigner:null) ?? "Unavailable"}\n"+
            $"SHA-256: {exe.ContentHash ?? "Unavailable"}\nPackage: {exe.PackageIdentity ?? "Unavailable"}";
        if(entity.Process is { } process) return
            $"PID: {process.Key.Pid}\nReported parent PID: {process.ParentPid?.ToString() ?? "Unavailable"}\nStarted: {process.Key.StartedAt?.ToString("O") ?? "Unavailable"}\n"+
            File(process.Executable)+$"\nServices: {string.Join(", ",process.ServiceNames)}\n\nCommand line:\n{Command(process.CommandLine?.Raw)}\n\nVisible windows:\n"+
            (process.Windows is null?"Unavailable":process.Windows.Count==0?"None observed":string.Join("\n",process.Windows.Select(w=>
                $"{(string.IsNullOrWhiteSpace(w.Title)?"(untitled or unavailable)":w.Title)} · class {w.ClassName}"+(w.Foreground?" · foreground":"")+(w.Minimized?" · minimized":""))));
        var service=entity.Service!;
        return $"Service: {service.Name}\nState: {service.State}\nStart mode: {service.StartMode}\nHost PID: {service.HostPid}\n\nConfigured implementation:\n"+
            File(service.Implementation)+$"\nConfigured host: {service.HostExecutable?.FullPath ?? "Unavailable"}\nHost signature: {service.HostExecutable?.Signature.ToString() ?? "Unavailable"}\n"+
            "Configuration identifies a file; loaded-module state has not been verified. Host signature does not authenticate its service DLL."+
            $"\n\nConfigured command line:\n{Command(service.BinaryPath)}";
    }
}

using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
public sealed record LocalDetailField(string Label,[property:System.Text.Json.Serialization.JsonIgnore] string Value)
{
    public override string ToString()=>"LocalDetailField [transient local display]";
}
public sealed record LocalDetailSection(string Key,string Title,IReadOnlyList<LocalDetailField> Fields);
/// <summary>Transient detail text for local display only; never feed it to storage or providers.</summary>
public static class ProcessDetails
{
    private static string Unavailable(string field,IReadOnlyList<MetadataIssue>? issues,bool fileFallback=false)
    {
        var reason=(issues?.FirstOrDefault(i=>i.Field==field)??(fileFallback?issues?.FirstOrDefault(i=>i.Field=="FileMetadata"):null))?.Reason;
        return "Unavailable ("+(reason switch {
            MetadataUnavailableReason.AccessDenied=>"access denied by Windows",
            MetadataUnavailableReason.ProcessExited=>"process exited before collection",
            MetadataUnavailableReason.NotFound=>"file or metadata source not found",
            MetadataUnavailableReason.ReadFailed=>"Windows could not complete the read",
            _=>"not provided by Windows"
        })+")";
    }
    public static IReadOnlyList<LocalDetailSection> Sections(ResolvedEntity entity)
    {
        var sections=new List<LocalDetailSection>();
        void Add(string key,string title,params (string Label,string Value)[] fields)=>sections.Add(new(key,title,fields.Select(f=>new LocalDetailField(f.Label,f.Value)).ToArray()));
        void File(ExecutableMetadata? exe)
        {
            if(exe is null) { Add("executable","Executable",("Implementation metadata","Unavailable")); return; }
            string Value(string? value,string field)=>string.IsNullOrWhiteSpace(value)?Unavailable(field,exe.MetadataIssues,true):value;
            Add("executable","Executable",("Name",exe.Name),("Path",Value(exe.FullPath,"ExecutablePath")),("Original filename",Value(exe.OriginalName,"OriginalName")),
                ("Product",Value(exe.Product,"Product")),("Version",Value(exe.Version,"Version")),("Company metadata",Value(exe.Company,"Company")),("Package",Value(exe.PackageIdentity,"Package")));
            Add("signature","Signature & file identity",("Signature",exe.Signature+(exe.MetadataIssues?.Any(i=>i.Field=="Signature")==true?" · "+Unavailable("Signature",exe.MetadataIssues):"")),
                ("Verified signer",Value(exe.Signature==SignatureStatus.Verified?exe.VerifiedSigner:null,"Signature")),("SHA-256",Value(exe.ContentHash,"ContentHash")));
        }
        void Issues(IReadOnlyList<MetadataIssue>? issues)
        {
            if(issues is { Count:>0 }) sections.Add(new("availability","Collection availability",issues.Select(i=>new LocalDetailField(RuleInspection.Field(i.Field),Unavailable(i.Field,issues))).ToArray()));
        }
        if(entity.Process is { } process) {
            Add("process","Process identity",("PID",process.Key.Pid.ToString()),("Reported parent PID",process.ParentPid?.ToString()??"Unavailable"),("Started",process.Key.StartedAt?.ToLocalTime().ToString("g")??"Unavailable"));
            File(process.Executable);
            Add("command","Command line",("Local invocation",process.CommandLine?.Raw??Unavailable("CommandLine",process.MetadataIssues)));
            Add("windows","Visible windows",("Captions & classes",process.Windows is null?"Unavailable":process.Windows.Count==0?"None observed":string.Join("\n\n",process.Windows.Select(w=>
                $"{(string.IsNullOrWhiteSpace(w.Title)?"(untitled or unavailable)":w.Title)}\nClass: {w.ClassName}"+(w.Foreground?" · foreground":"")+(w.Minimized?" · minimized":"")))));
            if(process.ServiceNames.Count>0) Add("services","Hosted services",("Services",string.Join("\n",process.ServiceNames)));
            Issues(process.MetadataIssues);
        } else if(entity.Service is { } service) {
            Add("service","Service identity",("Name",service.Name),("State",service.State),("Start mode",service.StartMode),("Host PID",service.HostPid?.ToString()??"Unavailable"));
            File(service.Implementation);
            Add("host","Configured host",("Executable",service.HostExecutable?.FullPath??"Unavailable"),("Host signature",service.HostExecutable?.Signature.ToString()??"Unavailable"),
                ("Evidence boundary","Configuration identifies a file; loaded-module state has not been verified. Host signature does not authenticate its service DLL."));
            Add("command","Configured command line",("Local configuration",service.BinaryPath??Unavailable("PathName",service.MetadataIssues)));
            Issues(service.MetadataIssues);
        }
        return sections;
    }
    public static string FormatIdentity(ResolvedEntity entity,CommandLineMode mode)
    {
        string Missing(string field,IReadOnlyList<MetadataIssue>? issues,bool fileFallback=false)=>Unavailable(field,issues,fileFallback);
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

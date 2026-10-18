using Microsoft.Win32;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
/// <summary>Configuration evidence only: never load or execute a service module.</summary>
public static class ServiceBinaryReader
{
    public static string? ExecutablePath(string? configuration)
    {
        if(string.IsNullOrWhiteSpace(configuration) || configuration.Length>32768) return null;
        var line=Environment.ExpandEnvironmentVariables(configuration.Trim());
        string? path;
        if(line.StartsWith('"')) {
            var end=line.IndexOf('"',1); path=end>1?line[1..end]:null;
        } else {
            var end=line.IndexOf(".exe",StringComparison.OrdinalIgnoreCase);
            path=end>=0 && (end+4==line.Length || char.IsWhiteSpace(line[end+4]))?line[..(end+4)]:null;
            if(path?.Any(char.IsWhiteSpace)==true) return null;
        }
        return LocalPath(path);
    }
    private static string? LocalPath(string? path)
    {
        if(path is null) return null;
        path=Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if(path.StartsWith(@"\SystemRoot\",StringComparison.OrdinalIgnoreCase))
            path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),path[12..]);
        return path.Length>=3 && char.IsAsciiLetter(path[0]) && path[1]==':' && path[2]=='\\' &&
            !path.Contains('"') && !path.Contains('\0')?path:null;
    }
    public static ExecutableMetadata? Implementation(string name,string? configuration,ProcessMetadataReader reader)
        => Implementation(name,configuration,reader,out _);
    public static ExecutableMetadata? Implementation(string name,string? configuration,ProcessMetadataReader reader,out MetadataUnavailableReason? reason)
    {
        reason=null;
        var host=ExecutablePath(configuration); if(host is null) return null;
        if(!Path.GetFileName(host).Equals("svchost.exe",StringComparison.OrdinalIgnoreCase))
            return reader.Read(Path.GetFileName(host),host);
        try {
            using var service=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+name);
            var module=ModulePath(service);
            if(module is null && name.LastIndexOf('_') is var separator && separator>0 &&
                name[(separator+1)..] is { Length:>0 and <=16 } suffix && suffix.All(Uri.IsHexDigit)) {
                using var template=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+name[..separator]);
                if(template?.GetValue("Type") is int type && (type&0x40)!=0) module=ModulePath(template);
            }
            if(module is null) { reason=MetadataUnavailableReason.NotProvided; return null; }
            return reader.Read(Path.GetFileName(module),module);
        } catch(Exception e) when(MetadataAccess.IsUnavailable(e)) { reason=MetadataAccess.Reason(e); return null; }
    }
    private static string? ModulePath(RegistryKey? service)
    {
        using var parameters=service?.OpenSubKey("Parameters");
        return LocalPath(parameters?.GetValue("ServiceDll") as string ?? service?.GetValue("ServiceDll") as string);
    }
}

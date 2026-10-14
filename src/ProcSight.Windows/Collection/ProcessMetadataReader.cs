using System.Diagnostics;
using System.Management;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public sealed class ProcessMetadataReader
{
    private readonly Func<string, FileVersionInfo> readVersion;
    public ProcessMetadataReader(Func<string, FileVersionInfo>? readVersion=null) => this.readVersion=readVersion ?? FileVersionInfo.GetVersionInfo;
    private readonly Dictionary<string, (DateTime Stamp, ExecutableMetadata Value)> cache = new(StringComparer.OrdinalIgnoreCase);
    public ExecutableMetadata Read(string name, string? path)
    {
        if (path is null) return new(name);
        try
        {
            var stamp = File.GetLastWriteTimeUtc(path);
            if (cache.TryGetValue(path, out var value) && value.Stamp == stamp) return value.Value;
            var version = readVersion(path);
            var metadata = new ExecutableMetadata(name, path, Version: version.FileVersion,
                Product: version.ProductName, Company: version.CompanyName,Description:version.FileDescription,OriginalName:version.OriginalFilename);
            cache[path] = (stamp, metadata);
            return metadata;
        }
        catch (Exception e) when (MetadataAccess.IsUnavailable(e))
        { return new(name, path, MetadataIssues:[new("FileMetadata", MetadataAccess.Reason(e))]); }
    }
    public static Dictionary<int, string> ReadCommandLines() => ReadCommandLineMetadata().Commands;
    public static (Dictionary<int, string> Commands, Dictionary<int, MetadataUnavailableReason> Issues,
        MetadataUnavailableReason MissingReason) ReadCommandLineMetadata()
    {
        var result = new Dictionary<int, string>();
        var issues = new Dictionary<int, MetadataUnavailableReason>();
        var missingReason = MetadataUnavailableReason.NotProvided;
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process");
            searcher.Options.Timeout = TimeSpan.FromSeconds(3);
            using var objects = searcher.Get();
            foreach (ManagementObject item in objects)
                using (item)
                {
                    int? pid=null;
                    try
                    {
                        pid=Convert.ToInt32(item["ProcessId"]);
                        if (item["CommandLine"] is string command && !string.IsNullOrWhiteSpace(command)) result[pid.Value]=command;
                        else issues[pid.Value]=MetadataUnavailableReason.NotProvided;
                    }
                    catch (Exception e) when (MetadataAccess.IsUnavailable(e))
                    { if(pid is { } value) issues[value]=MetadataAccess.Reason(e); }
                }
        }
        catch (Exception e) when (MetadataAccess.IsUnavailable(e)) { missingReason=MetadataAccess.Reason(e); }
        return (result, issues, missingReason);
    }
}

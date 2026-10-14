using System.Diagnostics;
using System.Management;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public sealed class ProcessMetadataReader
{
    private readonly Dictionary<string, (DateTime Stamp, ExecutableMetadata Value)> cache = new(StringComparer.OrdinalIgnoreCase);
    public ExecutableMetadata Read(string name, string? path)
    {
        if (path is null) return new(name);
        try
        {
            var stamp = File.GetLastWriteTimeUtc(path);
            if (cache.TryGetValue(path, out var value) && value.Stamp == stamp) return value.Value;
            var version = FileVersionInfo.GetVersionInfo(path);
            var metadata = new ExecutableMetadata(name, path, Version: version.FileVersion,
                Product: version.ProductName, Company: version.CompanyName,Description:version.FileDescription,OriginalName:version.OriginalFilename);
            cache[path] = (stamp, metadata);
            return metadata;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { return new(name, path); }
    }
    public static Dictionary<int, string> ReadCommandLines()
    {
        var result = new Dictionary<int, string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process");
            searcher.Options.Timeout = TimeSpan.FromSeconds(3);
            using var objects = searcher.Get();
            foreach (ManagementObject item in objects)
                using (item) if (item["CommandLine"] is string command)
                    result[Convert.ToInt32(item["ProcessId"])] = command;
        }
        catch (Exception e) when (e is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { }
        return result;
    }
}

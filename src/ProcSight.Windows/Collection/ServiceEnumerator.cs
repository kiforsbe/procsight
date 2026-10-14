using System.Management;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public static class ServiceEnumerator
{
    public static IReadOnlyList<ServiceSnapshot> Read()
    {
        var services = new List<ServiceSnapshot>();
        var reader=new ProcessMetadataReader();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, State, StartMode, ProcessId, PathName FROM Win32_Service");
            searcher.Options.Timeout = TimeSpan.FromSeconds(3);
            using var objects = searcher.Get();
            foreach (ManagementObject service in objects)
                using (service) if(ReadRow(property=>service[property], reader) is { } row) services.Add(row);
        }
        catch (Exception e) when (MetadataAccess.IsUnavailable(e)) { }
        return services;
    }
    /// <summary>Reads fields independently so restricted metadata does not discard a known service.</summary>
    public static ServiceSnapshot? ReadRow(Func<string, object?> readProperty, ProcessMetadataReader? reader=null)
    {
        reader ??=new();
        var issues=new List<MetadataIssue>();
        object? Read(string property)
        {
            try { return readProperty(property); }
            catch(Exception e) when(MetadataAccess.IsUnavailable(e))
            { issues.Add(new(property,MetadataAccess.Reason(e))); return null; }
        }
        var name=Read("Name")?.ToString();
        if(string.IsNullOrWhiteSpace(name)) return null;
        var display=Read("DisplayName")?.ToString() ?? name;
        var state=Read("State")?.ToString() ?? "Unknown";
        var start=Read("StartMode")?.ToString() ?? "Unknown";
        var pid=Read("ProcessId") is { } value?Convert.ToInt32(value):0;
        var configuration=Read("PathName")?.ToString();
        if(string.IsNullOrWhiteSpace(configuration) && !issues.Any(i=>i.Field=="PathName"))
            issues.Add(new("PathName",MetadataUnavailableReason.NotProvided));
        var implementation=ServiceBinaryReader.Implementation(name,configuration,reader,out var implementationReason);
        if(implementationReason is { } reason) issues.Add(new("ServiceImplementation",reason));
        var host=ServiceBinaryReader.ExecutablePath(configuration) is { } path?reader.Read(Path.GetFileName(path),path):null;
        return new(name,display,state,start,pid>0?pid:null,configuration,
            issues.Count==0?ObservationState.Available:ObservationState.Unavailable,implementation,host,issues.ToArray());
    }
}

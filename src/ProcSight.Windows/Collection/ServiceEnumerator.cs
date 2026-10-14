using System.Management;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public static class ServiceEnumerator
{
    public static IReadOnlyList<ServiceSnapshot> Read()
    {
        var services = new List<ServiceSnapshot>();
        var reader=new ProcessMetadataReader();
        using var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, State, StartMode, ProcessId, PathName FROM Win32_Service");
        searcher.Options.Timeout = TimeSpan.FromSeconds(3);
        try
        {
            using var objects = searcher.Get();
            foreach (ManagementObject service in objects)
                using (service) services.Add(new(service["Name"]?.ToString() ?? "", service["DisplayName"]?.ToString() ?? "",
                    service["State"]?.ToString() ?? "Unknown", service["StartMode"]?.ToString() ?? "Unknown",
                    Convert.ToInt32(service["ProcessId"]) is var pid && pid > 0 ? pid : null,
                    service["PathName"]?.ToString(), ObservationState.Available,
                    ServiceBinaryReader.Implementation(service["Name"]?.ToString() ?? "",service["PathName"]?.ToString(),reader),
                    ServiceBinaryReader.ExecutablePath(service["PathName"]?.ToString()) is { } path?reader.Read(Path.GetFileName(path),path):null));
        }
        catch (Exception e) when (e is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { }
        return services;
    }
}

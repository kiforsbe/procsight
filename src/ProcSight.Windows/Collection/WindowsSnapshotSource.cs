using ProcSight.Core.Models;
using System.Diagnostics;
namespace ProcSight.Windows.Collection;
public sealed class WindowsSnapshotSource : IWindowsSnapshotSource
{
    private readonly ProcessSampler sampler = new();
    private readonly SemaphoreSlim captureGate = new(1);
    private readonly ProcessMetadataReader metadata = new();
    private IReadOnlyList<ServiceSnapshot> services = [];
    private DateTimeOffset servicesAt;
    public async Task<MachineSnapshot> CaptureAsync(CommandLineMode mode, CancellationToken ct)
    {
        await captureGate.WaitAsync(ct);
        try { return await Task.Run(() => Capture(mode, ct), ct); }
        finally { captureGate.Release(); }
    }
    private MachineSnapshot Capture(CommandLineMode mode, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        if (now - servicesAt > TimeSpan.FromSeconds(10))
        { services = ServiceEnumerator.Read(); servicesAt = now; }
        var parents = WindowsNativeMethods.ReadParents();
        var commands = mode == CommandLineMode.Off ? new Dictionary<int, string>() : ProcessMetadataReader.ReadCommandLines();
        var rows = new List<ProcessSnapshot>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                ct.ThrowIfCancellationRequested();
                var pid = process.Id;
                var started = Try<DateTimeOffset>(() => process.StartTime.ToUniversalTime());
                var key = new ProcessKey(pid, started, started is null ? id : null);
                var name = "Unknown";
                try { name = process.ProcessName + ".exe"; } catch (InvalidOperationException) { }
                var path = WindowsNativeMethods.ReadImagePath(pid);
                var executable = metadata.Read(name, path);
                var sample = sampler.Sample(key, Try(() => process.TotalProcessorTime),
                    Try(() => process.WorkingSet64), now, Environment.ProcessorCount);
                rows.Add(new(key, parents.GetValueOrDefault(pid), executable, sample,
                    services.Where(s => s.HostPid == pid).Select(s => s.Name).ToArray(),
                    commands.TryGetValue(pid, out var command) ? new(command, now) : null));
            }
        }
        sampler.Prune(rows.Select(r => r.Key).ToHashSet());
        return new(id, now, rows, services);
    }
    private static T? Try<T>(Func<T> read) where T : struct
    {
        try { return read(); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        { return null; }
    }
}

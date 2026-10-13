using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
public interface IWindowsSnapshotSource
{
    Task<MachineSnapshot> CaptureAsync(CommandLineMode mode, CancellationToken ct);
}

using System.ComponentModel;
using ProcSight.Core.Models;
namespace ProcSight.App.ViewModels;
public sealed class ProcessRow : INotifyPropertyChanged
{
    public string Key { get; init; } = "";
    public ResolvedEntity Entity { get; private set; } = null!;
    public string Name => Entity.DisplayName;
    public string Owner => Entity.Owner ?? "Unknown ownership";
    public string Pid => Entity.Process?.Key.Pid.ToString() ?? Entity.Service?.HostPid?.ToString() ?? "—";
    public string Cpu => Entity.Process?.Resources.CpuPercent.Value is { } cpu ? $"{cpu:F1}%" : "—";
    public string Memory => Entity.Process?.Resources.WorkingSetBytes.Value is { } memory ? $"{memory / 1048576.0:F1} MB" : "—";
    public string Status => Entity.Service?.State ?? "Local metadata";
    public void Update(ResolvedEntity entity) { Entity = entity; PropertyChanged?.Invoke(this, new(null)); }
    public event PropertyChangedEventHandler? PropertyChanged;
}

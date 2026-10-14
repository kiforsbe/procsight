using System.ComponentModel;
using ProcSight.Core.Models;
using ProcSight.Application.Presentation;
using Microsoft.UI.Xaml.Media;
namespace ProcSight.App.ViewModels;
public sealed class ProcessRow : INotifyPropertyChanged
{
    public string Key { get; init; } = "";
    public ResolvedEntity Entity { get; private set; } = null!;
    public ProcessListRow Projection { get; private set; } = null!;
    public bool IsGroup => Projection.IsGroup;
    public bool Expanded { get; set; }
    public long? TotalMemoryBytes { get; set; }
    public string ServiceState=>Entity.Service?.State ?? "—";
    public string Startup=>Entity.Service?.StartMode ?? "—";
    public Brush CpuBrush=>Heat(Projection.CpuPercent);
    public Brush MemoryBrush=>Heat(TotalMemoryBytes is >0?Projection.MemoryBytes/(double)TotalMemoryBytes.Value*100:0);
    private static Brush Heat(double percent)
    {
        var color=new global::Windows.UI.ViewManagement.UISettings().GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Accent);
        return new SolidColorBrush(global::Windows.UI.Color.FromArgb((byte)Math.Clamp(18+percent*.8,18,88),color.R,color.G,color.B));
    }
    public string Name => IsGroup?$"{(Expanded?"▼":"▶")} {Projection.GroupName} ({Projection.Members.Count})":
        new string(' ',Projection.Depth*2)+(Projection.Depth>0?"↳ ":"")+(analysis?.Result.IdentityLabel ?? Entity.DisplayName);
    public string Owner => IsGroup?"Owner candidate · select to expand":analysis?.Result.Role is { } analyzedRole?
        $"{Entity.Owner ?? Entity.Process?.Executable.Name ?? "Unknown owner"} · {analyzedRole}":Entity.Facts.TryGetValue("commandRole",out var role)?
        $"{Entity.Owner ?? "Unknown owner"} · {role}":Entity.Owner ?? "Unknown ownership";
    public string Pid => IsGroup?"—":Entity.Process?.Key.Pid.ToString() ?? Entity.Service?.HostPid?.ToString() ?? "—";
    public string Cpu => Projection.Members.Any(e=>e.Process?.Resources.CpuPercent.Value is not null)?
        $"{(Projection.CompleteCpu?"":"≥")}{Projection.CpuPercent:F1}%":"—";
    public string Memory => Projection.Members.Any(e=>e.Process?.Resources.WorkingSetBytes.Value is not null)?
        $"{(Projection.CompleteMemory?"":"≥")}{Projection.MemoryBytes/1048576.0:F1} MB":"—";
    public string Status
    {
        get
        {
            if(analysis is null) return Entity.Service?.State ?? "Unknown";
            var concern=analysis.Result.Assessments.FirstOrDefault(a=>a.Value is "Suspicious" or "KnownThreat" or "PotentiallyUnwanted" or "PossibleLeftover");
            var value=concern?.Value ?? analysis.Result.Assessments.FirstOrDefault(a=>a.Axis==AssessmentAxis.Purpose)?.Value ?? analysis.State.ToString();
            return System.Text.RegularExpressions.Regex.Replace(value,"([a-z])([A-Z])","$1 $2")+(concern is null?"":$" ({concern.Confidence})");
        }
    }
    public override string ToString() => Name;
    private AnalysisUpdate? analysis;
    public string Classification => analysis is null ? "Unknown" : string.Join(" / ",analysis.Result.Assessments.Select(a=>a.Value));
    public void SetAnalysis(AnalysisUpdate value) { analysis=value; PropertyChanged?.Invoke(this,new(null)); }
    public void Update(ProcessListRow projection)
    {
        if(Entity is not null&&Entity.Identity!=projection.Entity.Identity) analysis=null;
        Projection=projection; Entity=projection.Entity; PropertyChanged?.Invoke(this,new(null));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

using System.ComponentModel;
using ProcSight.Core.Models;
using ProcSight.Application.Presentation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
namespace ProcSight.App.ViewModels;
public sealed class ProcessRow : INotifyPropertyChanged
{
    public string Key { get; init; } = "";
    public ResolvedEntity Entity { get; private set; } = null!;
    public ProcessListRow Projection { get; private set; } = null!;
    public bool IsGroup => Projection.IsGroup;
    public ImageSource? Icon { get; private set; }
    public Visibility FallbackIconVisibility=>Icon is null?Visibility.Visible:Visibility.Collapsed;
    public Visibility ExpanderVisibility=>Projection.HasChildren?Visibility.Visible:Visibility.Collapsed;
    public string ExpansionGlyph=>Projection.Expanded?"\uE70D":"\uE76C";
    public string ExpansionAction=>(Projection.Expanded?"Collapse ":"Expand ")+Name;
    public Thickness Indent=>new(8+Projection.Depth*14,0,8,0);
    public void SetIcon(ImageSource icon) { Icon=icon; Changed(nameof(Icon)); Changed(nameof(FallbackIconVisibility)); }
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
    private RoleLabel LocalRole=>ProcessRoleLabels.Describe(Entity);
    public string Name => IsGroup?$"{Projection.GroupName} ({Projection.Members.Count})":
        ProcessRoleLabels.DisplayName(Entity,analysis?.Result.IdentityLabel);
    public string Owner=>!IsGroup&&Entity.Process?.Windows is { Count:>0 }?Name+"\n"+OwnerExplanation+
        "\nVisible-window captions are local observations, not identity proof.":OwnerExplanation;
    private string OwnerExplanation => IsGroup?"Owner candidate · use the chevron to expand or collapse":LocalRole.UsesCommandContext?
        LocalRole.Label+"\nOwner candidate: "+(Entity.Owner ?? "Unknown")+"\n"+LocalRole.Explanation:analysis?.Result.Role is { } analyzedRole?
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
    public override string ToString() => "ProcessRow ["+Key+"]";
    private AnalysisUpdate? analysis;
    public string Classification => analysis is null ? "Unknown" : string.Join(" / ",analysis.Result.Assessments.Select(a=>a.Value));
    private static readonly string[] Fields=[nameof(Name),nameof(Owner),nameof(Pid),nameof(Cpu),nameof(Memory),nameof(Status),nameof(ServiceState),nameof(Startup),nameof(ExpansionGlyph),nameof(ExpansionAction),nameof(ExpanderVisibility),nameof(Indent)];
    private string[] Values()=>[Name,Owner,Pid,Cpu,Memory,Status,ServiceState,Startup,ExpansionGlyph,ExpansionAction,ExpanderVisibility.ToString(),Indent.ToString()];
    private void Changed(string field)=>PropertyChanged?.Invoke(this,new(field));
    private void Notify(string[]? before) {
        var after=Values();
        for(var i=0;i<Fields.Length;i++) if(before is null||before[i]!=after[i]) Changed(Fields[i]);
        if(before is null||before[3]!=after[3]) Changed(nameof(CpuBrush));
        if(before is null||before[4]!=after[4]) Changed(nameof(MemoryBrush));
    }
    public void SetAnalysis(AnalysisUpdate value) { var before=Values(); analysis=value; Notify(before); }
    public void Update(ProcessListRow projection)
    {
        var before=Projection is null?null:Values();
        if(Entity is not null&&(Entity.Process?.Executable.FullPath!=projection.Entity.Process?.Executable.FullPath || Entity.Process?.Executable.FileIdentity!=projection.Entity.Process?.Executable.FileIdentity))
        { Icon=null; Changed(nameof(Icon)); Changed(nameof(FallbackIconVisibility)); }
        if(Entity is not null&&Entity.Identity!=projection.Entity.Identity) analysis=null;
        Projection=projection; Entity=projection.Entity; Notify(before);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

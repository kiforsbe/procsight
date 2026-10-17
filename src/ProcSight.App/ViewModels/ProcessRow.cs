using System.ComponentModel;
using ProcSight.Core.Models;
using ProcSight.Application.Presentation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
namespace ProcSight.App.ViewModels;
public sealed partial class ProcessRow : INotifyPropertyChanged
{
    public string Key { get; init; } = "";
    public ResolvedEntity Entity { get; private set; } = null!;
    public ProcessListRow Projection { get; private set; } = null!;
    public bool IsGroup => Projection.IsGroup;
    private IReadOnlyList<TableColumnWidth> tableColumns=[];
    public IReadOnlyList<TableColumnWidth> TableColumns { get=>tableColumns; set { tableColumns=value; Changed(nameof(TableColumns)); } }
    public string NativeName=>LocalValue("NativeName");
    public string CommandLine=>LocalValue("CommandLine");
    public string Path=>LocalValue("Path");
    public string ParentPid=>LocalValue("ParentPid");
    public string Publisher=>LocalValue("Publisher");
    public string Signature=>LocalValue("Signature");
    private string? startedDisplay;
    private DateOnly startedDisplayDay;
    public string Started {
        get {
            if(IsGroup) return "—";
            var now=DateTimeOffset.Now; var day=DateOnly.FromDateTime(now.DateTime);
            if(startedDisplay is null||startedDisplayDay!=day) {
                startedDisplay=ProcessTimePresentation.Started(Entity.Process?.Key.StartedAt,now); startedDisplayDay=day;
            }
            return startedDisplay;
        }
    }
    public string StartedDetails=>LocalValue("Started");
    public string Ownership=>LocalValue("Owner");
    public string Windows=>LocalValue("Windows");
    public string ServiceName=>LocalValue("ServiceName");
    private IEnumerable<Observation<double>> Io(bool network)=>Projection.Members.Where(e=>e.Process is not null)
        .Select(e=>network?e.Process!.Resources.NetworkBytesPerSecond:e.Process!.Resources.DiskBytesPerSecond);
    public string Disk=>ResourceRatePresentation.Text(Io(false));
    public string Network=>ResourceRatePresentation.Text(Io(true),true);
    public string DiskDetails=>ResourceRatePresentation.Details(Io(false));
    public string NetworkDetails=>ResourceRatePresentation.Details(Io(true));
    private IEnumerable<Observation<double>> GpuObservations=>Projection.Members.Where(e=>e.Process is not null)
        .Select(e=>e.Process!.Resources.GpuPercent??Observation<double>.Missing("GPU sample unavailable"));
    public string Gpu=>ResourceRatePresentation.GpuText(GpuObservations);
    public string GpuDetails=>GpuObservations.Any(v=>v.State!=ObservationState.Available)?ResourceRatePresentation.Details(GpuObservations):
        "Busiest reported GPU engine. Application groups show the busiest member, not a sum across unrelated engines.";
    public string Availability=>LocalValue("Availability");
    private IReadOnlyDictionary<string,string?>? localValues;
    private string LocalValue(string id)=>IsGroup?"—":(localValues??=TableValues.Read(Entity,analysis)).GetValueOrDefault(id) is { Length:>0 } text?text:"—";
    public ImageSource? Icon { get; private set; }
    public Visibility FallbackIconVisibility=>Icon is null?Visibility.Visible:Visibility.Collapsed;
    public Visibility ExpanderVisibility=>Projection.HasChildren?Visibility.Visible:Visibility.Collapsed;
    public string ExpansionGlyph=>Projection.Expanded?"\uE70D":"\uE76C";
    public string ExpansionAction=>(Projection.Expanded?"Collapse ":"Expand ")+Name;
    public Thickness Indent=>new(8+Projection.Depth*14,0,8,0);
    public void SetIcon(ImageSource icon) { Icon=icon; Changed(nameof(Icon)); Changed(nameof(FallbackIconVisibility)); }
    private long? totalMemoryBytes;
    public long? TotalMemoryBytes { get=>totalMemoryBytes; set {
        if(totalMemoryBytes==value) return;
        totalMemoryBytes=value;
        if(Projection is not null) Changed(nameof(MemoryBrush));
    } }
    public string ServiceState=>Entity.Service?.State ?? "—";
    public string Startup=>Entity.Service?.StartMode ?? "—";
    private int minimumShadingPercent=5;
    private bool usageHighContrast;
    public void ConfigureUsageShading(int minimumPercent,bool highContrast) {
        if(minimumShadingPercent==minimumPercent&&usageHighContrast==highContrast) return;
        minimumShadingPercent=minimumPercent; usageHighContrast=highContrast;
        Changed(nameof(CpuBrush)); Changed(nameof(MemoryBrush));
    }
    public Brush CpuBrush=>Heat(Projection.CpuPercent,Projection.Members.Any(m=>m.Process?.Resources.CpuPercent.Value is not null));
    public Brush MemoryBrush=>Heat(TotalMemoryBytes is >0?Projection.MemoryBytes/(double)TotalMemoryBytes.Value*100:0,
        TotalMemoryBytes is >0&&Projection.Members.Any(m=>m.Process?.Resources.WorkingSetBytes.Value is not null));
    private Brush Heat(double percent,bool available)
    {
        var color=new global::Windows.UI.ViewManagement.UISettings().GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Accent);
        var floor=255*minimumShadingPercent/100d;
        var alpha=available&&!usageHighContrast&&double.IsFinite(percent)?
            (byte)Math.Clamp(Math.Round(floor+(88-floor)*Math.Clamp(percent,0,100)/100),0,88):(byte)0;
        return new SolidColorBrush(global::Windows.UI.Color.FromArgb(alpha,color.R,color.G,color.B));
    }
    private RoleLabel? localRole;
    private string? name,owner,status;
    private RoleLabel LocalRole=>localRole??=ProcessRoleLabels.Describe(Entity);
    public string Name => IsGroup?$"{Projection.GroupName} ({Projection.Members.Count})":
        name??=ProcessRoleLabels.DisplayName(Entity,analysis?.Result.IdentityLabel);
    public string Owner=>IsGroup?OwnerExplanation:owner??=(Entity.Process?.Windows is { Count:>0 }?Name+"\n"+OwnerExplanation+
        "\nVisible-window captions are local observations, not identity proof.":OwnerExplanation);
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
        get=>status??=ReadStatus();
    }
    private string ReadStatus()
        {
            if(analysis is null) return Entity.Service?.State ?? "Unknown";
            var concern=analysis.Result.Assessments.FirstOrDefault(a=>a.Value is "Suspicious" or "KnownThreat" or "PotentiallyUnwanted" or "PossibleLeftover");
            var value=concern?.Value ?? analysis.Result.Assessments.FirstOrDefault(a=>a.Axis==AssessmentAxis.Purpose)?.Value ?? analysis.State.ToString();
            return System.Text.RegularExpressions.Regex.Replace(value,"([a-z])([A-Z])","$1 $2")+(concern is null?"":$" ({concern.Confidence})");
    }
    public override string ToString() => "ProcessRow ["+Key+"]";
    private AnalysisUpdate? analysis;
    public string Classification => analysis is null ? "Unknown" : string.Join(" / ",analysis.Result.Assessments.Select(a=>a.Value));
    private static readonly string[] Fields=[nameof(Name),nameof(Owner),nameof(Pid),nameof(Cpu),nameof(Memory),nameof(Status),nameof(ServiceState),nameof(Startup),nameof(ExpansionGlyph),nameof(ExpansionAction),nameof(ExpanderVisibility),nameof(Indent),nameof(NativeName),nameof(CommandLine),nameof(Path),nameof(ParentPid),nameof(Publisher),nameof(Signature),nameof(Started),nameof(Ownership),nameof(Windows),nameof(ServiceName),nameof(Availability),nameof(Disk),nameof(Network),nameof(DiskDetails),nameof(NetworkDetails),nameof(Gpu),nameof(GpuDetails),nameof(StartedDetails)];
    private string[] Values()=>[Name,Owner,Pid,Cpu,Memory,Status,ServiceState,Startup,ExpansionGlyph,ExpansionAction,ExpanderVisibility.ToString(),Indent.ToString(),NativeName,CommandLine,Path,ParentPid,Publisher,Signature,Started,Ownership,Windows,ServiceName,Availability,Disk,Network,DiskDetails,NetworkDetails,Gpu,GpuDetails,StartedDetails];
    private void Changed(string field)=>PropertyChanged?.Invoke(this,new(field));
    private string[]? displayedValues;
    private void Notify(string[]? before,bool cpuChanged=false,bool memoryChanged=false) {
        var after=Values();
        displayedValues=after;
        for(var i=0;i<Fields.Length;i++) if(before is null||before[i]!=after[i]) Changed(Fields[i]);
        if(before is null||cpuChanged||before[3]!=after[3]) Changed(nameof(CpuBrush));
        if(before is null||memoryChanged||before[4]!=after[4]) Changed(nameof(MemoryBrush));
    }
    private void InvalidateMetadata() { name=null; owner=null; status=null; localRole=null; localValues=null; startedDisplay=null; }
    public void SetAnalysis(AnalysisUpdate value) {
        if(ReferenceEquals(analysis,value)) return;
        var before=displayedValues; analysis=value; InvalidateMetadata(); Notify(before);
    }
    // Snapshots are replaced on collection. Resource samples and evidence timestamps do not change
    // local presentation; compare its actual inputs before rerunning command/role/table projection.
    private static bool SameMetadata(ResolvedEntity left,ResolvedEntity right)
    {
        if(ReferenceEquals(left,right)) return true;
        if(left.Identity!=right.Identity||left.DisplayName!=right.DisplayName||left.Owner!=right.Owner||
            left.Facts.GetValueOrDefault("commandRole")!=right.Facts.GetValueOrDefault("commandRole")||left.Service!=right.Service) return false;
        var a=left.Process; var b=right.Process;
        if(a is null||b is null) return a is null&&b is null;
        return a.Key==b.Key&&a.ParentPid==b.ParentPid&&a.Executable==b.Executable&&a.CommandLine?.Raw==b.CommandLine?.Raw&&
            SameItems(a.ServiceNames,b.ServiceNames)&&SameItems(a.Windows,b.Windows)&&SameItems(a.MetadataIssues,b.MetadataIssues);
    }
    private static bool SameItems<T>(IReadOnlyList<T>? left,IReadOnlyList<T>? right)
    {
        if(ReferenceEquals(left,right)) return true;
        if(left is null||right is null||left.Count!=right.Count) return false;
        for(var i=0;i<left.Count;i++) if(!EqualityComparer<T>.Default.Equals(left[i],right[i])) return false;
        return true;
    }
    public void Update(ProcessListRow projection)
    {
        if(ReferenceEquals(Projection,projection)) return;
        var before=displayedValues;
        var cpuChanged=Projection?.CpuPercent!=projection.CpuPercent;
        var memoryChanged=Projection?.MemoryBytes!=projection.MemoryBytes;
        var metadataChanged=Entity is null||!SameMetadata(Entity,projection.Entity);
        if(Entity is not null&&ProcessIconSource.Select(Entity)!=ProcessIconSource.Select(projection.Entity)&&
            !StringComparer.OrdinalIgnoreCase.Equals(ProcessIconSource.Key(Entity),ProcessIconSource.Key(projection.Entity)))
        { Icon=null; Changed(nameof(Icon)); Changed(nameof(FallbackIconVisibility)); }
        if(Entity is not null&&Entity.Identity!=projection.Entity.Identity) analysis=null;
        if(metadataChanged) InvalidateMetadata();
        Projection=projection; Entity=projection.Entity; Notify(before,cpuChanged,memoryChanged);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

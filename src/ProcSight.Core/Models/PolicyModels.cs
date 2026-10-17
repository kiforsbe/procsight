namespace ProcSight.Core.Models;
public enum CommandLineMode { Off, LocalOnly, OnlineFull }
public sealed record UsageLimits(int AutomaticJobs = 20, int BillableCalls = 60);
public enum UsageKind { AutomaticJob, BillableCall }
public sealed record ProviderSettings(string Id, Uri Endpoint, string Model, bool StructuredOutput = true,string? AccountId=null,string? CredentialId=null);
public sealed record AnalysisPolicy(bool AutomaticEnabled = false, bool ResearchEnabled = false,
    CommandLineMode CommandLineMode = CommandLineMode.Off, ProviderSettings? Provider = null,
    ProviderSettings? LocalModel = null, UsageLimits? Limits = null, long Revision = 0,bool ShareIdentifyingMetadata=false,
    ConnectionProfile[]? Connections=null,bool FallbackEnabled=false,string? ResearchProfileId=null,
    [property:System.Text.Json.Serialization.JsonIgnore] bool ExternalAnalysisAllowed=true);
public sealed record TableColumnPreference(string Id,double Width);
public enum ResourceShadingMode { Off,RecentChanges,CurrentActivity,RecentSpikes,TopConsumers }
public sealed record ResourceShadingOptions(ResourceShadingMode Disk=ResourceShadingMode.RecentChanges,
    ResourceShadingMode Network=ResourceShadingMode.RecentChanges,ResourceShadingMode Gpu=ResourceShadingMode.RecentChanges,
    int FadeSeconds=5,int IntensityPercent=50)
{
    public void Validate() {
        if(!Enum.IsDefined(Disk)||!Enum.IsDefined(Network)||!Enum.IsDefined(Gpu)||FadeSeconds is <1 or >30||IntensityPercent is <0 or >100)
            throw new ArgumentException("Invalid resource shading preferences.");
    }
}
public sealed record PresentationPreferences(string Theme = "System", bool Grouped = true,
    string Sort = "Name", string Page = "Processes",bool SortDescending=false,
    bool ShowTrayIcon=false,TrayMetric TrayMetric=TrayMetric.Cpu,bool MinimizeToTray=false,
    TableColumnPreference[]? ProcessColumns=null,TableColumnPreference[]? ServiceColumns=null,
    ResourceShadingOptions? ResourceShading=null,bool AutomaticProcessSorting=false);

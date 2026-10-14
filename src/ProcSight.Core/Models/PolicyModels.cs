namespace ProcSight.Core.Models;
public enum CommandLineMode { Off, LocalOnly, OnlineFull }
public sealed record UsageLimits(int AutomaticJobs = 20, int BillableCalls = 60);
public enum UsageKind { AutomaticJob, BillableCall }
public sealed record ProviderSettings(string Id, Uri Endpoint, string Model, bool StructuredOutput = true);
public sealed record AnalysisPolicy(bool AutomaticEnabled = false, bool ResearchEnabled = false,
    CommandLineMode CommandLineMode = CommandLineMode.Off, ProviderSettings? Provider = null,
    ProviderSettings? LocalModel = null, UsageLimits? Limits = null, long Revision = 0,bool ShareIdentifyingMetadata=false);
public sealed record PresentationPreferences(string Theme = "System", bool Grouped = true,
    string Sort = "Name", string Page = "Processes",bool SortDescending=false);

using System.Text.Json.Serialization;
namespace ProcSight.Core.Models;
public enum ObservationState { Available, Pending, Unavailable }
public sealed record Observation<T>(T? Value, ObservationState State, string? Reason = null) where T : struct
{
    public static Observation<T> Available(T value) => new(value, ObservationState.Available);
    public static Observation<T> Missing(string reason) => new(null, ObservationState.Unavailable, reason);
    public static Observation<T> Pending() => new(null, ObservationState.Pending);
}
public enum SignatureStatus { Unavailable, Unsigned, Invalid, Verified }
public enum MetadataUnavailableReason { AccessDenied, ProcessExited, NotFound, ReadFailed, NotProvided }
public sealed record MetadataIssue(string Field, MetadataUnavailableReason Reason);
public sealed record ExecutableMetadata(string Name, string? FullPath = null,
    string? ContentHash = null, string? Version = null, string? Product = null,
    string? Company = null, SignatureStatus Signature = SignatureStatus.Unavailable,
    string? VerifiedSigner = null, string? FileIdentity = null, string? PackageIdentity = null,string? Description=null,string? OriginalName=null,
    [property: JsonIgnore] IReadOnlyList<MetadataIssue>? MetadataIssues=null);
public sealed record ResourceSample(Observation<double> CpuPercent,
    Observation<long> WorkingSetBytes, Observation<double> DiskBytesPerSecond,
    Observation<double> NetworkBytesPerSecond, DateTimeOffset SampledAt, TimeSpan? Interval,
    Observation<double>? GpuPercent=null);
public sealed record CommandLineValue(string Raw, DateTimeOffset CollectedAt)
{
    public override string ToString() => "CommandLineValue [private transient data]";
}
public sealed record ProcessSnapshot(ProcessKey Key, int? ParentPid, ExecutableMetadata Executable,
    ResourceSample Resources, IReadOnlyList<string> ServiceNames,
    [property: JsonIgnore] CommandLineValue? CommandLine = null,
    [property: JsonIgnore] IReadOnlyList<ProcessWindow>? Windows=null,
    [property: JsonIgnore] IReadOnlyList<MetadataIssue>? MetadataIssues=null);
public sealed record ProcessWindow([property: JsonIgnore] string Title,[property: JsonIgnore] string ClassName,bool Foreground=false,bool Minimized=false)
{
    public override string ToString()=>"ProcessWindow [private transient caption]";
}
public sealed record ServiceSnapshot(string Name, string DisplayName, string State,
    string StartMode, int? HostPid, [property: JsonIgnore] string? BinaryPath, ObservationState AccessState,
    ExecutableMetadata? Implementation=null, ExecutableMetadata? HostExecutable=null,
    [property: JsonIgnore] IReadOnlyList<MetadataIssue>? MetadataIssues=null)
{
    public override string ToString()=>$"ServiceSnapshot [{Name}, {AccessState}; command configuration private]";
}
public sealed record MachineSnapshot(Guid Id, DateTimeOffset CapturedAt,
    IReadOnlyList<ProcessSnapshot> Processes, IReadOnlyList<ServiceSnapshot> Services,SystemResourceSample? SystemResources=null);
public sealed record SystemResourceSample(double? CpuPercent,long? TotalPhysicalBytes,long? AvailablePhysicalBytes)
{
    public double? MemoryPercent=>TotalPhysicalBytes is >0&&AvailablePhysicalBytes is { } available?
        (1-available/(double)TotalPhysicalBytes.Value)*100:null;
}

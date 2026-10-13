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
public sealed record ExecutableMetadata(string Name, string? FullPath = null,
    string? ContentHash = null, string? Version = null, string? Product = null,
    string? Company = null, SignatureStatus Signature = SignatureStatus.Unavailable,
    string? VerifiedSigner = null, string? FileIdentity = null, string? PackageIdentity = null);
public sealed record ResourceSample(Observation<double> CpuPercent,
    Observation<long> WorkingSetBytes, Observation<double> DiskBytesPerSecond,
    Observation<double> NetworkBytesPerSecond, DateTimeOffset SampledAt, TimeSpan? Interval);
public sealed record CommandLineValue(string Raw, DateTimeOffset CollectedAt)
{
    public override string ToString() => "CommandLineValue [private transient data]";
}
public sealed record ProcessSnapshot(ProcessKey Key, int? ParentPid, ExecutableMetadata Executable,
    ResourceSample Resources, IReadOnlyList<string> ServiceNames,
    [property: JsonIgnore] CommandLineValue? CommandLine = null);
public sealed record ServiceSnapshot(string Name, string DisplayName, string State,
    string StartMode, int? HostPid, string? BinaryPath, ObservationState AccessState);
public sealed record MachineSnapshot(Guid Id, DateTimeOffset CapturedAt,
    IReadOnlyList<ProcessSnapshot> Processes, IReadOnlyList<ServiceSnapshot> Services);

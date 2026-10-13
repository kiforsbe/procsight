namespace ProcSight.Core.Models;
public sealed record ProcessKey
{
    public int Pid { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public Guid? SnapshotScope { get; init; }
    public ProcessKey(int pid, DateTimeOffset? startedAt, Guid? snapshotScope)
    {
        if (pid < 0 || (startedAt is null && snapshotScope is null))
            throw new ArgumentException("A process needs a PID and creation time or snapshot scope.");
        Pid = pid; StartedAt = startedAt; SnapshotScope = snapshotScope;
    }
}
public sealed record AnalysisIdentity(string Value);
public sealed record ResolvedEntity(AnalysisIdentity Identity, string DisplayName,
    string? Owner, ProcessSnapshot? Process, ServiceSnapshot? Service,
    Dictionary<string, string> Facts, List<EvidenceRecord> Evidence,
    HashSet<DataOrigin> Origins);

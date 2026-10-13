using ProcSight.Core.Models;
namespace ProcSight.Tests;
public static class Samples
{
    public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-13T10:00:00Z");
    public static ProcessSnapshot Process(int pid = 20, int? parent = null, string name = "node.exe",
        string? product = null, string? hash = "hash-a", DateTimeOffset? started = null) => new(
        new(pid, started ?? Now, null), parent, new(name, $@"C:\Program Files\Example\{name}", hash,
        Product: product, Company: "Example", Signature: SignatureStatus.Verified, VerifiedSigner: "Example signer"),
        new(Observation<double>.Pending(), Observation<long>.Available(1024),
            Observation<double>.Missing("Not collected"), Observation<double>.Missing("Not collected"), Now, null), []);
    public static MachineSnapshot Snapshot(params ProcessSnapshot[] processes) => new(Guid.NewGuid(), Now, processes, []);
}

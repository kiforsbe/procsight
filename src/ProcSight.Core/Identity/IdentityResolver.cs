using ProcSight.Core.Models;
using System.Security.Cryptography;
using System.Text;
namespace ProcSight.Core.Identity;
public sealed class IdentityResolver
{
    public IReadOnlyList<ResolvedEntity> Resolve(MachineSnapshot snapshot, IReadOnlyList<EvidenceRecord> evidence)
    {
        var byPid = snapshot.Processes.ToDictionary(p => p.Key.Pid);
        var result = new List<ResolvedEntity>();
        foreach (var process in snapshot.Processes)
        {
            var owner = process.Executable.Product;
            var cursor = process; var visited = new HashSet<int>();
            for (var depth = 0; depth < 8 && visited.Add(cursor.Key.Pid); depth++)
            {
                if (cursor.ParentPid is not { } pid || !byPid.TryGetValue(pid, out var parent) ||
                    !ParentLinkValidator.IsVerified(cursor, parent)) break;
                if (!string.IsNullOrWhiteSpace(parent.Executable.Product)) owner = parent.Executable.Product;
                cursor = parent;
            }
            var facts = Facts(process.Executable);
            if (!string.IsNullOrWhiteSpace(owner)) facts["owner"] = owner;
            if (process.ServiceNames.Count > 0) facts["service"] = string.Join("|", process.ServiceNames.Order());
            var key = process.Executable.ContentHash ?? process.Executable.FullPath ??
                $"unverified:{process.Key.Pid}:{process.Key.StartedAt}:{process.Key.SnapshotScope}";
            var identity = MakeIdentity($"{key}|{owner}|{facts.GetValueOrDefault("service")}");
            var observation = new EvidenceRecord("local:" + identity.Value, DataOrigin.ShareableMetadata,
                snapshot.CapturedAt, null, "LocalMetadata", facts);
            result.Add(new(identity, process.Executable.Product ?? process.Executable.Name, owner, process, null,
                facts, [observation, .. evidence], [DataOrigin.ShareableMetadata]));
        }
        foreach (var service in snapshot.Services)
        {
            var facts = new Dictionary<string,string> { ["service"] = service.Name, ["product"] = service.DisplayName };
            result.Add(new(MakeIdentity("service:" + service.Name + "|" + service.BinaryPath), service.DisplayName,
                null, null, service, facts, [new("service:" + service.Name, DataOrigin.ShareableMetadata,
                    snapshot.CapturedAt, null, "ServiceMetadata", facts)], [DataOrigin.ShareableMetadata]));
        }
        return result;
    }
    public static AnalysisIdentity MakeIdentity(string value) => new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))));
    private static Dictionary<string, string> Facts(ExecutableMetadata executable)
    {
        var facts = new Dictionary<string, string> { ["name"] = executable.Name };
        foreach (var (key, value) in new[] { ("path", executable.FullPath), ("hash", executable.ContentHash),
            ("product", executable.Product), ("version", executable.Version), ("package", executable.PackageIdentity) })
            if (!string.IsNullOrWhiteSpace(value)) facts[key] = value;
        if (executable.Signature == SignatureStatus.Verified && executable.VerifiedSigner is { } signer) facts["signer"] = signer;
        return facts;
    }
}

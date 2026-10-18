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
            var owner = string.IsNullOrWhiteSpace(process.Executable.Product)?null:process.Executable.Product;
            if(owner?.Contains("Windows",StringComparison.OrdinalIgnoreCase)==true)
                owner=string.IsNullOrWhiteSpace(process.Executable.Description)?process.Executable.Name:process.Executable.Description;
            var ownerPath=process.Executable.FullPath;
            var cursor = process; var visited = new HashSet<int>();
            for (var depth = 0; depth < 8 && visited.Add(cursor.Key.Pid); depth++)
            {
                if (cursor.ParentPid is not { } pid || !byPid.TryGetValue(pid, out var parent) ||
                    !ParentLinkValidator.IsVerified(cursor, parent)) break;
                if(parent.Executable.Name.ToLowerInvariant() is "explorer.exe" or "services.exe" or "svchost.exe" or
                    "wininit.exe" or "winlogon.exe" or "cmd.exe" or "powershell.exe" or "pwsh.exe" or "conhost.exe" or
                    "runtimebroker.exe" or "taskhostw.exe" or "dllhost.exe") break;
                if(parent.Executable.Product?.Contains("Windows",StringComparison.OrdinalIgnoreCase)==true) break;
                if (!string.IsNullOrWhiteSpace(parent.Executable.Product)) { owner = parent.Executable.Product; ownerPath=parent.Executable.FullPath; }
                cursor = parent;
            }
            var facts = Facts(process.Executable);
            if (!string.IsNullOrWhiteSpace(owner)) facts["owner"] = owner;
            if(owner is not null) facts["ownerIdentity"]=MakeIdentity(owner+"|"+ownerPath).Value;
            if (process.ServiceNames.Count > 0) facts["service"] = string.Join("|", process.ServiceNames.Order());
            var key = process.Executable.ContentHash ?? process.Executable.FullPath ??
                $"unverified:{process.Key.Pid}:{process.Key.StartedAt}:{process.Key.SnapshotScope}";
            var identity = MakeIdentity($"{key}|{process.Executable.FullPath}|{process.Executable.Signature}|{process.Executable.VerifiedSigner}|{facts.GetValueOrDefault("ownerIdentity")}|{facts.GetValueOrDefault("service")}");
            var observation = new EvidenceRecord("local:" + identity.Value, DataOrigin.ShareableMetadata,
                snapshot.CapturedAt, null, "LocalMetadata", facts);
            var display=!string.IsNullOrWhiteSpace(process.Executable.Description)?process.Executable.Description:
                string.IsNullOrWhiteSpace(process.Executable.Product)||process.Executable.Product.Contains("Windows",StringComparison.OrdinalIgnoreCase)?process.Executable.Name:process.Executable.Product;
            result.Add(new(identity, display, owner, process, null,
                facts, [observation, .. evidence], [DataOrigin.ShareableMetadata]));
        }
        foreach (var service in snapshot.Services)
        {
            var facts = service.Implementation is { } module?Facts(module):new Dictionary<string,string>();
            facts["service"]=service.Name;
            var identity=MakeIdentity("service:"+service.Name+"|"+service.HostExecutable?.FullPath+"|"+service.Implementation?.FullPath+
                "|"+service.Implementation?.ContentHash+"|"+service.Implementation?.Signature+"|"+service.Implementation?.VerifiedSigner);
            result.Add(new(identity, service.DisplayName,
                null, null, service, facts, [new("service:" + identity.Value, DataOrigin.ShareableMetadata,
                    snapshot.CapturedAt, null, "ServiceMetadata", facts)], [DataOrigin.ShareableMetadata]));
        }
        return result;
    }
    public static AnalysisIdentity MakeIdentity(string value) => new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))));
    private static Dictionary<string, string> Facts(ExecutableMetadata executable)
    {
        var facts = new Dictionary<string, string> { ["name"] = executable.Name };
        foreach (var (key, value) in new[] { ("path", executable.FullPath), ("hash", executable.ContentHash),
            ("product", executable.Product), ("version", executable.Version), ("package", executable.PackageIdentity), ("originalName",executable.OriginalName) })
            if (!string.IsNullOrWhiteSpace(value)) facts[key] = value;
        if (executable.Signature == SignatureStatus.Verified && executable.VerifiedSigner is { } signer) facts["signer"] = signer;
        return facts;
    }
}

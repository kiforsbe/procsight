using ProcSight.Core.Models;
using System.Security.Cryptography;
namespace ProcSight.Windows.Collection;
public sealed class ExecutableEnricher
{
    private readonly Dictionary<string,(string Stamp, ExecutableMetadata Value)> cache = new(StringComparer.OrdinalIgnoreCase);
    public bool TryGetCached(ExecutableMetadata metadata,out ExecutableMetadata result)
    {
        result=metadata;
        if(metadata.FullPath is not { } path) return false;
        try
        {
            var info=new FileInfo(path); var stamp=$"{info.Length}:{info.LastWriteTimeUtc.Ticks}:{info.CreationTimeUtc.Ticks}";
            if(!cache.TryGetValue(path,out var cached)||cached.Stamp!=stamp) return false;
            result=cached.Value with { Name=metadata.Name,Product=metadata.Product,Company=metadata.Company,
                Version=metadata.Version,PackageIdentity=metadata.PackageIdentity,Description=metadata.Description,OriginalName=metadata.OriginalName,
                MetadataIssues=MergeIssues(metadata,cached.Value) };
            return true;
        }
        catch(Exception e) when(MetadataAccess.IsUnavailable(e)) { return false; }
    }
    public async Task<ExecutableMetadata> EnrichAsync(ExecutableMetadata metadata,CancellationToken ct)
    {
        if (metadata.FullPath is not { } path) return metadata;
        try
        {
            var info = new FileInfo(path); var stamp = $"{info.Length}:{info.LastWriteTimeUtc.Ticks}:{info.CreationTimeUtc.Ticks}";
            if (cache.TryGetValue(path,out var cached) && cached.Stamp == stamp)
                return cached.Value with { Name=metadata.Name, Product=metadata.Product, Company=metadata.Company,
                    Version=metadata.Version, PackageIdentity=metadata.PackageIdentity,Description=metadata.Description,OriginalName=metadata.OriginalName,
                    MetadataIssues=MergeIssues(metadata,cached.Value) };
            await using var file = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete,65536,true);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(file,ct));
            ct.ThrowIfCancellationRequested();
            var signature = SignatureVerifier.VerifyWithReason(path);
            info.Refresh();
            if (stamp != $"{info.Length}:{info.LastWriteTimeUtc.Ticks}:{info.CreationTimeUtc.Ticks}") return metadata;
            var result = metadata with { ContentHash=hash, FileIdentity=stamp, Signature=signature.Status, VerifiedSigner=signature.Signer,
                MetadataIssues=signature.Reason is { } reason ? (metadata.MetadataIssues ?? []).Append(new("Signature",reason)).ToArray() : metadata.MetadataIssues };
            cache[path]=(stamp,result); return result;
        }
        catch (Exception e) when (MetadataAccess.IsUnavailable(e) || e is System.Security.Cryptography.CryptographicException)
        { return metadata with { MetadataIssues=(metadata.MetadataIssues ?? []).Concat([
            new MetadataIssue("ContentHash",MetadataAccess.Reason(e)), new MetadataIssue("Signature",MetadataAccess.Reason(e))]).ToArray() }; }
    }
    private static IReadOnlyList<MetadataIssue> MergeIssues(ExecutableMetadata current,ExecutableMetadata cached)
        => (current.MetadataIssues ?? []).Concat((cached.MetadataIssues ?? [])
            .Where(issue=>issue.Field is "ContentHash" or "Signature")).Distinct().ToArray();
}

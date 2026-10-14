using ProcSight.Core.Models;
namespace ProcSight.Core.Privacy;
public sealed class AnalysisProjection
{
    private static readonly HashSet<string> Allowed=["name","path","hash","product","version","package","signer","owner","ownerIdentity","service"];
    public static bool IdentifyingField(string field)=>field is "path" or "package" or "owner" or "ownerIdentity" or "service";
    public AnalysisPayload Build(ResolvedEntity entity,AnalysisPolicy policy,AnalysisDestination destination)
    {
        bool Permitted(string field)=>destination!=AnalysisDestination.CloudModel || policy.ShareIdentifyingMetadata || !IdentifyingField(field);
        var line=(destination != AnalysisDestination.CloudModel || policy.CommandLineMode==CommandLineMode.OnlineFull)
            ? entity.Process?.CommandLine?.Raw ?? entity.Service?.BinaryPath : null;
        var metadata=entity.Facts.Where(f=>Allowed.Contains(f.Key)&&Permitted(f.Key)).ToDictionary(f=>f.Key,f=>MetadataSanitizer.Normalize(f.Value));
        var evidence=entity.Evidence.Where(e=>destination != AnalysisDestination.CloudModel || OriginPolicy.CanShare(new HashSet<DataOrigin>{e.Origin},policy))
            .Select(e=>e with { Facts=e.Facts.Where(f=>Permitted(f.Key)&&(Allowed.Contains(f.Key)||f.Key is "claim" or "threatHash" or "commandRole"))
                .ToDictionary(f=>f.Key,f=>MetadataSanitizer.Normalize(f.Value)) }).ToList();
        if(line?.Length>32768) line=null;
        return new(destination==AnalysisDestination.CloudModel && policy.CommandLineMode!=CommandLineMode.OnlineFull?
            entity.ShareableIdentity ?? entity.Identity:entity.Identity,metadata,evidence,line,policy.Revision,destination);
    }
    public ResearchRequest BuildResearchRequest(ResolvedEntity entity,AnalysisPolicy? policy=null) => new(entity.Facts
        .Where(f=>(f.Key is "name" or "product" or "signer" or "hash" or "service") &&
            ((policy?.ShareIdentifyingMetadata ?? false)||!IdentifyingField(f.Key)))
        .ToDictionary(f=>f.Key,f=>MetadataSanitizer.Normalize(f.Value)));
}

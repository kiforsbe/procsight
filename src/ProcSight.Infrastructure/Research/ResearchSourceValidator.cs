using ProcSight.Core.Models;
using ProcSight.Core.Identity;
using ProcSight.Core.Privacy;
using System.Text.Json.Nodes;
namespace ProcSight.Infrastructure.Research;
public static class ResearchSourceValidator
{
    public static List<EvidenceRecord> Extract(string json,DateTimeOffset now)
    {
        var body=JsonNode.Parse(json)!; var result=new List<EvidenceRecord>();
        foreach(var item in body["output"]?.AsArray() ?? [])
            foreach(var content in item?["content"]?.AsArray() ?? [])
            {
                if(content?["type"]?.GetValue<string>()!="output_text") continue;
                var text=content["text"]?.GetValue<string>() ?? "";
                foreach(var annotation in content["annotations"]?.AsArray() ?? [])
                {
                    if(annotation?["type"]?.GetValue<string>()!="url_citation") continue;
                    var url=annotation["url"]?.GetValue<string>();
                    if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0) continue;
                    var start=annotation["start_index"]?.GetValue<int>() ?? 0;
                    var end=annotation["end_index"]?.GetValue<int>() ?? text.Length;
                    if(start<0||end<start||end>text.Length) continue;
                    var claim=text[start..end]; if(claim.Length>8192) claim=claim[..8192];
                    result.Add(new("web:"+IdentityResolver.MakeIdentity(url+"|"+claim).Value,DataOrigin.Research,now,
                        now.AddDays(7),"Research",new(){["claim"]=MetadataSanitizer.Normalize(claim)},url));
                    if(result.Count>=10) return result;
                }
            }
        return result.DistinctBy(e=>e.Id).ToList();
    }
}

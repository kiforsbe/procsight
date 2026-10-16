using System.Text.Json.Nodes;
using ProcSight.Core.Models;
using ProcSight.Core.Identity;
using ProcSight.Core.Privacy;
using ProcSight.Infrastructure.Providers;
namespace ProcSight.Infrastructure.Research;
internal static class InlineResearchResult
{
    internal static ProviderResult Parse(string text,JsonNode response,AnalysisPayload payload)
    {
        var now=DateTimeOffset.UtcNow;
        var evidence=ResearchSourceValidator.Extract(response.ToJsonString(),now);
        var tools=0;
        foreach(var item in response["output"]?.AsArray()??[]) {
            if(item?["type"]?.GetValue<string>()!="web_search_call") continue;
            tools++;
            if(item["status"]?.GetValue<string>()!="completed") continue;
            foreach(var source in item["action"]?["sources"]?.AsArray()??[]) {
                var url=source?["url"]?.GetValue<string>();
                if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||url!.Length>2048) continue;
                var title=source?["title"]?.GetValue<string>()??"Public source returned by web search";
                evidence.Add(new("web:"+IdentityResolver.MakeIdentity(url).Value,DataOrigin.Research,now,now.AddDays(7),"Research",
                    new(){["claim"]=MetadataSanitizer.Normalize(title.Length>1024?title[..1024]:title)},url));
                if(evidence.Count>=256) break;
            }
        }
        evidence=evidence.DistinctBy(e=>e.SourceUrl).Take(256).ToList();
        var references=evidence.ToDictionary(e=>e.SourceUrl!,e=>e.Id,StringComparer.Ordinal);
        var augmented=payload with { Evidence=[..payload.Evidence,..evidence.Where(e=>!payload.Evidence.Any(p=>p.Id==e.SourceUrl)).Select(e=>e with { Id=e.SourceUrl! })] };
        var proposal=ProviderResponseValidator.Parse(text,augmented,now);
        var referenced=proposal.Assessments.SelectMany(a=>a.EvidenceIds).Concat(proposal.Rules.SelectMany(r=>r.EvidenceIds))
            .Concat(proposal.Rules.SelectMany(r=>r.Predicates.SelectMany(p=>p.EvidenceIds))).Where(references.ContainsKey).ToHashSet(StringComparer.Ordinal);
        if(referenced.Count>10) throw new InvalidDataException("Response exceeds allowed structure");
        evidence=evidence.OrderByDescending(e=>referenced.Contains(e.SourceUrl!)).Take(10).ToList();
        List<string> Map(List<string> ids)=>ids.Select(id=>references.GetValueOrDefault(id,id)).Distinct().ToList();
        Assessment MapAssessment(Assessment a)=>a with { EvidenceIds=Map(a.EvidenceIds) };
        proposal=proposal with { Assessments=proposal.Assessments.Select(MapAssessment).ToList(),Rules=proposal.Rules.Select(r=>r with {
            EvidenceIds=Map(r.EvidenceIds),Assessments=r.Assessments.Select(MapAssessment).ToList(),
            Predicates=r.Predicates.Select(p=>p with { EvidenceIds=Map(p.EvidenceIds) }).ToList() }).ToList() };
        return new(proposal,ToolUses:tools,ResearchEvidence:evidence);
    }
}

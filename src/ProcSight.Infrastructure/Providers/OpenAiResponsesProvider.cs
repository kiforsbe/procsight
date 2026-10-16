using ProcSight.Application.Providers;
using ProcSight.Core.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProcSight.Infrastructure.Research;
using ProcSight.Core.Privacy;
namespace ProcSight.Infrastructure.Providers;
public sealed class OpenAiResponsesProvider(ProviderSettings settings,Func<string?> apiKey,HttpClient? http=null) : IAnalysisProvider
{
    private readonly HttpClient client=http ?? ProviderHttp.Create();
    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct)=>Task.FromResult(new ProviderCapabilities(true,true));
    public async Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct)
    {
        if(payload.ResearchEnabled&&!AnalysisProjection.ResearchSafe(payload)) return new(null,new(ProviderErrorKind.Unsupported,"Web research requires public identity metadata without command context."),0);
        var key=apiKey(); if(string.IsNullOrWhiteSpace(key)) return new(null,new(ProviderErrorKind.Auth,"Configure an API key."),0);
        var body=new JsonObject { ["model"]=settings.Model,["store"]=false,["instructions"]=AnalysisResponseSchema.Instructions,
            ["input"]=JsonSerializer.Serialize(payload),["max_output_tokens"]=4096,
            ["text"]=new JsonObject { ["format"]=new JsonObject { ["type"]="json_schema",["name"]="process_analysis",["strict"]=true,["schema"]=AnalysisResponseSchema.Schema } } };
        if(payload.ResearchEnabled) {
            body["tools"]=new JsonArray(new JsonObject { ["type"]="web_search" }); body["tool_choice"]="required"; body["max_tool_calls"]=1;
            body["include"]=new JsonArray("web_search_call.action.sources");
            body["instructions"]=AnalysisResponseSchema.Instructions.Replace("using only submitted facts and evidence","using submitted facts, evidence and attributable web search results")
                .Replace("Do not request files, commands or tools.","Use web search within this analysis. No files, commands or other tools.")+
                " For web evidence, cite exact HTTPS source URLs returned by the search tool in evidenceIds; never invent URLs. Distinguish likely public matches from authenticated local identity.";
        }
        var response=await ProviderHttp.SendAsync(client,ProviderHttp.Route(settings,"responses"),key,body,ct);
        if(response.Error is not null) return new(null,response.Error);
        try
        {
            if(payload.ResearchEnabled&&(response.Body?["status"]?.GetValue<string>()!="completed"||response.Body?["error"] is not null||response.Body?["incomplete_details"] is not null))
                return new(null,new(ProviderErrorKind.Malformed,"Research analysis did not complete successfully; partial findings were discarded.",FailureReason:ProviderFailureReason.OutputIncomplete));
            if(response.Body?["status"]?.GetValue<string>()=="incomplete") return new(null,new(ProviderErrorKind.Malformed,"Provider response was truncated.",FailureReason:ProviderFailureReason.OutputIncomplete));
            var (text,refused)=ProviderHttp.ResponsesText(response.Body!);
            if(refused) return new(null,new(ProviderErrorKind.Refused,"Provider refused analysis."));
            if(text is null) return new(null,new(ProviderErrorKind.Malformed,"No structured explanation returned.",FailureReason:ProviderFailureReason.NoOutput));
            return payload.ResearchEnabled?InlineResearchResult.Parse(text,response.Body!,payload):new(ProviderResponseValidator.Parse(text,payload,DateTimeOffset.UtcNow));
        }
        catch(InvalidDataException e) { return new(null,ProviderResponseValidator.Failure(e)); }
        catch(Exception e) when(e is InvalidOperationException or System.Text.Json.JsonException)
        { return new(null,new(ProviderErrorKind.Malformed,"Invalid analysis result; no rules activated.",FailureReason:ProviderFailureReason.InvalidStructure)); }
    }
}

using ProcSight.Application.Providers;
using ProcSight.Core.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace ProcSight.Infrastructure.Providers;
public sealed class OpenAiResponsesProvider(ProviderSettings settings,Func<string?> apiKey,HttpClient? http=null) : IAnalysisProvider
{
    private readonly HttpClient client=http ?? ProviderHttp.Create();
    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct)=>Task.FromResult(new ProviderCapabilities(true,true));
    public async Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct)
    {
        var key=apiKey(); if(string.IsNullOrWhiteSpace(key)) return new(null,new(ProviderErrorKind.Auth,"Configure an API key."),0);
        var body=new JsonObject { ["model"]=settings.Model,["store"]=false,["instructions"]=AnalysisResponseSchema.Instructions,
            ["input"]=JsonSerializer.Serialize(payload),["max_output_tokens"]=4096,
            ["text"]=new JsonObject { ["format"]=new JsonObject { ["type"]="json_schema",["name"]="process_analysis",["strict"]=true,["schema"]=AnalysisResponseSchema.Schema } } };
        var response=await ProviderHttp.SendAsync(client,ProviderHttp.Route(settings,"responses"),key,body,ct);
        if(response.Error is not null) return new(null,response.Error);
        try
        {
            if(response.Body?["status"]?.GetValue<string>()=="incomplete") return new(null,new(ProviderErrorKind.Malformed,"Provider response was truncated.",FailureReason:ProviderFailureReason.OutputIncomplete));
            var (text,refused)=ProviderHttp.ResponsesText(response.Body!);
            if(refused) return new(null,new(ProviderErrorKind.Refused,"Provider refused analysis."));
            if(text is null) return new(null,new(ProviderErrorKind.Malformed,"No structured explanation returned.",FailureReason:ProviderFailureReason.NoOutput));
            return new(ProviderResponseValidator.Parse(text,payload,DateTimeOffset.UtcNow));
        }
        catch(InvalidDataException e) { return new(null,ProviderResponseValidator.Failure(e)); }
        catch(Exception e) when(e is InvalidOperationException or System.Text.Json.JsonException)
        { return new(null,new(ProviderErrorKind.Malformed,"Invalid analysis result; no rules activated.",FailureReason:ProviderFailureReason.InvalidStructure)); }
    }
}

using ProcSight.Application.Providers;
using ProcSight.Core.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace ProcSight.Infrastructure.Providers;
public sealed class CompatibleChatProvider(ProviderSettings settings,Func<string?> apiKey,HttpClient? http=null,bool local=false) : IAnalysisProvider
{
    private readonly HttpClient client=http ?? ProviderHttp.Create();
    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct)=>Task.FromResult(new ProviderCapabilities(settings.StructuredOutput,false));
    public async Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct)
    {
        if(local) LocalEndpointPolicy.EnsureLocal(settings.Endpoint);
        if(payload.Destination==AnalysisDestination.LocalModel&&!local) return new(null,new(ProviderErrorKind.Unsupported,"A loopback local adapter is required."),0);
        var body=new JsonObject { ["model"]=settings.Model,["messages"]=new JsonArray(
            new JsonObject { ["role"]="system",["content"]=AnalysisResponseSchema.Instructions+" Return JSON matching this schema: "+AnalysisResponseSchema.Schema.ToJsonString() },
            new JsonObject { ["role"]="user",["content"]=JsonSerializer.Serialize(payload) }),["max_tokens"]=4096 };
        if(settings.StructuredOutput) body["response_format"]=new JsonObject { ["type"]="json_schema",["json_schema"]=new JsonObject {
            ["name"]="process_analysis",["strict"]=true,["schema"]=AnalysisResponseSchema.Schema } };
        var response=await ProviderHttp.SendAsync(client,ProviderHttp.Route(settings,"chat/completions"),apiKey(),body,ct);
        if(response.Error is not null) return new(null,response.Error);
        try
        {
            var choice=response.Body?["choices"]?[0];
            if(choice?["finish_reason"]?.GetValue<string>()=="length") return new(null,new(ProviderErrorKind.Malformed,"Provider response truncated.",FailureReason:ProviderFailureReason.OutputIncomplete));
            var text=choice?["message"]?["content"]?.GetValue<string>();
            return text is null ? new(null,new(ProviderErrorKind.Malformed,"No JSON explanation returned.",FailureReason:ProviderFailureReason.NoOutput)) :
                new(ProviderResponseValidator.Parse(text,payload,DateTimeOffset.UtcNow));
        }
        catch(InvalidDataException e) { return new(null,ProviderResponseValidator.Failure(e)); }
        catch(Exception e) when(e is InvalidOperationException or System.Text.Json.JsonException)
        { return new(null,new(ProviderErrorKind.Malformed,"Invalid analysis result; no rules activated.",FailureReason:ProviderFailureReason.InvalidStructure)); }
    }
}

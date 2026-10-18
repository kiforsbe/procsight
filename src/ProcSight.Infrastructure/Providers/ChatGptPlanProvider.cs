using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProcSight.Application.Providers;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Authentication;
using ProcSight.Infrastructure.Research;
using ProcSight.Core.Privacy;

namespace ProcSight.Infrastructure.Providers;

/// <summary>Direct OAuth ChatGPT plan inference. Never retries or substitutes API credentials.</summary>
public sealed class ChatGptPlanProvider : IAnalysisProvider
{
    private static readonly Uri Endpoint=new("https://api.openai.com/v1/responses");
    private const int StreamLimit=1024*1024, TextLimit=256*1024;
    private readonly ProviderSettings settings;
    private readonly Func<CancellationToken,Task<string?>> accessToken;
    private readonly HttpClient client;

    public ChatGptPlanProvider(ProviderSettings settings,Func<CancellationToken,Task<string?>> accessToken,HttpClient? http=null)
    {
        if(settings.Endpoint != new Uri("https://api.openai.com/v1") && settings.Endpoint != Endpoint &&
            settings.Endpoint != new Uri("https://api.openai.com/v1/"))
            throw new ArgumentException("ChatGPT plan inference requires the public OpenAI endpoint.",nameof(settings));
        this.settings=settings; this.accessToken=accessToken; client=http ?? ProviderHttp.Create();
    }
    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct)=>Task.FromResult(new ProviderCapabilities(true,true));

    public async Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct)
    {
        if(payload.ResearchEnabled&&!AnalysisProjection.ResearchSafe(payload)) return Failure(ProviderErrorKind.Unsupported,"Web research requires public identity metadata without command context.",0);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        var calls=0;
        try
        {
            var token=await accessToken(timeout.Token);
            if(string.IsNullOrWhiteSpace(token)) return Failure(ProviderErrorKind.Auth,"Sign in to the selected ChatGPT account.",0);
            timeout.Token.ThrowIfCancellationRequested();
            var body=new JsonObject { ["model"]=settings.Model,["store"]=false,["stream"]=true,
                ["instructions"]=AnalysisResponseSchema.Instructions,
                ["input"]=new JsonArray(new JsonObject { ["role"]="user",["content"]=JsonSerializer.Serialize(payload) }),
                ["text"]=new JsonObject { ["format"]=new JsonObject { ["type"]="json_schema",["name"]="process_analysis",
                    ["strict"]=true,["schema"]=AnalysisResponseSchema.Schema } } };
            if(payload.ResearchEnabled) {
                body["tools"]=new JsonArray(new JsonObject { ["type"]="web_search" }); body["tool_choice"]="required";
                body["include"]=new JsonArray("web_search_call.action.sources");
                body["instructions"]=AnalysisResponseSchema.Instructions.Replace("using only submitted facts and evidence","using submitted facts, evidence and attributable web search results")
                    .Replace("Do not request files, commands or tools.","Use web search within this analysis to identify the executable or service. No files, commands or other tools. Use one focused search where possible.")+
                    " For web evidence, put the exact HTTPS source URL returned by the search tool in evidenceIds; never invent URLs. Local evidence IDs remain valid. Distinguish a likely public product match from authenticated local executable identity.";
            }
            using var request=new HttpRequestMessage(HttpMethod.Post,Endpoint) {
                Content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json") };
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
            request.Headers.Accept.Add(new("text/event-stream"));
            calls=1;
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            if(!response.IsSuccessStatusCode)
            {
                var status=(int)response.StatusCode;
                var kind=status is 401 or 403?ProviderErrorKind.Auth:status==429?ProviderErrorKind.RateLimited:
                    status>=500?ProviderErrorKind.Transient:ProviderErrorKind.Unsupported;
                return new(null,new(kind,$"ChatGPT plan HTTP {status}; response content withheld.",response.Headers.RetryAfter?.Delta),calls);
            }
            await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);
            return await ReadCompletedAsync(stream,payload,timeout.Token);
        }
        catch(OperationCanceledException) { return Failure(ct.IsCancellationRequested?ProviderErrorKind.Cancelled:ProviderErrorKind.Transient,"ChatGPT plan request cancelled or timed out.",calls); }
        catch(HttpRequestException) { return Failure(ProviderErrorKind.Transient,"ChatGPT plan connection unavailable.",calls); }
        catch(IOException) { return Failure(ProviderErrorKind.Transient,"ChatGPT plan stream interrupted.",calls); }
        catch(ChatGptAuthenticationException error)
        { return Failure(error.Kind,"ChatGPT account credential renewal failed; error content withheld.",calls); }
        catch(InvalidDataException e) { return new(null,ProviderResponseValidator.Failure(e),calls); }
        catch(Exception e) when(e is JsonException or InvalidOperationException or ArgumentException or DecoderFallbackException)
        { return Failure(ProviderErrorKind.Malformed,"Invalid ChatGPT plan result; no rules activated.",calls,ProviderFailureReason.InvalidStructure); }
        catch(Exception) when(calls==0)
        { return Failure(ProviderErrorKind.Auth,"ChatGPT account credentials unavailable; sign in again.",0); }
    }

    private static async Task<ProviderResult> ReadCompletedAsync(Stream stream,AnalysisPayload payload,CancellationToken ct)
    {
        // Frame bytes before decoding: neither an unterminated line nor a comment can evade the total limit.
        var chunk=new byte[8192]; using var line=new MemoryStream(); var frame=new StringBuilder();
        var text=new StringBuilder(); var total=0; var textBytes=0;
        var items=new SortedDictionary<int,JsonNode>();
        var utf8=new UTF8Encoding(false,true);
        int read;
        while((read=await stream.ReadAsync(chunk,ct))>0)
        {
            total+=read;
            if(total>StreamLimit) return Failure(ProviderErrorKind.Malformed,"ChatGPT plan stream exceeds 1 MiB.",reason:ProviderFailureReason.OutputTooLarge);
            for(var i=0;i<read;i++)
            {
                if(chunk[i]!=(byte)'\n') { line.WriteByte(chunk[i]); continue; }
                var value=utf8.GetString(line.GetBuffer(),0,(int)line.Length).TrimEnd('\r'); line.SetLength(0);
                if(value.Length==0)
                {
                    if(frame.Length==0) continue;
                    var result=ReadEvent(frame.ToString(),text,ref textBytes,payload,items); frame.Clear();
                    if(result is not null) return result;
                }
                else if(value.StartsWith("data:",StringComparison.Ordinal))
                {
                    if(frame.Length>0) frame.Append('\n');
                    frame.Append(value.AsSpan(value.Length>5 && value[5]==' '?6:5));
                }
            }
        }
        return Failure(ProviderErrorKind.Malformed,"ChatGPT plan stream ended before completed inference.",reason:ProviderFailureReason.StreamEnded);
    }

    private static ProviderResult? ReadEvent(string data,StringBuilder text,ref int textBytes,AnalysisPayload payload,SortedDictionary<int,JsonNode> items)
    {
        if(data=="[DONE]") return Failure(ProviderErrorKind.Malformed,"ChatGPT plan stream lacks completed inference.",reason:ProviderFailureReason.StreamEnded);
        var item=JsonNode.Parse(data) ?? throw new InvalidDataException();
        var type=item["type"]?.GetValue<string>() ?? throw new InvalidDataException();
        if(type is "response.refusal.delta" or "response.refusal.done" || ContainsRefusal(item))
            return Failure(ProviderErrorKind.Refused,"ChatGPT plan refused analysis.");
        if(type is "error" or "response.failed")
        {
            var error=item["response"]?["error"] ?? item["error"] ?? item;
            var code=error["code"]?.GetValue<string>();
            var kind=code is "subscription_sharing_usage_limit_exceeded" or "rate_limit_exceeded"?ProviderErrorKind.RateLimited:
                code is "invalid_api_key" or "invalid_token" or "authentication_error" or "token_expired"?ProviderErrorKind.Auth:
                ProviderErrorKind.Transient;
            return Failure(kind,"ChatGPT plan inference failed; error content withheld.");
        }
        if(type=="response.incomplete") return Failure(ProviderErrorKind.Malformed,"ChatGPT plan inference was incomplete.",reason:ProviderFailureReason.OutputIncomplete);
        if(payload.ResearchEnabled&&type=="response.output_item.done"&&item["item"] is { } completedItem)
            items[item["output_index"]?.GetValue<int>()??items.Count]=completedItem.DeepClone();
        if(type=="response.output_text.delta")
        {
            var delta=item["delta"]?.GetValue<string>() ?? throw new InvalidDataException();
            textBytes+=Encoding.UTF8.GetByteCount(delta);
            if(textBytes>TextLimit) return Failure(ProviderErrorKind.Malformed,"ChatGPT plan output exceeds 256 KiB.",reason:ProviderFailureReason.OutputTooLarge);
            text.Append(delta);
        }
        if(type!="response.completed") return null;
        var response=item["response"] ?? throw new InvalidDataException();
        if(response["status"]?.GetValue<string>()!="completed" || response["error"] is not null || response["incomplete_details"] is not null)
            return Failure(ProviderErrorKind.Malformed,"ChatGPT plan inference was not completed.",reason:ProviderFailureReason.OutputIncomplete);
        if(payload.ResearchEnabled&&response["output"] is not JsonArray { Count:>0 }&&items.Count>0)
            response["output"]=new JsonArray(items.Values.Select(n=>n.DeepClone()).ToArray());
        var final=text.ToString();
        if(response["output"] is not null)
        {
            var output=ProviderHttp.ResponsesText(response);
            if(output.Refused) return Failure(ProviderErrorKind.Refused,"ChatGPT plan refused analysis.");
            // Plan streams can confirm completion with an empty output array after sending all deltas.
            // A nonempty final explanation replaces deltas; absent final text must not erase them.
            if(!string.IsNullOrWhiteSpace(output.Text)) final=output.Text;
        }
        if(string.IsNullOrWhiteSpace(final)) return Failure(ProviderErrorKind.Malformed,"No explanation returned.",reason:ProviderFailureReason.NoOutput);
        if(Encoding.UTF8.GetByteCount(final)>TextLimit) return Failure(ProviderErrorKind.Malformed,"Explanation exceeded the output limit.",reason:ProviderFailureReason.OutputTooLarge);
        return payload.ResearchEnabled?InlineResearchResult.Parse(final,response,payload):new(ProviderResponseValidator.Parse(final,payload,DateTimeOffset.UtcNow));
    }
    private static bool ContainsRefusal(JsonNode? node) => node switch
    {
        JsonObject obj => obj["type"] is JsonValue type && type.TryGetValue<string>(out var value) && value=="refusal" || obj.Any(p=>ContainsRefusal(p.Value)),
        JsonArray array => array.Any(ContainsRefusal),
        _ => false
    };
    private static ProviderResult Failure(ProviderErrorKind kind,string message,int calls=1,ProviderFailureReason? reason=null)=>new(null,new(kind,message,FailureReason:reason),calls);
}

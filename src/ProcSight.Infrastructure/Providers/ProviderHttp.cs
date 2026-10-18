using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text;
using ProcSight.Core.Models;
namespace ProcSight.Infrastructure.Providers;
public static class ProviderHttp
{
    public static HttpClient Create()=>new(new HttpClientHandler { AllowAutoRedirect=false }) { Timeout=TimeSpan.FromSeconds(60) };
    public static async Task<(JsonNode? Body,ProviderError? Error)> SendAsync(HttpClient client,Uri endpoint,string? key,JsonNode body,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,endpoint) { Content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json") };
            if(!string.IsNullOrEmpty(key)) request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            if(!response.IsSuccessStatusCode)
            {
                var status=(int)response.StatusCode;
                var kind=status is 401 or 403?ProviderErrorKind.Auth:status==429?ProviderErrorKind.RateLimited:
                    status>=500?ProviderErrorKind.Transient:ProviderErrorKind.Unsupported;
                return(null,new(kind,$"Provider HTTP {status}; response content withheld.",response.Headers.RetryAfter?.Delta));
            }
            await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token); using var buffer=new MemoryStream();
            var chunk=new byte[8192]; int read;
            while((read=await stream.ReadAsync(chunk,timeout.Token))>0)
            { if(buffer.Length+read>262144) return(null,new(ProviderErrorKind.Malformed,"Provider response exceeds 256 KiB.")); await buffer.WriteAsync(chunk.AsMemory(0,read),timeout.Token); }
            return(JsonNode.Parse(buffer.ToArray()),null);
        }
        catch(OperationCanceledException) { return(null,new(ct.IsCancellationRequested?ProviderErrorKind.Cancelled:ProviderErrorKind.Transient,"Request cancelled or timed out.")); }
        catch(HttpRequestException) { return(null,new(ProviderErrorKind.Transient,"Provider connection unavailable.")); }
        catch(System.Text.Json.JsonException) { return(null,new(ProviderErrorKind.Malformed,"Provider response is not JSON.")); }
    }
    public static Uri Route(ProviderSettings settings,string route)
    {
        if(settings.Endpoint.Scheme!="https" && !settings.Endpoint.IsLoopback) throw new ArgumentException("Remote providers require HTTPS.");
        return new(settings.Endpoint.ToString().TrimEnd('/')+"/"+route);
    }
    public static (string? Text,bool Refused) ResponsesText(JsonNode body)
    {
        var parts=new List<string>();
        foreach(var item in body["output"]?.AsArray() ?? [])
            foreach(var content in item?["content"]?.AsArray() ?? [])
            { if(content?["type"]?.GetValue<string>()=="refusal") return(null,true); if(content?["type"]?.GetValue<string>()=="output_text") parts.Add(content["text"]!.GetValue<string>()); }
        return(parts.Count==0?null:string.Join("\n",parts),false);
    }
}

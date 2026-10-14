using System.Net;
using System.Text;
using System.Text.Json;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Providers;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class FakeHttp(string body,HttpStatusCode status=HttpStatusCode.OK) : HttpMessageHandler
{
    public string? Request; public int Calls;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    { Calls++; Request=await request.Content!.ReadAsStringAsync(ct); return new(status){ Content=new StringContent(body,Encoding.UTF8,"application/json") }; }
}
public sealed class ProviderTests
{
    private static AnalysisPayload Payload()=>new(new("fixture"),new(){["name"]="node.exe"},[],null,0,AnalysisDestination.CloudModel);
    [Fact] public async Task ResponsesRequestUsesStrictSchema()
    {
        var http=new FakeHttp("{\"output\":[]}");
        await new OpenAiResponsesProvider(new("openai",new("https://api.openai.com/v1"),"chosen-model"),()=>"synthetic-key",new(http)).AnalyzeAsync(Payload(),default);
        using var request=JsonDocument.Parse(http.Request!);
        Assert.True(request.RootElement.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
        Assert.False(request.RootElement.TryGetProperty("tools",out _));
        Assert.DoesNotContain("synthetic-key",http.Request!);
    }
    [Fact] public async Task RefusalIsNotMalformedSuccess()
    {
        var http=new FakeHttp("{\"output\":[{\"content\":[{\"type\":\"refusal\",\"refusal\":\"No\"}]}]}");
        var result=await new OpenAiResponsesProvider(new("openai",new("https://api.openai.com/v1"),"model"),()=>"key",new(http)).AnalyzeAsync(Payload(),default);
        Assert.Equal(ProviderErrorKind.Refused,result.Error?.Kind);
    }
    [Fact] public void LocalOnlyRejectsRemoteEndpoint() => Assert.Throws<ArgumentException>(()=>LocalEndpointPolicy.EnsureLocal(new("https://example.com")));
    [Fact] public async Task LoopbackRedirectNeverFollowed()
    {
        var http=new FakeHttp("{}",HttpStatusCode.Redirect);
        var result=await new CompatibleChatProvider(new("local",new("http://127.0.0.1:1234/v1"),"model"),()=>null,new(http),true).AnalyzeAsync(
            Payload() with { Destination=AnalysisDestination.LocalModel,CommandLine="synthetic" },default);
        Assert.Equal(1,http.Calls); Assert.Equal(ProviderErrorKind.Unsupported,result.Error?.Kind);
    }
    [Fact] public void UnknownEvidenceReferenceRejected() => Assert.Throws<InvalidDataException>(()=>ProviderResponseValidator.Parse(
        """{"assessments":[{"axis":"Purpose","value":"DeveloperTool","reason":"Fixture","confidence":"Low","evidenceIds":["invented"]}],"rules":[]}""",Payload(),Samples.Now));
}

using System.Net;
using System.Text;
using System.Text.Json;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Research;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class ChatGptResearchTests
{
    private static string Event(object item)=>"data: "+JsonSerializer.Serialize(item)+"\n\n";
    private static object Citation()=>new { type="message",content=new[]{new { type="output_text",text="An example developer tool",annotations=new[]{new { type="url_citation",url="https://example.com/tool",start_index=0,end_index=25 }} }} };
    private sealed class Handler(string body,HttpStatusCode status=HttpStatusCode.OK):HttpMessageHandler
    {
        public string? Body,Token,Url;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Body=await request.Content!.ReadAsStringAsync(ct); Token=request.Headers.Authorization?.Parameter; Url=request.RequestUri!.ToString();
            return new(status) { Content=new StringContent(body,Encoding.UTF8,"text/event-stream") };
        }
    }
    private static ChatGptWebResearchProvider Provider(Handler handler)=>new(new("chatgpt",new("https://api.openai.com/v1"),"fixture-model"),
        _=>Task.FromResult<string?>("synthetic-token"),new(handler));
    [Fact] public async Task ResearchUsesPlanOAuthRequiredWebToolAndOnlyPublicIdentityFields()
    {
        var handler=new Handler(Event(new { type="response.completed",response=new { status="completed",output=new object[]{new { type="web_search_call",status="completed" },Citation()} } }));
        var result=await Provider(handler).ResearchAsync(new(new(){["name"]="fixture.exe",["path"]="PRIVATE_PATH",["CommandLine"]="PRIVATE_COMMAND"}),default);
        Assert.Null(result.Error); Assert.Single(result.Evidence); Assert.Equal(1,result.ToolUses);
        Assert.Equal("synthetic-token",handler.Token); Assert.Equal("https://api.openai.com/v1/responses",handler.Url);
        using var json=JsonDocument.Parse(handler.Body!); var root=json.RootElement;
        Assert.True(root.GetProperty("stream").GetBoolean()); Assert.False(root.GetProperty("store").GetBoolean());
        Assert.False(root.TryGetProperty("max_tool_calls",out _)); Assert.False(root.TryGetProperty("max_output_tokens",out _));
        Assert.Equal("web_search",root.GetProperty("tools")[0].GetProperty("type").GetString());
        Assert.Equal("required",root.GetProperty("tool_choice").GetString());
        Assert.DoesNotContain("PRIVATE_",handler.Body!); Assert.DoesNotContain("synthetic-token",handler.Body!);
    }
    [Theory] [InlineData("response.failed")] [InlineData("response.incomplete")] [InlineData("response.refusal.done")]
    public async Task PartialCitationsCannotSurviveFailure(string type)
    {
        var handler=new Handler(Event(new { type="response.output_item.done",output_index=0,item=Citation() })+Event(new { type }));
        var result=await Provider(handler).ResearchAsync(new(new(){["name"]="fixture.exe"}),default);
        Assert.NotNull(result.Error); Assert.Empty(result.Evidence);
    }
    [Fact] public async Task EmptyFinalOutputPreservesCompletedItemsOnlyAfterSuccessfulCompletion()
    {
        var handler=new Handler(Event(new { type="response.output_item.done",output_index=0,item=Citation() })+
            Event(new { type="response.completed",response=new { status="completed",output=Array.Empty<object>() } }));
        Assert.Single((await Provider(handler).ResearchAsync(new(new()),default)).Evidence);
    }
    [Fact] public async Task UnsupportedToolDoesNotFallBackToApiKey()
    {
        var result=await Provider(new("private error body",HttpStatusCode.BadRequest)).ResearchAsync(new(new()),default);
        Assert.Equal(ProviderErrorKind.Unsupported,result.Error?.Kind); Assert.Empty(result.Evidence);
        Assert.DoesNotContain("private error body",JsonSerializer.Serialize(result));
    }
}

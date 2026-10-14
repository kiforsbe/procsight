using System.Net;
using System.Text;
using System.Text.Json;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Providers;
using ProcSight.Infrastructure.Authentication;
using Xunit;

namespace ProcSight.Infrastructure.Tests;

public sealed class ChatGptPlanProviderTests
{
    private const string Valid = """{"identityLabel":null,"role":null,"assessments":[],"rules":[]}""";
    private static AnalysisPayload Payload() => new(new("fixture"),new(){["name"]="fixture.exe"},[],null,0,AnalysisDestination.CloudModel);
    private static string Event(object value) => "data: " + JsonSerializer.Serialize(value) + "\n\n";
    private static string Delta(string text) => Event(new { type="response.output_text.delta",delta=text });
    private static string Completed() => Event(new { type="response.completed",response=new { status="completed" } });
    private static ChatGptPlanProvider Provider(Handler handler, Func<CancellationToken,Task<string?>>? token=null) =>
        new(new("chatgpt-plan",new("https://api.openai.com/v1"),"account-model"),token ?? (_=>Task.FromResult<string?>("synthetic-token")),new(handler));

    [Fact] public async Task UsesFixedEndpointArrayInputAndFreshBearerWithStrictSchema()
    {
        var handler=new Handler(Delta(Valid)+Completed()); var tokenReads=0;
        var provider=Provider(handler,_=>Task.FromResult<string?>("synthetic-token-"+(++tokenReads)));
        Assert.NotNull((await provider.AnalyzeAsync(Payload(),default)).Proposal);
        Assert.NotNull((await provider.AnalyzeAsync(Payload(),default)).Proposal);
        Assert.Equal(2,tokenReads); Assert.Equal("synthetic-token-2",handler.Bearer);
        Assert.Equal("https://api.openai.com/v1/responses",handler.Url);
        using var document=JsonDocument.Parse(handler.Body!); var body=document.RootElement;
        Assert.Equal("account-model",body.GetProperty("model").GetString());
        Assert.True(body.GetProperty("stream").GetBoolean()); Assert.False(body.GetProperty("store").GetBoolean());
        Assert.Equal("user",body.GetProperty("input")[0].GetProperty("role").GetString());
        Assert.Equal(JsonSerializer.Serialize(Payload()),body.GetProperty("input")[0].GetProperty("content").GetString());
        Assert.True(body.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
        Assert.False(body.TryGetProperty("max_output_tokens",out _)); Assert.False(body.TryGetProperty("tools",out _));
        Assert.DoesNotContain("synthetic-token",handler.Body!);
        Assert.Equal(new ProviderCapabilities(true,false),await provider.GetCapabilitiesAsync(default));
    }
    [Fact] public void ArbitraryEndpointRejected() => Assert.Throws<ArgumentException>(()=>
        new ChatGptPlanProvider(new("chatgpt-plan",new("https://example.com/v1"),"model"),_=>Task.FromResult<string?>("token")));
    [Fact] public async Task CompletedOutputCanSupplyFullStructuredDocument()
    {
        var stream=Event(new { type="response.completed",response=new { status="completed",output=new[]{new { content=new[]{new { type="output_text",text=Valid }} }} } });
        Assert.NotNull((await Provider(new(stream)).AnalyzeAsync(Payload(),default)).Proposal);
    }
    [Theory]
    [InlineData("data: invalid\n\n")]
    [InlineData("data: {\"type\":\"response.completed\",\"response\":{\"status\":\"incomplete\"}}\n\n")]
    [InlineData("data: {\"type\":\"response.output_text.delta\",\"delta\":\"{}\"}\n\n")]
    [InlineData("data: {\"type\":\"response.incomplete\"}\n\n")]
    public async Task MalformedOrInterruptedStreamsNeverYieldProposal(string stream)
    {
        var result=await Provider(new(stream)).AnalyzeAsync(Payload(),default);
        Assert.Null(result.Proposal); Assert.Equal(ProviderErrorKind.Malformed,result.Error?.Kind);
    }
    [Fact] public async Task CompletedButInvalidStructuredOutputRejected()
    {
        var result=await Provider(new(Delta("{}")+Completed())).AnalyzeAsync(Payload(),default);
        Assert.Null(result.Proposal); Assert.Equal(ProviderErrorKind.Malformed,result.Error?.Kind);
    }
    [Theory]
    [InlineData("response.refusal.delta")]
    [InlineData("response.refusal.done")]
    public async Task RefusalsNeverAcceptPartialValidOutput(string type)
    {
        var result=await Provider(new(Delta(Valid)+Event(new {type,delta="private error"})+Completed())).AnalyzeAsync(Payload(),default);
        Assert.Null(result.Proposal); Assert.Equal(ProviderErrorKind.Refused,result.Error?.Kind);
        Assert.DoesNotContain("private error",result.Error!.Message);
    }
    [Fact] public async Task CompletedRefusalIsRejected()
    {
        var result=await Provider(new(Event(new {type="response.completed",response=new {status="completed",output=new[]{new {content=new[]{new {type="refusal",refusal="private"}}}}}}))).AnalyzeAsync(Payload(),default);
        Assert.Equal(ProviderErrorKind.Refused,result.Error?.Kind);
    }
    [Theory]
    [InlineData(401,ProviderErrorKind.Auth)]
    [InlineData(403,ProviderErrorKind.Auth)]
    [InlineData(429,ProviderErrorKind.RateLimited)]
    [InlineData(500,ProviderErrorKind.Transient)]
    public async Task HttpErrorsAreRedactedAndNeverRetried(int status,ProviderErrorKind expected)
    {
        var handler=new Handler("private-token-or-payload",(HttpStatusCode)status);
        var result=await Provider(handler).AnalyzeAsync(Payload(),default);
        Assert.Equal(expected,result.Error?.Kind); Assert.Null(result.Proposal); Assert.Equal(1,handler.Calls);
        Assert.DoesNotContain("private-token-or-payload",result.Error!.Message);
    }
    [Theory]
    [InlineData("subscription_sharing_usage_limit_exceeded",ProviderErrorKind.RateLimited)]
    [InlineData("subscription_sharing_usage_unavailable",ProviderErrorKind.Transient)]
    [InlineData("invalid_api_key",ProviderErrorKind.Auth)]
    public async Task TerminalStreamErrorInvalidatesPartialOutput(string code,ProviderErrorKind expected)
    {
        var result=await Provider(new(Delta(Valid)+Event(new {type="response.failed",response=new {error=new {code,message="private"}}}))).AnalyzeAsync(Payload(),default);
        Assert.Equal(expected,result.Error?.Kind); Assert.Null(result.Proposal); Assert.DoesNotContain("private",result.Error!.Message);
    }
    [Fact] public async Task MissingTokenMakesNoRequest()
    {
        var handler=new Handler(""); var result=await Provider(handler,_=>Task.FromResult<string?>(null)).AnalyzeAsync(Payload(),default);
        Assert.Equal(ProviderErrorKind.Auth,result.Error?.Kind); Assert.Equal(0,handler.Calls); Assert.Equal(0,result.BillableCalls);
    }
    [Theory]
    [InlineData(ProviderErrorKind.Auth)]
    [InlineData(ProviderErrorKind.RateLimited)]
    [InlineData(ProviderErrorKind.Transient)]
    public async Task CredentialRenewalErrorsPreserveCategoryWithoutSendingOrEchoingDetails(ProviderErrorKind kind)
    {
        var handler=new Handler("");
        var result=await Provider(handler,_=>Task.FromException<string?>(new ChatGptAuthenticationException("private credential response",kind))).AnalyzeAsync(Payload(),default);
        Assert.Equal(kind,result.Error?.Kind); Assert.Null(result.Proposal); Assert.Equal(0,handler.Calls);
        Assert.Equal(0,result.BillableCalls); Assert.DoesNotContain("private credential response",result.Error!.Message);
    }
    [Fact] public async Task CancellationWhileReadingReturnsCancelled()
    {
        var handler=new Handler("") { BlockRead=true }; using var cts=new CancellationTokenSource();
        var pending=Provider(handler).AnalyzeAsync(Payload(),cts.Token); await handler.Started.Task; cts.Cancel();
        Assert.Equal(ProviderErrorKind.Cancelled,(await pending).Error?.Kind);
    }
    [Fact] public async Task OutputAndStreamLimitsRejectOversizedData()
    {
        foreach(var stream in new[]{Delta(new string('x',262145))+Completed(),":"+new string('x',1048576)+"\n\n"+Delta(Valid)+Completed()})
        {
            var result=await Provider(new(stream)).AnalyzeAsync(Payload(),default);
            Assert.Null(result.Proposal); Assert.Equal(ProviderErrorKind.Malformed,result.Error?.Kind);
        }
    }
    [Fact] public async Task ExactOutputLimitAndExactStreamLimitRemainAccepted()
    {
        var padded=Valid+new string(' ',262144-Encoding.UTF8.GetByteCount(Valid));
        Assert.NotNull((await Provider(new(Delta(padded)+Completed())).AnalyzeAsync(Payload(),default)).Proposal);
        var terminal=Delta(Valid)+Completed();
        var prefix=":"+new string(' ',1048576-Encoding.UTF8.GetByteCount(terminal)-3)+"\n\n";
        Assert.NotNull((await Provider(new(prefix+terminal)).AnalyzeAsync(Payload(),default)).Proposal);
    }
    [Fact] public async Task CompletionRunsExistingEvidenceValidation()
    {
        const string invented="""{"identityLabel":null,"role":null,"assessments":[{"axis":"Purpose","value":"DeveloperTool","reason":"fixture","confidence":"High","evidenceIds":["invented"]}],"rules":[]}""";
        var result=await Provider(new(Delta(invented)+Completed())).AnalyzeAsync(Payload(),default);
        Assert.Null(result.Proposal); Assert.Equal(ProviderErrorKind.Malformed,result.Error?.Kind);
    }
    [Fact] public async Task EventNamesAndCrLfFramingAreSupported()
    {
        var stream=": keepalive\r\nevent: response.output_text.delta\r\n"+Delta(Valid).Replace("\n","\r\n")+Completed().Replace("\n","\r\n");
        Assert.NotNull((await Provider(new(stream)).AnalyzeAsync(Payload(),default)).Proposal);
    }
    private sealed class Handler(string stream,HttpStatusCode status=HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls; public string? Url,Body,Bearer; public bool BlockRead;
        public TaskCompletionSource Started { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++; Url=request.RequestUri!.AbsoluteUri; Body=await request.Content!.ReadAsStringAsync(ct); Bearer=request.Headers.Authorization?.Parameter;
            return new(status) {Content=BlockRead?new StreamContent(new BlockingStream(Started)):new StringContent(stream,Encoding.UTF8,"text/event-stream")};
        }
    }
    private sealed class BlockingStream(TaskCompletionSource started) : Stream
    {
        public override bool CanRead=>true; public override bool CanSeek=>false; public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException(); public override long Position {get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default) { started.TrySetResult(); await Task.Delay(Timeout.Infinite,ct); return 0; }
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override void Flush()=>throw new NotSupportedException(); public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException(); public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}

using ProcSight.Core.Models;
using ProcSight.Infrastructure.Providers;
using Xunit;
using System.Text.Json;
using ProcSight.Application.Analysis;
namespace ProcSight.Infrastructure.Tests;
public sealed class ProviderFailureTests
{
    private static AnalysisPayload Payload()=>new(new("fixture"),new(){["name"]="fixture.exe"},[],null,0,AnalysisDestination.CloudModel);
    [Theory]
    [InlineData("not JSON",ProviderFailureReason.InvalidJson)]
    [InlineData("{}",ProviderFailureReason.InvalidStructure)]
    [InlineData("{\"assessments\":[{\"axis\":\"Purpose\",\"value\":\"Application\",\"reason\":\"fixture\",\"confidence\":\"Low\",\"evidenceIds\":[]}],\"rules\":[]}",ProviderFailureReason.MissingEvidence)]
    [InlineData("{\"assessments\":[{\"axis\":\"Purpose\",\"value\":\"Unknown\",\"reason\":\"fixture\",\"confidence\":\"Low\",\"evidenceIds\":[\"private-unsubmitted-id\"]}],\"rules\":[]}",ProviderFailureReason.UnknownEvidence)]
    public void ValidationFailureHasSafeReasonAndRecovery(string text,ProviderFailureReason reason)
    {
        var error=Assert.Throws<InvalidDataException>(()=>ProviderResponseValidator.Parse(text,Payload(),DateTimeOffset.UtcNow));
        var failure=ProviderResponseValidator.Failure(error);
        Assert.Equal(reason,failure.FailureReason);
        var explanation=ProviderFailureExplanation.Describe(failure.Kind,failure.FailureReason);
        Assert.NotEmpty(explanation.Details); Assert.NotEmpty(explanation.NextStep);
        Assert.DoesNotContain("private-unsubmitted-id",explanation.ToString());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectionReasonReachesActivityWithoutRetainingProviderText(bool compatible)
    {
        const string text="""{"assessments":[{"axis":"Purpose","value":"Unknown","reason":"PRIVATE_PROVIDER_TEXT","confidence":"Low","evidenceIds":["PRIVATE_EVIDENCE_ID"]}],"rules":[]}""";
        var wire=compatible?JsonSerializer.Serialize(new { choices=new[]{new { message=new { content=text } }} }):
            JsonSerializer.Serialize(new { output=new[]{new { content=new[]{new { type="output_text",text }} }} });
        var http=new HttpClient(new FakeHttp(wire));
        var settings=new ProviderSettings(compatible?"compatible":"openai",new("https://example.com/v1"),"fixture");
        var payload=Payload();
        var result=compatible?await new CompatibleChatProvider(settings,()=>"fixture",http).AnalyzeAsync(payload,default):
            await new OpenAiResponsesProvider(settings,()=>"fixture",http).AnalyzeAsync(payload,default);
        Assert.Equal(ProviderFailureReason.UnknownEvidence,result.Error?.FailureReason);
        var log=new AnalysisActivityLog(); var id=log.Begin(payload,settings,"Fixture process"); log.Complete(id,result,payload);
        var entry=Assert.Single(log.Snapshot()); Assert.Equal(ProviderFailureReason.UnknownEvidence,entry.FailureReason);
        var retained=JsonSerializer.Serialize(entry);
        Assert.DoesNotContain("PRIVATE_PROVIDER_TEXT",retained); Assert.DoesNotContain("PRIVATE_EVIDENCE_ID",retained);
    }
}

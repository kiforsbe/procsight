using ProcSight.Core.Models;
using ProcSight.Infrastructure.Research;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class ResearchTests
{
    [Fact] public void CitationMustComeFromToolResult()
    {
        var result=ResearchSourceValidator.Extract("""{"output":[{"content":[{"type":"output_text","text":"A model guessed https://example.com"}]}]}""",Samples.Now);
        Assert.Empty(result);
    }
    [Fact] public void ToolCitationProducesBoundedAttributableEvidence()
    {
        var result=ResearchSourceValidator.Extract("""{"output":[{"content":[{"type":"output_text","text":"A product helper","annotations":[{"type":"url_citation","url":"https://example.com/product","start_index":0,"end_index":16}]}]}]}""",Samples.Now);
        Assert.Single(result); Assert.Equal("https://example.com/product",result[0].SourceUrl);
        Assert.Equal(DataOrigin.Research,result[0].Origin);
    }
    [Fact] public async Task ResearchQueryHasIdentityOnly()
    {
        var http=new FakeHttp("{\"output\":[]}");
        await new OpenAiWebResearchProvider(new("openai",new("https://api.openai.com/v1"),"chosen-model"),()=>"key",new(http))
            .ResearchAsync(new(new(){["name"]="fixture.exe"}),default);
        Assert.Contains("web_search",http.Request!); Assert.DoesNotContain("CommandLine",http.Request!);
    }
}

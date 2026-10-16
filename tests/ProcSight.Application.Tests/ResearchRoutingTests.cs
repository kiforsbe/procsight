using ProcSight.Application.Analysis;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class ResearchRoutingTests
{
    private static ConnectionProfile Profile(string id,string kind,bool enabled=true)=>new(id,new(kind,new("https://api.openai.com/v1"),"fixture-model"),enabled,0);
    [Theory] [InlineData("chatgpt")] [InlineData("openai")]
    public void ResearchUsesPrimarySupportedConnectionUnlessExplicitlyOverridden(string kind)
    {
        var primary=Profile("primary",kind);
        Assert.Equal(primary.Provider,ConnectionRouter.Research(new(ResearchEnabled:true,Connections:[primary]),primary.Provider));
    }
    [Fact] public void ExplicitChatGptResearchProfileIsEligible()
    {
        var primary=Profile("primary","openai"); var research=Profile("research","chatgpt");
        Assert.Equal(research.Provider,ConnectionRouter.Research(new(ResearchEnabled:true,Connections:[primary,research],ResearchProfileId:"research"),primary.Provider));
    }
    [Fact] public void DisabledExplicitProfileDoesNotSilentlyChargeAnotherConnection()
    {
        var primary=Profile("primary","openai"); var research=Profile("research","chatgpt",false);
        Assert.Null(ConnectionRouter.Research(new(ResearchEnabled:true,Connections:[primary,research],ResearchProfileId:"research"),primary.Provider));
    }
}

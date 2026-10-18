using ProcSight.Application.Settings;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class ConnectionProfileActionsTests
{
    private static ConnectionProfile Profile(string id,int priority)=>new(id,new("openai",new("https://api.openai.com/v1"),"example"),true,priority);
    [Fact] public void RemoveClearsResearchReferenceAndPreservesRemainingConfiguration()
    {
        var other=Profile("second",4);
        var before=new AnalysisPolicy(true,true,Connections:[Profile("first",2),other],ResearchProfileId:"first",FallbackEnabled:true);
        var after=ConnectionProfileActions.Remove(before,"first");
        var remaining=Assert.Single(after.Connections!);
        Assert.Equal(other.Provider,remaining.Provider); Assert.Equal("second",remaining.ProfileId); Assert.Equal(0,remaining.Priority);
        Assert.Null(after.ResearchProfileId); Assert.True(after.FallbackEnabled); Assert.True(after.AutomaticEnabled);
    }
    [Fact] public void LastConnectionCanBeRemovedWithoutChangingAccountOrCredentialStorage()
    {
        var before=new AnalysisPolicy(Connections:[Profile("one",0)]);
        Assert.Empty(ConnectionProfileActions.Remove(before,"one").Connections!);
        Assert.Same(before,ConnectionProfileActions.Remove(before,"missing"));
    }
}

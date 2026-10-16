using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Core.Tests;
public sealed class ConnectionProfileTests
{
    private static ConnectionProfile Profile(string id="one",int priority=0)=>new(id,new("openai",new("https://api.openai.com/v1"),"test"),true,priority);
    [Fact] public void DuplicatedOrUnboundedProfilesAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>ConnectionProfiles.Validate([Profile(),Profile()]));
        Assert.Throws<ArgumentException>(()=>ConnectionProfiles.Validate([Profile(priority:-1)]));
        Assert.Throws<ArgumentException>(()=>ConnectionProfiles.Validate(Enumerable.Range(0,33).Select(i=>Profile("p"+i)).ToArray()));
    }
    [Theory] [InlineData("https://user:secret@example.com/v1")] [InlineData("http://example.com/v1")] [InlineData("https://example.com/v1?key=secret")]
    public void CredentialBearingOrInsecureEndpointsAreRejected(string endpoint)=>Assert.Throws<ArgumentException>(()=>
        ConnectionProfiles.Validate([Profile() with { Provider=new("compatible",new(endpoint),"test") }]));
    [Fact] public void LocalProfileMustBeLoopbackAndChatGptMustBeBound()
    {
        Assert.Throws<ArgumentException>(()=>ConnectionProfiles.Validate([Profile() with { Provider=new("local",new("https://example.com"),"test") }]));
        Assert.Throws<ArgumentException>(()=>ConnectionProfiles.Validate([Profile() with { Provider=new("chatgpt",new("https://api.openai.com/v1"),"test") }]));
        ConnectionProfiles.Validate([Profile() with { Provider=new("local",new("http://127.0.0.1:1234/v1"),"test") }]);
    }
}

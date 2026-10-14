using System.Text.Json;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Core.Tests;
public sealed class ChatGptProfileTests
{
    [Fact] public void AccountBindingSurvivesPreferencesSerialization()
    {
        var profile=new ProviderSettings("chatgpt",new("https://api.openai.com/v1"),"selected-model",AccountId:"opaque-registration");
        var restored=JsonSerializer.Deserialize<ProviderSettings>(JsonSerializer.Serialize(profile));
        Assert.Equal("opaque-registration",restored!.AccountId);
        Assert.Null(new ProviderSettings("openai",new("https://api.openai.com/v1"),"api-model").AccountId);
    }
}

using ProcSight.Application.Settings;
using ProcSight.Application.Analysis;
using ProcSight.Core.Models;
using ProcSight.Core.Privacy;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class SessionPolicyTests
{
    private static AnalysisPolicy Saved()=>new(true,true,CommandLineMode.OnlineFull,ShareIdentifyingMetadata:true,Connections:[
        new("cloud",new("openai",new("https://api.openai.com/v1"),"test"),true,0),
        new("local",new("local",new("http://127.0.0.1:1234/v1"),"test"),true,1)],FallbackEnabled:true);
    [Fact] public void ElevatedDefaultsCannotDispatchOnlineButKeepLocalProcessing()
    {
        var saved=Saved(); var effective=SessionPolicy.Effective(saved,true,new());
        Assert.False(effective.ExternalAnalysisAllowed); Assert.False(effective.ShareIdentifyingMetadata);
        Assert.NotEqual(CommandLineMode.OnlineFull,effective.CommandLineMode); Assert.True(effective.AutomaticEnabled);
        Assert.True(Assert.Single(ConnectionRouter.Resolve(effective)).IsLocal);
        Assert.False(CommandLinePolicy.CanDispatch(new(new("test"),new(),[],null,0,AnalysisDestination.CloudModel),effective));
        Assert.True(saved.ShareIdentifyingMetadata); Assert.Equal(CommandLineMode.OnlineFull,saved.CommandLineMode);
    }
    [Fact] public void ConsentCannotGrantUnsavedPermissionsAndIsNotPersisted()
    {
        var effective=SessionPolicy.Effective(Saved(),true,new(true,true,true));
        Assert.True(effective.ExternalAnalysisAllowed); Assert.True(effective.ShareIdentifyingMetadata);
        Assert.Equal(CommandLineMode.OnlineFull,effective.CommandLineMode);
        Assert.False(SessionPolicy.Effective(new(),true,new(true,true,true)).ShareIdentifyingMetadata);
        Assert.DoesNotContain("ExternalAnalysisAllowed",System.Text.Json.JsonSerializer.Serialize(effective));
        Assert.Same(Saved() is { } saved? saved:throw new Exception(),SessionPolicy.Effective(saved,false,new()));
    }
}

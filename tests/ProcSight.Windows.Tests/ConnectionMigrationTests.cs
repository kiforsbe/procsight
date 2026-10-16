using ProcSight.Application.Settings;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class ConnectionMigrationTests
{
    private sealed class HeldCredentials : ICredentialStore
    {
        public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public HashSet<string> Keys=[];
        public string? Get(string id)=>null;
        public void Set(string id,string value) { Started.SetResult(); Release.Task.GetAwaiter().GetResult(); Keys.Add(id); }
        public void Remove(string id)=>Keys.Remove(id);
    }
    [Fact] public async Task KeyEnteredForOneEndpointCannotBindAfterDestinationChanges()
    {
        var store=new HeldCredentials(); var original=new ConnectionProfile("one",new("compatible",new("https://one.example/v1"),"test"),true,0);
        ConnectionProfile current=original; var published=false;
        var commit=ConnectionCredentials.StoreAsync(original,"synthetic-secret",()=>current,store,p=> { published=true; return Task.CompletedTask; },default);
        await store.Started.Task; current=current with { Provider=current.Provider with { Endpoint=new("https://two.example/v1") } }; store.Release.SetResult();
        Assert.False(await commit); Assert.False(published); Assert.Empty(store.Keys);
    }
    [Fact] public void NewProfileWithoutBoundKeyCannotBorrowLegacyCredential()
    {
        var credentials=new Credentials(); var settings=new ProviderSettings("openai",new("https://example.com/v1"),"test");
        Assert.Null(ConnectionCredentials.Read(settings,true,credentials));
        Assert.Equal("synthetic-secret",ConnectionCredentials.Read(settings,false,credentials));
    }
    private sealed class Credentials : ICredentialStore
    {
        public Dictionary<string,string> Values=new(){{"openai","synthetic-secret"}};
        public string? Get(string id)=>Values.GetValueOrDefault(id);
        public void Set(string id,string value)=>Values[id]=value;
        public void Remove(string id)=>Values.Remove(id);
    }
    [Fact] public void MigrationPreservesPermissionsAndProtectsIndependentCredentials()
    {
        var credentials=new Credentials();
        var before=new AnalysisPolicy(true,true,CommandLineMode.LocalOnly,new("openai",new("https://api.openai.com/v1"),"test"),new("local",new("http://127.0.0.1:1234/v1"),"local"));
        var after=ConnectionSettingsMigration.Migrate(before,credentials);
        Assert.Equal(before.AutomaticEnabled,after.AutomaticEnabled); Assert.Equal(before.CommandLineMode,after.CommandLineMode);
        Assert.True(after.ResearchEnabled); Assert.False(after.FallbackEnabled); Assert.Equal(2,after.Connections!.Length);
        var cloud=after.Connections[0]; Assert.NotEqual("openai",cloud.Provider.CredentialId);
        Assert.Equal("synthetic-secret",ConnectionCredentials.Read(cloud.Provider,true,credentials));
        Assert.Null(ConnectionCredentials.Read(cloud.Provider with { Endpoint=new("https://different.example/v1") },true,credentials));
        Assert.Null(ConnectionCredentials.Read(cloud.Provider with { CredentialId="context-key" },true,credentials));
        Assert.DoesNotContain("synthetic-secret",System.Text.Json.JsonSerializer.Serialize(after));
        Assert.Equal(after,ConnectionSettingsMigration.Migrate(after,credentials));
    }
}

using ProcSight.Application.Settings;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class ConnectionMigrationTests
{
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
        Assert.Equal("synthetic-secret",credentials.Get(cloud.Provider.CredentialId!));
        Assert.DoesNotContain("synthetic-secret",System.Text.Json.JsonSerializer.Serialize(after));
        Assert.Equal(after,ConnectionSettingsMigration.Migrate(after,credentials));
    }
}

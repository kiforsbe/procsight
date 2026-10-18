using ProcSight.Core.Models;
using System.Text.Json;
namespace ProcSight.Application.Settings;
public static class ConnectionCredentials
{
    private sealed class CredentialValue
    {
        public string Kind { get; set; }="";
        public string Endpoint { get; set; }="";
        public string Secret { get; set; }="";
    }
    public static string Encode(ProviderSettings provider,string secret)=>JsonSerializer.Serialize(new CredentialValue { Kind=provider.Id,Endpoint=provider.Endpoint.AbsoluteUri,Secret=secret });
    public static async Task<bool> StoreAsync(ConnectionProfile authorized,string secret,Func<ConnectionProfile?> current,
        ICredentialStore store,Func<ConnectionProfile,Task> publish,CancellationToken ct)
    {
        bool Matches(ConnectionProfile? profile)=>profile is not null&&profile.ProfileId==authorized.ProfileId&&
            profile.Provider.Id==authorized.Provider.Id&&profile.Provider.Endpoint==authorized.Provider.Endpoint;
        if(string.IsNullOrWhiteSpace(secret)||!Matches(current())) return false;
        var key="connection-"+Guid.NewGuid().ToString("N"); var bound=false;
        try {
            await Task.Run(()=>store.Set(key,Encode(authorized.Provider,secret)),ct);
            ct.ThrowIfCancellationRequested(); var latest=current();
            if(!Matches(latest)) return false;
            await publish(latest! with { Provider=latest.Provider with { CredentialId=key } }); bound=true; return true;
        } finally { if(!bound) { try { store.Remove(key); } catch { } } }
    }
    public static string? Read(ProviderSettings provider,bool profilesConfigured,ICredentialStore store)
    {
        if(provider.CredentialId is not { } id) return profilesConfigured?null:provider.Id is "openai" or "compatible"?store.Get(provider.Id):null;
        if(!id.StartsWith("connection-",StringComparison.Ordinal)||id.Length!=43||!Guid.TryParseExact(id[11..],"N",out _)) return null;
        var raw=store.Get(id); if(raw is null||raw.Length>2560) return null;
        try { var value=JsonSerializer.Deserialize<CredentialValue>(raw);
            return value?.Kind==provider.Id&&value.Endpoint==provider.Endpoint.AbsoluteUri?value.Secret:null;
        } catch(JsonException) { return null; }
    }
}

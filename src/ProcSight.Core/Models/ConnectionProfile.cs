namespace ProcSight.Core.Models;
public sealed record ConnectionProfile(string ProfileId,ProviderSettings Provider,bool Enabled,int Priority)
{
    public bool IsLocal=>Provider.Id=="local";
    public string Label=>$"{Provider.Id switch { "chatgpt"=>"ChatGPT plan", "openai"=>"OpenAI API", "local"=>"Local model", _=>"Compatible API" }} · {Provider.Model}";
}
public static class ConnectionProfiles
{
    public static void Validate(IReadOnlyList<ConnectionProfile> profiles)
    {
        if(profiles.Count>32) throw new ArgumentException("At most 32 connections are supported.");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var profile in profiles)
        {
            if(profile is null || !ValidId(profile.ProfileId) || !ids.Add(profile.ProfileId) || profile.Priority<0)
                throw new ArgumentException("Connection IDs must be unique and priority must be nonnegative.");
            var p=profile.Provider;
            if(p is null || p.Id is not ("chatgpt" or "openai" or "compatible" or "local") || string.IsNullOrWhiteSpace(p.Model) || p.Model.Length>256)
                throw new ArgumentException("Choose a provider and model for the connection.");
            var u=p.Endpoint;
            if(u is null || !u.IsAbsoluteUri || u.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(u.UserInfo) ||
                !string.IsNullOrEmpty(u.Query) || !string.IsNullOrEmpty(u.Fragment) || u.AbsoluteUri.Length>2048 || (u.Scheme!="https"&&!u.IsLoopback))
                throw new ArgumentException("Use an HTTPS endpoint (or loopback HTTP) without credentials, query or fragment.");
            if(profile.IsLocal && !(u.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase)||
                System.Net.IPAddress.TryParse(u.Host.Trim('[',']'),out var ip)&&System.Net.IPAddress.IsLoopback(ip)))
                throw new ArgumentException("Local models require a loopback endpoint.");
            if(p.Id=="chatgpt" && (string.IsNullOrWhiteSpace(p.AccountId)||p.AccountId.Length>256 || u.AbsoluteUri.TrimEnd('/')!="https://api.openai.com/v1"))
                throw new ArgumentException("Connect a ChatGPT account and choose its model.");
            if(p.CredentialId is not null && !ValidId(p.CredentialId)) throw new ArgumentException("Invalid credential reference.");
        }
    }
    private static bool ValidId(string? id)=>id is { Length:>0 and <=128 } && id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_');
}

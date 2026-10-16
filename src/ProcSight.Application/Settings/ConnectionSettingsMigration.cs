using ProcSight.Core.Models;
namespace ProcSight.Application.Settings;
public static class ConnectionSettingsMigration
{
    public static AnalysisPolicy Migrate(AnalysisPolicy policy,ICredentialStore credentials)
    {
        if(policy.Connections is not null) { ConnectionProfiles.Validate(policy.Connections); return policy; }
        var profiles=new List<ConnectionProfile>();
        foreach(var settings in new[]{policy.Provider,policy.LocalModel}.OfType<ProviderSettings>())
        {
            var id=Guid.NewGuid().ToString("N"); var next=settings;
            if(settings.Id is "openai" or "compatible")
            {
                var key="connection-"+id;
                if(credentials.Get(settings.CredentialId??settings.Id) is { } secret) credentials.Set(key,secret);
                next=settings with { CredentialId=key };
            }
            profiles.Add(new(id,next,true,profiles.Count));
        }
        ConnectionProfiles.Validate(profiles);
        return policy with { Connections=profiles.ToArray(),FallbackEnabled=false,
            ResearchProfileId=policy.ResearchEnabled?profiles.FirstOrDefault(p=>p.Provider.Id=="openai")?.ProfileId:null };
    }
}

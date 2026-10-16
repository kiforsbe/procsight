using ProcSight.Core.Models;
namespace ProcSight.Application.Analysis;
public static class ConnectionRouter
{
    public static IReadOnlyList<ConnectionProfile> Resolve(AnalysisPolicy policy)
    {
        if(policy.Connections is { } profiles)
            return profiles.Where(p=>p.Enabled).OrderBy(p=>policy.CommandLineMode==CommandLineMode.LocalOnly&&!p.IsLocal?1:0)
                .ThenBy(p=>p.Priority).ToArray();
        var legacy=new[]{policy.Provider,policy.LocalModel}.OfType<ProviderSettings>()
            .Select((p,i)=>new ConnectionProfile("legacy-"+i,p,true,i));
        return legacy.OrderBy(p=>policy.CommandLineMode==CommandLineMode.LocalOnly&&!IsLocal(policy,p)?1:0).ToArray();
    }
    public static bool IsLocal(AnalysisPolicy policy,ConnectionProfile connection)=>connection.IsLocal||
        policy.Connections is null&&ReferenceEquals(connection.Provider,policy.LocalModel);
    public static bool CanFallback(ProviderError error)=>error.Kind is ProviderErrorKind.Auth or ProviderErrorKind.RateLimited or ProviderErrorKind.Transient;
    public static ProviderSettings? Research(AnalysisPolicy policy,ProviderSettings primary)=>policy.Connections is null
        ? primary
        : policy.Connections.FirstOrDefault(p=>p.Enabled&&p.ProfileId==policy.ResearchProfileId&&p.Provider.Id=="openai")?.Provider;
}

using ProcSight.Core.Models;
namespace ProcSight.Application.Settings;
public static class ConnectionProfileActions
{
    public static AnalysisPolicy Remove(AnalysisPolicy policy,string profileId)
    {
        if(policy.Connections is not { } profiles||!profiles.Any(p=>p.ProfileId==profileId)) return policy;
        return policy with {
            Connections=profiles.Where(p=>p.ProfileId!=profileId).Select((p,i)=>p with { Priority=i }).ToArray(),
            ResearchProfileId=policy.ResearchProfileId==profileId?null:policy.ResearchProfileId
        };
    }
}

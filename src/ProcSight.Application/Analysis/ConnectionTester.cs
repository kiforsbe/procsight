using ProcSight.Application.Budget;
using ProcSight.Application.Providers;
using ProcSight.Core.Models;
namespace ProcSight.Application.Analysis;
public sealed class ConnectionTester(Func<ProviderSettings,bool,IAnalysisProvider> providers,IUsageLedger usage,AnalysisActivityLog activity,Func<AnalysisPolicy>? currentPolicy=null)
{
    public async Task<ProviderResult> TestAsync(ConnectionProfile profile,AnalysisPolicy policy,CancellationToken ct)
    {
        ConnectionProfiles.Validate([profile]);
        if(!profile.IsLocal&&!policy.ExternalAnalysisAllowed) return new(null,new(ProviderErrorKind.Unsupported,"Online analysis requires explicit consent for this administrator session."));
        if(!profile.IsLocal&&!await usage.TryReserveAsync(UsageKind.BillableCall,DateOnly.FromDateTime(DateTime.Now),policy.Limits??new(),ct))
            return new(null,new(ProviderErrorKind.Unsupported,"Daily call budget reached."));
        ct.ThrowIfCancellationRequested();
        if(currentPolicy is not null&&(currentPolicy().Revision!=policy.Revision||!profile.IsLocal&&!currentPolicy().ExternalAnalysisAllowed))
            return new(null,new(ProviderErrorKind.Cancelled,"Connection test cancelled because settings or session consent changed."));
        var payload=new AnalysisPayload(new("connection-test"),new(){["name"]="synthetic-connection-test.exe"},[],null,
            policy.Revision,profile.IsLocal?AnalysisDestination.LocalModel:AnalysisDestination.CloudModel);
        var id=activity.Begin(payload,profile.Provider,"Synthetic connection test",kind:AnalysisActivityKind.ConnectionTest);
        try { var result=await providers(profile.Provider,profile.IsLocal).AnalyzeAsync(payload,ct);
            ct.ThrowIfCancellationRequested();
            if(currentPolicy is not null&&currentPolicy().Revision!=policy.Revision) { activity.Fail(id,true); return new(null,new(ProviderErrorKind.Cancelled,"Connection test cancelled because settings changed.")); }
            activity.Complete(id,result,payload); return result; }
        catch(OperationCanceledException) { activity.Fail(id,true); throw; }
        catch { activity.Fail(id); return new(null,new(ProviderErrorKind.Transient,"Connection test failed. No real process metadata was sent.")); }
    }
}

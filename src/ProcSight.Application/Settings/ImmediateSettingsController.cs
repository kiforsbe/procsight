using ProcSight.Core.Models;
namespace ProcSight.Application.Settings;
public enum SettingsFailure { None, Invalid, Storage, Superseded }
public sealed record SettingsApplyResult(bool Applied,bool Persisted,long Revision,SettingsFailure Failure);
public sealed class ImmediateSettingsController(IPreferencesStore store,Func<AnalysisPolicy,CancellationToken,Task> change,
    AnalysisPolicy initial,PresentationPreferences presentation)
{
    private readonly SemaphoreSlim commits=new(1,1);
    private long generation,revision=initial.Revision;
    public AnalysisPolicy Current { get; private set; }=initial;
    private AnalysisPolicy persisted=initial;
    private PresentationPreferences savedPresentation=presentation;
    public async Task<SettingsApplyResult> ApplyAsync(AnalysisPolicy candidate,PresentationPreferences view,CancellationToken ct)
    {
        var version=Interlocked.Increment(ref generation);
        try { if(candidate.Connections is not null) ConnectionProfiles.Validate(candidate.Connections); }
        catch(ArgumentException) { return new(false,false,Current.Revision,SettingsFailure.Invalid); }
        // Revoke permissions before waiting for disk or an older commit. Never enable in this overlay.
        var restrictive=Current with {
            AutomaticEnabled=Current.AutomaticEnabled&&candidate.AutomaticEnabled,
            ResearchEnabled=Current.ResearchEnabled&&candidate.ResearchEnabled,
            ShareIdentifyingMetadata=Current.ShareIdentifyingMetadata&&candidate.ShareIdentifyingMetadata,
            CommandLineMode=candidate.CommandLineMode==CommandLineMode.OnlineFull?Current.CommandLineMode:candidate.CommandLineMode,
            FallbackEnabled=Current.FallbackEnabled&&candidate.FallbackEnabled,
            Connections=Current.Connections?.Select(p=>p with { Enabled=p.Enabled&&candidate.Connections?.Any(c=>c.ProfileId==p.ProfileId&&c.Enabled)==true }).ToArray()
        };
        if(!Equivalent(Current,restrictive)) {
            restrictive=restrictive with { Revision=Interlocked.Increment(ref revision) };
            Current=restrictive; await change(restrictive,ct);
        }
        await commits.WaitAsync(ct);
        try {
            if(version!=Volatile.Read(ref generation)) return new(false,false,Current.Revision,SettingsFailure.Superseded);
            if(Equivalent(candidate,persisted)&&view==savedPresentation&&Equivalent(candidate,Current)) return new(true,true,Current.Revision,SettingsFailure.None);
            var next=candidate with { Revision=Interlocked.Increment(ref revision) };
            try { await Task.Run(()=>store.Save(next,view),ct); }
            catch(OperationCanceledException) { throw; }
            catch { return new(false,false,Current.Revision,SettingsFailure.Storage); }
            persisted=next; savedPresentation=view;
            if(version!=Volatile.Read(ref generation)) return new(false,true,Current.Revision,SettingsFailure.Superseded);
            Current=next; await change(next,ct);
            return new(true,true,next.Revision,SettingsFailure.None);
        } finally { commits.Release(); }
    }
    public Task FlushAsync(CancellationToken ct)=>FlushCoreAsync(ct);
    private async Task FlushCoreAsync(CancellationToken ct) { await commits.WaitAsync(ct); commits.Release(); }
    private static bool Equivalent(AnalysisPolicy a,AnalysisPolicy b)=>a with { Revision=0,Connections=null }==b with { Revision=0,Connections=null }&&
        (a.Connections??[]).SequenceEqual(b.Connections??[]);
}

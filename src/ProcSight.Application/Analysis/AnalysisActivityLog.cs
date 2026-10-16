using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using ProcSight.Core.Models;
using ProcSight.Core.Privacy;
namespace ProcSight.Application.Analysis;

public enum AnalysisActivityKind { Analysis, Research, ConnectionTest }
public enum AnalysisActivityStatus { Running, Succeeded, Failed, Cancelled }
public sealed record ActivityAssessment(AssessmentAxis Axis,string Value,string Reason,Confidence Confidence);
public sealed record AnalysisActivityEntry(Guid Id,AnalysisActivityKind Kind,AnalysisActivityStatus Status,
    DateTimeOffset StartedAt,DateTimeOffset? CompletedAt,TimeSpan? Elapsed,string ProviderId,string Model,
    AnalysisDestination Destination,string SourceName,int? ProcessId,string? ServiceIdentity,int Attempt,
    IReadOnlyDictionary<string,string> Metadata,IReadOnlyList<string> MetadataFields,int EvidenceCount,
    bool CommandIncluded,IReadOnlyList<ActivityAssessment> Assessments,int ProposedRuleCount=0,
    int ReturnedEvidenceCount=0,ProviderErrorKind? ErrorKind=null,ProviderFailureReason? FailureReason=null,ProviderErrorKind? FallbackReason=null);

/// <summary>Bounded transient product activity; never retains transport payloads or exception text.</summary>
public sealed class AnalysisActivityLog(TimeProvider? clock=null)
{
    public const int Capacity=200;
    private readonly object gate=new();
    private readonly List<AnalysisActivityEntry> entries=[];
    private readonly TimeProvider time=clock ?? TimeProvider.System;
    private bool enabled=true;
    public event EventHandler? Changed;
    public bool Enabled { get { lock(gate) return enabled; } set { lock(gate) enabled=value; Notify(); } }
    public IReadOnlyList<AnalysisActivityEntry> Snapshot() { lock(gate) return Array.AsReadOnly(entries.AsEnumerable().Reverse().ToArray()); }
    public void Clear() { lock(gate) entries.Clear(); Notify(); }
    public Guid? Begin(AnalysisPayload payload,ProviderSettings provider,string sourceName,int? processId=null,
        string? serviceIdentity=null,AnalysisActivityKind kind=AnalysisActivityKind.Analysis,int attempt=1,ProviderErrorKind? fallbackReason=null)
    {
        try
        {
            lock(gate)
            {
                if(!enabled) return null;
                var metadata=payload.Metadata.Where(f=>f.Key is "name" or "product" or "signer" or "hash" or "originalName" ||
                    f.Key=="commandRole" && f.Value is "Renderer" or "Utility" or "Worker" or "CLI")
                    .Take(6).ToDictionary(f=>f.Key,f=>Safe(f.Value,256));
                var fields=payload.Metadata.Keys.Where(f=>f is "name" or "product" or "signer" or "hash" or "originalName" or
                    "commandRole" or "path" or "owner" or "ownerIdentity" or "service" or "package" or "version" or "company" or "commandContext")
                    .Order(StringComparer.Ordinal).ToArray();
                var id=Guid.NewGuid();
                entries.Add(new(id,kind,AnalysisActivityStatus.Running,time.GetUtcNow(),null,null,Safe(provider.Id,128),Safe(provider.Model,128),
                    payload.Destination,Safe(sourceName,256),processId,serviceIdentity is null?null:Safe(serviceIdentity,256),Math.Max(1,attempt),
                    new ReadOnlyDictionary<string,string>(metadata),Array.AsReadOnly(fields),payload.Evidence.Count,payload.CommandLine is not null,
                    Array.AsReadOnly(Array.Empty<ActivityAssessment>()),FallbackReason:fallbackReason));
                if(entries.Count>Capacity) entries.RemoveAt(0);
                return id;
            }
        }
        catch { return null; } // Diagnostics must not interfere with provider work.
        finally { Notify(); }
    }
    public void Complete(Guid? id,ProviderResult result,AnalysisPayload payload)
    {
        try
        {
            var projected=result.Proposal is null?null:OutputPrivacyFilter.Project(result.Proposal,payload);
            Finish(id,row=>row with {
                Status=Status(result.Error),ErrorKind=result.Error?.Kind,FailureReason=result.Error?.FailureReason,
                Assessments=Array.AsReadOnly((projected?.Assessments ?? []).Take(3)
                    .Select(a=>new ActivityAssessment(a.Axis,AssessmentValue(a),Safe(a.Reason,1024),a.Confidence)).ToArray()),
                ProposedRuleCount=result.Proposal?.Rules.Count ?? 0 });
        }
        catch { Fail(id); }
    }
    public void CompleteResearch(Guid? id,ResearchResult result)=>Finish(id,row=>row with {
        Status=Status(result.Error),ErrorKind=result.Error?.Kind,ReturnedEvidenceCount=result.Evidence.Count });
    public void Fail(Guid? id,bool cancelled=false)=>Finish(id,row=>row with {
        Status=cancelled?AnalysisActivityStatus.Cancelled:AnalysisActivityStatus.Failed,
        ErrorKind=cancelled?ProviderErrorKind.Cancelled:null });
    private static AnalysisActivityStatus Status(ProviderError? error)=>error is null?AnalysisActivityStatus.Succeeded:
        error.Kind==ProviderErrorKind.Cancelled?AnalysisActivityStatus.Cancelled:AnalysisActivityStatus.Failed;
    private void Finish(Guid? id,Func<AnalysisActivityEntry,AnalysisActivityEntry> update)
    {
        try
        {
            lock(gate)
            {
                var index=entries.FindIndex(e=>e.Id==id && e.Status==AnalysisActivityStatus.Running);
                if(index<0) return;
                var now=time.GetUtcNow(); var entry=entries[index];
                entries[index]=update(entry) with { CompletedAt=now,Elapsed=now>=entry.StartedAt?now-entry.StartedAt:TimeSpan.Zero };
            }
        }
        catch { }
        finally { Notify(); }
    }
    private void Notify()
    {
        foreach(var handler in Changed?.GetInvocationList() ?? [])
            try { ((EventHandler)handler)(this,EventArgs.Empty); } catch { }
    }
    private static string Safe(string value,int limit)
    {
        // Apply the shared projection first, then omit all remaining paths/URLs from this log.
        var normalized=MetadataSanitizer.Normalize(value.Length>4096?value[..4096]:value);
        normalized=Regex.Replace(normalized,@"(?i)(?:https?://\S+|(?:[a-z]:|<UserProfile>|<NetworkRoot>)\\[^\r\n\t]+|\\\\[^\r\n\t]+|(?<!\w)/[\w.~-]+(?:/[^\s]+)*)",
            "<Private location omitted>",RegexOptions.None,TimeSpan.FromMilliseconds(100));
        return normalized.Length>limit?normalized[..limit]:normalized;
    }
    private static string AssessmentValue(Assessment assessment)=>assessment.Axis switch {
        AssessmentAxis.Purpose when assessment.Value is "OSComponent" or "Application" or "BackgroundHelper" or "Updater" or "DeveloperTool" or "Unknown"=>assessment.Value,
        AssessmentAxis.Security when assessment.Value is "NoConcernObserved" or "Suspicious" or "KnownThreat" or "Unknown"=>assessment.Value,
        AssessmentAxis.Optionality when assessment.Value is "Essential" or "Optional" or "PotentiallyUnwanted" or "PossibleLeftover" or "Unknown"=>assessment.Value,
        _=>"Unknown" };
}

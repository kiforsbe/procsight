using ProcSight.Application.Knowledge;
using ProcSight.Application.Budget;
using ProcSight.Application.Providers;
using ProcSight.Application.Research;
using ProcSight.Core.Models;
using ProcSight.Core.Identity;
using ProcSight.Core.Privacy;
using ProcSight.Core.Rules;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
namespace ProcSight.Application.Analysis;
public sealed class AnalysisCoordinator(IKnowledgeStore store,IUsageLedger ledger,
    Func<ProviderSettings,bool,IAnalysisProvider> providers,
    Func<ProviderSettings,IResearchProvider?>? research=null,TimeProvider? clock=null,byte[]? contextKey=null,
    AnalysisActivityLog? activity=null) : IAsyncDisposable
{
    private sealed record Work(ResolvedEntity Entity,AnalysisPolicy Policy,CancellationToken PolicyToken,bool Manual);
    private readonly Channel<Work> queue=Channel.CreateBounded<Work>(128);
    private readonly Channel<AnalysisUpdate> updates=Channel.CreateBounded<AnalysisUpdate>(new BoundedChannelOptions(1024) { FullMode=BoundedChannelFullMode.DropOldest });
    private readonly CancellationTokenSource lifetime=new();
    private CancellationTokenSource policyCancellation=new();
    private readonly ConcurrentDictionary<AnalysisIdentity,ResolvedEntity> current=[];
    private readonly ConcurrentDictionary<AnalysisIdentity,string> fingerprints=[];
    private readonly ConcurrentDictionary<AnalysisIdentity,DateTimeOffset> reviewTimes=[];
    private readonly TimeProvider time=clock ?? TimeProvider.System;
    private readonly byte[] key=contextKey ?? RandomNumberGenerator.GetBytes(32);
    private readonly LocalAnalysisEngine engine=new();
    private readonly AnalysisProjection projection=new();
    private readonly object policyLock=new();
    private volatile AnalysisPolicy policy=new();
    private bool policyEstablished;
    private Task? worker;
    private int outstanding;
    private readonly ConcurrentDictionary<string,ProviderSettings> authPaused=[];
    public async Task ObserveAsync(MachineSnapshot snapshot,AnalysisPolicy newPolicy,CancellationToken ct)
    {
        if(newPolicy!=policy) await ChangePolicyAsync(newPolicy,ct);
        if(newPolicy!=policy) return; // A collected snapshot cannot restore an older permission.
        var entities=ResolveEntities(snapshot,newPolicy);
        var live=entities.Select(e=>e.Identity).ToHashSet();
        foreach(var id in current.Keys.Where(id=>!live.Contains(id))) { current.TryRemove(id,out _); fingerprints.TryRemove(id,out _); reviewTimes.TryRemove(id,out _); }
        foreach(var entity in entities.OrderBy(e=>e.Owner is null?0:1))
        {
            ct.ThrowIfCancellationRequested();
            if(newPolicy!=policy) return;
            current[entity.Identity]=entity;
            var fingerprint=IdentityResolver.MakeIdentity(JsonSerializer.Serialize(entity.Facts.OrderBy(f=>f.Key))+
                newPolicy.Revision+entity.Process?.Executable.Signature).Value;
            if(fingerprints.TryGetValue(entity.Identity,out var previous)&&previous==fingerprint &&
                (!reviewTimes.TryGetValue(entity.Identity,out var reviewAt)||reviewAt>time.GetUtcNow())) continue;
            fingerprints[entity.Identity]=fingerprint;
            await EvaluateAsync(entity,newPolicy,false,ct);
        }
    }
    public IReadOnlyList<ResolvedEntity> ResolveEntities(MachineSnapshot snapshot,AnalysisPolicy config)=>
        new IdentityResolver().Resolve(snapshot,[]).Select(e=>WithCommandContext(e,config)).ToList();
    private ResolvedEntity WithCommandContext(ResolvedEntity entity,AnalysisPolicy config)
    {
        var command=entity.Process?.CommandLine?.Raw ?? entity.Service?.BinaryPath;
        if(command is null) return entity;
        var digest=Convert.ToHexString(HMACSHA256.HashData(key,Encoding.UTF8.GetBytes(command)));
        var origin=config.CommandLineMode!=CommandLineMode.OnlineFull?DataOrigin.LocalCommandLine:DataOrigin.OnlineCommandLine;
        var role=LocalCommandRules.Match(entity.Process?.Executable.Name ?? entity.Service?.HostExecutable?.Name ?? "",command)?.Category ?? "CLI";
        var facts=new Dictionary<string,string>(entity.Facts){["commandRole"]=role,["commandContext"]=digest};
        return entity with { ShareableIdentity=entity.Identity,Identity=IdentityResolver.MakeIdentity(entity.Identity.Value+digest),Facts=facts,
            Evidence=[..entity.Evidence,new("command:"+digest,origin,time.GetUtcNow(),null,"CommandRole",new(){["commandRole"]=role})],
            Origins=[..entity.Origins,origin] };
    }
    private async Task EvaluateAsync(ResolvedEntity entity,AnalysisPolicy config,bool manual,CancellationToken ct)
    {
        var knowledge=await store.ReadCandidatesAsync(entity,ct);
        if(config!=policy) return;
        var local=EvaluateLocal(entity,knowledge);
        ScheduleReview(entity,local,knowledge);
        var historical=knowledge.Assessments.Any(a=>a.ExpiresAt<=time.GetUtcNow()) || knowledge.Rules.Any(r=>
            (r.ReviewAt<=time.GetUtcNow()||r.ExpiresAt<=time.GetUtcNow()) && r.Predicates.All(p=>
                entity.Facts.TryGetValue(p.Field,out var value)&&PredicateMatcher.Matches(p,value)));
        Publish(entity,local.Assessments.Count>0?AnalysisState.Local:historical?AnalysisState.Stale:AnalysisState.Unknown,local);
        if(!manual && entity.Process is not null && !entity.Facts.ContainsKey("path") && !entity.Facts.ContainsKey("hash"))
        {
            Publish(entity,AnalysisState.Unknown,local,"Executable identity metadata is unavailable. Automatic analysis cannot authenticate a process from its name alone. Select Analyze for a limited assessment of the available metadata.");
            return;
        }
        if(local.UnresolvedAxes.Count==0&&!manual || !config.AutomaticEnabled&&!manual) return;
        if(ConnectionRouter.Resolve(config).Count==0) {
            if(manual||config.AutomaticEnabled) Publish(entity,AnalysisState.Failed,local,"No enabled connection is configured. Enable a connection in Settings.");
            return;
        }
        CancellationToken token; lock(policyLock) token=policyCancellation.Token;
        Interlocked.Increment(ref outstanding);
        if(!queue.Writer.TryWrite(new(entity,config,token,manual)))
        { Interlocked.Decrement(ref outstanding); Publish(entity,AnalysisState.BudgetPaused,local,"Analysis queue full; request re-analysis later."); return; }
        Publish(entity,AnalysisState.Queued,local);
        lock(policyLock) worker ??=Task.Run(RunAsync);
    }
    private async Task RunAsync()
    {
        try
        {
            await foreach(var work in queue.Reader.ReadAllAsync(lifetime.Token))
            {
                try { await AnalyzeAsync(work); }
                catch(OperationCanceledException) { }
                catch { Publish(work.Entity,AnalysisState.Failed,Empty(work.Entity),"Analysis unavailable; local collection continues."); }
                finally { Interlocked.Decrement(ref outstanding); }
            }
        }
        catch(OperationCanceledException) { }
    }
    private async Task AnalyzeAsync(Work work)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,work.PolicyToken);
        var ct=linked.Token; ct.ThrowIfCancellationRequested();
        if(work.Policy!=policy || !current.ContainsKey(work.Entity.Identity)) return;
        var config=work.Policy; var limits=config.Limits ?? new(); var date=DateOnly.FromDateTime(time.GetLocalNow().Date);
        var entity=work.Entity;
        var knowledge=await store.ReadCandidatesAsync(entity,ct); var local=EvaluateLocal(entity,knowledge);
        if(local.UnresolvedAxes.Count==0&&!work.Manual) { Publish(entity,AnalysisState.Local,local); return; }
        if(!work.Manual&&!await ledger.TryReserveAsync(UsageKind.AutomaticJob,date,limits,ct))
        { Publish(entity,AnalysisState.BudgetPaused,local,"Daily analysis job limit reached."); return; }
        var route=ConnectionRouter.Resolve(config);
        ProviderErrorKind? fallbackReason=null;
        foreach(var connection in config.FallbackEnabled?route:route.Take(1))
        {
        if(authPaused.ContainsKey(connection.ProfileId)) {
            if(config.FallbackEnabled) { fallbackReason=ProviderErrorKind.Auth; continue; }
            Publish(entity,AnalysisState.Failed,local,"This connection needs sign-in or updated credentials in Settings."); return;
        }
        var useLocal=ConnectionRouter.IsLocal(config,connection);
        var selected=connection.Provider;
        var provider=providers(selected,useLocal);
        Publish(entity,AnalysisState.Analyzing,local);
        var researchSettings=ConnectionRouter.Research(config,selected);
        var researchOutcome=!config.ResearchEnabled?ResearchOutcome.Disabled:useLocal?ResearchOutcome.LocalAnalysis:ResearchOutcome.Unavailable;
        var inlineResearch=config.ResearchEnabled&&!useLocal&&researchSettings==selected&&selected.Id is "chatgpt" or "openai";
        if(inlineResearch) researchOutcome=ResearchOutcome.Searching;
        if(!inlineResearch&&config.ResearchEnabled && !useLocal && researchSettings is not null && research?.Invoke(researchSettings) is { } researchProvider)
        {
            // Reserve the request plus a research allowance. API-key search enforces one tool call;
            // plan search has no server-enforced cap and this allowance is not a spending guarantee.
            if(!await ledger.TryReserveAsync(UsageKind.BillableCall,date,limits,ct) || !await ledger.TryReserveAsync(UsageKind.BillableCall,date,limits,ct))
            { Publish(entity,AnalysisState.BudgetPaused,local,"Research call budget reached."); return; }
            var request=projection.BuildResearchRequest(entity,config);
            var researchPayload=new AnalysisPayload(entity.Identity,request.IdentityTerms,[],null,config.Revision,AnalysisDestination.CloudModel);
            var researchActivity=activity?.Begin(researchPayload,researchSettings,entity.DisplayName,entity.Process?.Key.Pid,
                entity.Service?.Name,AnalysisActivityKind.Research);
            ResearchResult researched;
            try
            {
                researched=await researchProvider.ResearchAsync(request,ct);
                activity?.CompleteResearch(researchActivity,researched);
            }
            catch(OperationCanceledException) { activity?.Fail(researchActivity,true); throw; }
            catch { activity?.Fail(researchActivity); throw; }
            if(researched.Error is null) entity=entity with { Evidence=[..entity.Evidence,..researched.Evidence] };
            researchOutcome=researched.Error is not null?ResearchOutcome.Failed:researched.Evidence.Count>0?ResearchOutcome.SourcesFound:ResearchOutcome.NoSources;
        }
        var payload=projection.Build(entity,config,useLocal?AnalysisDestination.LocalModel:AnalysisDestination.CloudModel);
        if(inlineResearch) payload=AnalysisProjection.ForResearchAnalysis(payload);
        for(var retry=0;retry<=2;retry++)
        {
            ct.ThrowIfCancellationRequested(); if(work.Policy!=policy || !CommandLinePolicy.CanDispatch(payload,policy)) return;
            if(!useLocal&&!await ledger.TryReserveAsync(UsageKind.BillableCall,date,limits,ct))
            { Publish(entity,AnalysisState.BudgetPaused,local,"Daily provider-call limit reached."); return; }
            if(inlineResearch&&!await ledger.TryReserveAsync(UsageKind.BillableCall,date,limits,ct))
            { Publish(entity,AnalysisState.BudgetPaused,local,"Research request allowance reached."); return; }
            var attemptActivity=activity?.Begin(payload,selected,entity.DisplayName,entity.Process?.Key.Pid,entity.Service?.Name,
                attempt:retry+1,fallbackReason:fallbackReason,research:researchOutcome);
            ProviderResult result;
            try
            {
                result=await provider.AnalyzeAsync(payload,ct);
                // Record the response immediately, before cancellation or rule validation/storage.
                activity?.Complete(attemptActivity,result,payload);
            }
            catch(OperationCanceledException) { activity?.Fail(attemptActivity,true); throw; }
            catch { activity?.Fail(attemptActivity); throw; }
            if(result.Error is { } error)
            {
                if(error.Kind==ProviderErrorKind.Auth) authPaused[connection.ProfileId]=selected;
                if(retry<2 && error.Kind is ProviderErrorKind.Transient or ProviderErrorKind.RateLimited)
                { await Task.Delay(error.RetryAfter ?? TimeSpan.FromSeconds(1<<retry),time,ct); continue; }
                if(config.FallbackEnabled&&ConnectionRouter.CanFallback(error)) { fallbackReason=error.Kind; break; }
                Publish(entity,AnalysisState.Failed,local,error.Message); return;
            }
            if(result.Proposal is not { } proposal) { Publish(entity,AnalysisState.Unknown,local); return; }
            ct.ThrowIfCancellationRequested(); if(work.Policy!=policy) return;
            if(inlineResearch) researchOutcome=result.ResearchEvidence is { Count:>0 }?ResearchOutcome.SourcesFound:ResearchOutcome.NoSources;
            var evaluatedPayload=payload with { Evidence=[..payload.Evidence,..inlineResearch?result.ResearchEvidence??[]:[]] };
            var projected=ExactAnalysisRule.Ensure(OutputPrivacyFilter.Project(proposal,evaluatedPayload),evaluatedPayload,time.GetUtcNow());
            var providerId=MetadataSanitizer.Normalize(selected.Id);
            var model=MetadataSanitizer.Normalize(selected.Model);
            var provenance=new RuleProvenance(providerId[..Math.Min(providerId.Length,128)],model[..Math.Min(model.Length,256)],
                payload.Destination,time.GetUtcNow(),researchOutcome);
            // Keep local ownership boundaries even when the provider never receives these fields.
            var scopeFacts=entity.Facts.Where(f=>f.Key is "path" or "ownerIdentity" or "service")
                .Where(f=>f.Value.Length is >0 and <=512).ToDictionary(f=>f.Key,f=>f.Value);
            var scopeEvidence=new EvidenceRecord("local-scope:"+(entity.ShareableIdentity ?? entity.Identity).Value,
                DataOrigin.ShareableMetadata,time.GetUtcNow(),null,"LocalRuleScope",scopeFacts);
            var storedEvidence=evaluatedPayload.Evidence.Concat(new[]{scopeEvidence}).ToList();
            var invocationEvidence=payload.CommandLine is not null && entity.Facts.TryGetValue("commandContext",out var context)?
                new EvidenceRecord("invocation-scope:"+context,payload.Destination==AnalysisDestination.LocalModel?DataOrigin.LocalCommandLine:DataOrigin.OnlineCommandLine,
                    time.GetUtcNow(),null,"InvocationScope",new(){["commandContext"]=context}):null;
            if(invocationEvidence is not null) storedEvidence.Add(invocationEvidence);
            var rules=projected.Rules.Select(rule=> {
                var originalReasons=rule.State==RuleState.Rejected?new RuleValidator().Validate(rule,evaluatedPayload.Evidence,time.GetUtcNow()).Reasons
                    .Where(reason=>invocationEvidence is null||RuleValidator.ReasonCode(reason)!=RuleRejectionReason.MissingInvocationScope).Select(RuleValidator.ReasonCode).ToList():[];
                // Provider responses cannot choose their attribution or retain account identifiers.
                var scoped=rule with { Provenance=provenance,Predicates=[..rule.Predicates,..scopeFacts.Where(f=>!rule.Predicates.Any(p=>p.Field==f.Key))
                    .Select(f=>new Predicate(PredicateKind.Exact,f.Key,f.Value,[scopeEvidence.Id]))] };
                if(invocationEvidence is not null) scoped=scoped with { Predicates=[..scoped.Predicates,
                    new(PredicateKind.Exact,"commandContext",invocationEvidence.Facts["commandContext"],[invocationEvidence.Id])] };
                var validation=new RuleValidator().Validate(scoped,storedEvidence,time.GetUtcNow());
                var reasons=(scoped.RejectionReasons??[]).Concat(originalReasons).Concat(validation.Reasons.Select(RuleValidator.ReasonCode)).Distinct().ToList();
                if(scoped.State==RuleState.Rejected&&reasons.Count==0) reasons.Add(RuleRejectionReason.Unspecified);
                return scoped with { State=reasons.Count>0?RuleState.Rejected:RuleState.Active,RejectionReasons=reasons };
            }).ToList();
            var entry=new KnowledgeEntry(entity.Identity,time.GetUtcNow(),projected.Assessments,storedEvidence,rules,knowledge.Corrections,
                rules.Where(r=>r.State==RuleState.Active).Select(r=>r.Id).ToList(),projected.IdentityLabel,projected.Role);
            await store.SaveAsync(entry,ct);
            local=EvaluateLocal(entity,await store.ReadCandidatesAsync(entity,ct));
            ScheduleReview(entity,local,await store.ReadCandidatesAsync(entity,ct));
            Publish(entity,local.UnresolvedAxes.Count>0?AnalysisState.Unknown:AnalysisState.Local,local);
            return;
        }
        }
        Publish(entity,AnalysisState.Failed,local,"Enabled connections are unavailable. Review AI activity and connection settings.");
    }
    private static LocalAnalysis Empty(ResolvedEntity entity)=>new([],entity.Owner,[],[],Enum.GetValues<AssessmentAxis>().ToHashSet());
    private LocalAnalysis EvaluateLocal(ResolvedEntity entity,KnowledgeEntry knowledge)=>KnownTaskCatalog.Supplement(entity,
        engine.Evaluate(entity,knowledge,time.GetUtcNow()),knowledge,time.GetUtcNow());
    private void ScheduleReview(ResolvedEntity entity,LocalAnalysis local,KnowledgeEntry knowledge)
    {
        var matched=knowledge.Rules.Where(r=>local.MatchingRuleIds.Contains(r.Id)).ToList();
        var references=local.Assessments.SelectMany(a=>a.EvidenceIds).Concat(matched.SelectMany(r=>r.EvidenceIds
            .Concat(r.Predicates.SelectMany(p=>p.EvidenceIds)))).ToHashSet();
        var deadlines=local.Assessments.Select(a=>a.ExpiresAt)
            .Concat(matched.SelectMany(r=>new[]{r.ReviewAt,r.ExpiresAt}))
            .Concat(knowledge.Evidence.Where(e=>e.ExpiresAt is not null && references.Contains(e.Id)).Select(e=>e.ExpiresAt!.Value))
            .Where(t=>t>time.GetUtcNow()).ToList();
        if(deadlines.Count>0) reviewTimes[entity.Identity]=deadlines.Min();
        else reviewTimes.TryRemove(entity.Identity,out _);
    }
    private void Publish(ResolvedEntity entity,AnalysisState state,LocalAnalysis result,string? error=null)
    { if(current.ContainsKey(entity.Identity)) updates.Writer.TryWrite(new(entity.Identity,state,result,error)); }
    public async Task WaitForIdleAsync(CancellationToken ct)
    { while(Volatile.Read(ref outstanding)>0) await Task.Delay(10,ct); }
    public Task RequestReanalysisAsync(AnalysisIdentity identity,CancellationToken ct)=>current.TryGetValue(identity,out var entity)
        ? EvaluateAsync(entity,policy,true,ct):Task.CompletedTask;
    public Task ChangePolicyAsync(AnalysisPolicy config,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock(policyLock)
        {
            if(policyEstablished && (config.Revision<policy.Revision || config.Revision==policy.Revision && config!=policy)) return Task.CompletedTask;
            if(policyEstablished && config==policy) return Task.CompletedTask;
            policyCancellation.Cancel(); policyCancellation.Dispose(); policyCancellation=new();
            var configured=config.Connections??ConnectionRouter.Resolve(config).ToArray();
            foreach(var (id,settings) in authPaused) if(!configured.Any(p=>p.ProfileId==id&&p.Provider==settings)) authPaused.TryRemove(id,out _);
            policy=config; policyEstablished=true; fingerprints.Clear(); reviewTimes.Clear();
        }
        return Task.CompletedTask;
    }
    public void InvalidateKnowledge() { fingerprints.Clear(); reviewTimes.Clear(); }
    public void RetryConnection(string profileId) { authPaused.TryRemove(profileId,out _); fingerprints.Clear(); }
    public async IAsyncEnumerable<AnalysisUpdate> Updates([EnumeratorCancellation] CancellationToken ct)
    { await foreach(var update in updates.Reader.ReadAllAsync(ct)) yield return update; }
    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel(); policyCancellation.Cancel(); queue.Writer.TryComplete();
        if(worker is not null) await worker;
        while(queue.Reader.TryRead(out _)) Interlocked.Decrement(ref outstanding);
        current.Clear(); fingerprints.Clear(); reviewTimes.Clear();
        updates.Writer.TryComplete(); lifetime.Dispose(); policyCancellation.Dispose(); Array.Clear(key);
    }
}

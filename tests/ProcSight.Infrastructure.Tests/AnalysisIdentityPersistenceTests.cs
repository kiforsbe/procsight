using System.Text.Json;
using ProcSight.Application.Analysis;
using ProcSight.Application.Presentation;
using ProcSight.Application.Providers;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Infrastructure.Providers;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class AnalysisIdentityPersistenceTests : IDisposable
{
    private readonly string folder=Path.Combine(Path.GetTempPath(),"procsight-identity-"+Guid.NewGuid());
    private SqliteKnowledgeStore Store()=>new(Path.Combine(folder,"knowledge.db"));
    private readonly byte[] contextKey=Enumerable.Repeat((byte)23,32).ToArray();
    private sealed class NoRuleProvider(bool unknown=false,bool rejectedRule=false,bool partial=false) : IAnalysisProvider
    {
        public int Calls;
        public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct)=>Task.FromResult(new ProviderCapabilities(true,false));
        public Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct)
        {
            Calls++;
            var id=payload.Evidence.First(e=>e.Facts.ContainsKey("hash")).Id;
            object[] rules=rejectedRule?[new { predicates=new[]{new { kind="Exact",field="name",value=payload.Metadata["name"],evidenceIds=new[]{id} }},assessmentIndexes=new[]{0,1,2} }]:[];
            var json=JsonSerializer.Serialize(new { identityLabel="Example indexing worker",role="Worker",rules,
                assessments=new[]{
                    new { axis="Purpose",value=unknown?"Unknown":"BackgroundHelper",reason="Synthetic binary metadata",confidence="Medium",evidenceIds=new[]{id} },
                    new { axis="Security",value=unknown||partial?"Unknown":"NoConcernObserved",reason="Synthetic evidence",confidence="Low",evidenceIds=partial?Array.Empty<string>():new[]{id} },
                    new { axis="Optionality",value=unknown||partial?"Unknown":"Optional",reason="Synthetic evidence",confidence="Medium",evidenceIds=partial?Array.Empty<string>():new[]{id} } } });
            return Task.FromResult(new ProviderResult(ProviderResponseValidator.Parse(json,payload,DateTimeOffset.UtcNow)));
        }
    }
    [Fact] public async Task MetadataAnalysisWithoutProposedRulesNamesTheBinaryAfterRestartAndChangedLaunchArguments()
    {
        var provider=new NoRuleProvider(); var store=Store();
        var policy=new AnalysisPolicy(true,CommandLineMode:CommandLineMode.Off,Provider:new("fixture",new("https://example.invalid/v1"),"fixture"));
        var first=Samples.Process(name:"worker.exe") with { CommandLine=new("worker.exe --session first",Samples.Now) };
        await using(var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider,contextKey:contextKey.ToArray())) {
            await coordinator.ObserveAsync(Samples.Snapshot(first),policy,default); await coordinator.WaitForIdleAsync(default);
            Assert.Equal(1,provider.Calls);
            var saved=Assert.Single(await store.GetRulesAsync(default));
            Assert.Equal(RuleState.Active,saved.State);
            Assert.Equal("Example indexing worker",saved.IdentityLabel);
            Assert.Contains(saved.Predicates,p=>p.Field=="hash");
            Assert.Contains(saved.Predicates,p=>p.Field=="path");
            Assert.DoesNotContain(saved.Predicates,p=>p.Field=="commandContext");
        }
        var second=first with { Key=new(99,Samples.Now.AddHours(1),null),CommandLine=new("worker.exe --session second",Samples.Now) };
        provider.Calls=0; var reopened=Store();
        await using(var coordinator=new AnalysisCoordinator(reopened,reopened,(_,_)=>provider,contextKey:contextKey.ToArray())) {
            var entity=Assert.Single(coordinator.ResolveEntities(Samples.Snapshot(second),policy));
            var result=new LocalAnalysisEngine().Evaluate(entity,await reopened.ReadCandidatesAsync(entity,default),DateTimeOffset.UtcNow);
            Assert.Equal("Example indexing worker",result.IdentityLabel);
            Assert.Equal("Example indexing worker",ProcessRoleLabels.DisplayName(entity,result.IdentityLabel));
            Assert.Single(result.AppliedRuleIds!);
            await coordinator.ObserveAsync(Samples.Snapshot(second),policy,default); await coordinator.WaitForIdleAsync(default);
            Assert.Equal(0,provider.Calls);
        }
    }
    [Fact] public async Task UnknownOnlyAnalysisDoesNotCreateAnIdentityRule()
    {
        var provider=new NoRuleProvider(true); var store=Store();
        await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider,contextKey:contextKey.ToArray());
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process(name:"worker.exe")),new(true,Provider:new("fixture",new("https://example.invalid/v1"),"fixture")),default);
        await coordinator.WaitForIdleAsync(default);
        Assert.Empty(await store.GetRulesAsync(default));
    }
    [Fact] public async Task PartiallyKnownAnalysisRestoresIdentityWhileUnknownAxesStayUnknown()
    {
        var store=Store(); var provider=new NoRuleProvider(partial:true);
        var policy=new AnalysisPolicy(true,Provider:new("fixture",new("https://example.invalid/v1"),"fixture"));
        var process=Samples.Process(name:"worker.exe") with { CommandLine=new("worker.exe --session first",Samples.Now) };
        await using(var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider,contextKey:contextKey.ToArray())) {
            await coordinator.ObserveAsync(Samples.Snapshot(process),policy,default); await coordinator.WaitForIdleAsync(default);
            Assert.Equal(RuleState.Active,Assert.Single(await store.GetRulesAsync(default)).State);
        }
        var reopened=Store();
        await using(var coordinator=new AnalysisCoordinator(reopened,reopened,(_,_)=>provider,contextKey:contextKey.ToArray())) {
            process=process with { Key=new(99,Samples.Now.AddHours(1),null),CommandLine=new("worker.exe --session second",Samples.Now) };
            var entity=Assert.Single(coordinator.ResolveEntities(Samples.Snapshot(process),policy));
            var result=new LocalAnalysisEngine().Evaluate(entity,await reopened.ReadCandidatesAsync(entity,default),DateTimeOffset.UtcNow);
            Assert.Equal("Example indexing worker",result.IdentityLabel);
            Assert.Contains(AssessmentAxis.Security,result.UnresolvedAxes);
            Assert.Contains(AssessmentAxis.Optionality,result.UnresolvedAxes);
            Assert.Single(result.Assessments);
        }
    }
    [Fact] public async Task RejectedProviderRuleIsNotReplacedWithAnAutomaticallyActiveRule()
    {
        var store=Store(); var provider=new NoRuleProvider(rejectedRule:true);
        await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider,contextKey:contextKey.ToArray());
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process(name:"worker.exe")),new(true,Provider:new("fixture",new("https://example.invalid/v1"),"fixture")),default);
        await coordinator.WaitForIdleAsync(default);
        Assert.Equal(RuleState.Rejected,Assert.Single(await store.GetRulesAsync(default)).State);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DifferentBinaryOrPathDoesNotReceiveTheSavedName(bool changeHash)
    {
        var store=Store(); var provider=new NoRuleProvider();
        var policy=new AnalysisPolicy(true,Provider:new("fixture",new("https://example.invalid/v1"),"fixture"));
        var process=Samples.Process(name:"worker.exe");
        await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider,contextKey:contextKey.ToArray());
        await coordinator.ObserveAsync(Samples.Snapshot(process),policy,default); await coordinator.WaitForIdleAsync(default);
        process=process with { Executable=changeHash?process.Executable with { ContentHash="different-binary" }:process.Executable with { FullPath=@"C:\Other\worker.exe" } };
        var entity=Assert.Single(coordinator.ResolveEntities(Samples.Snapshot(process),policy));
        var result=new LocalAnalysisEngine().Evaluate(entity,await Store().ReadCandidatesAsync(entity,default),DateTimeOffset.UtcNow);
        Assert.Empty(result.MatchingRuleIds); Assert.Null(result.IdentityLabel);
    }
    [Fact] public async Task LocalCommandAnalysisRetainsPrivateScopeAcrossRestartWithoutStoringArguments()
    {
        var store=Store(); var provider=new NoRuleProvider();
        var policy=new AnalysisPolicy(true,CommandLineMode:CommandLineMode.LocalOnly,LocalModel:new("local",new("http://127.0.0.1:1234/v1"),"fixture"));
        var process=Samples.Process(name:"worker.exe") with { CommandLine=new("worker.exe --token SYNTHETIC_PRIVATE",Samples.Now) };
        await using(var coordinator=new AnalysisCoordinator(store,store,(_,_)=>provider,contextKey:contextKey.ToArray())) {
            await coordinator.ObserveAsync(Samples.Snapshot(process),policy,default); await coordinator.WaitForIdleAsync(default);
            var rule=Assert.Single(await store.GetRulesAsync(default));
            Assert.True(rule.State==RuleState.Active,string.Join(",",rule.RejectionReasons??[]));
            Assert.Contains(rule.Predicates,p=>p.Field=="commandContext"&&p.Value.Length==64);
            Assert.DoesNotContain("SYNTHETIC_PRIVATE",JsonSerializer.Serialize(rule));
        }
        provider.Calls=0; var reopened=Store();
        await using(var coordinator=new AnalysisCoordinator(reopened,reopened,(_,_)=>provider,contextKey:contextKey.ToArray())) {
            var same=Assert.Single(coordinator.ResolveEntities(Samples.Snapshot(process with { Key=new(99,Samples.Now.AddHours(1),null) }),policy));
            var restored=await reopened.ReadCandidatesAsync(same,default);
            var reused=new LocalAnalysisEngine().Evaluate(same,restored,DateTimeOffset.UtcNow);
            Assert.True(reused.MatchingRuleIds.Count==1,"Matches="+reused.MatchingRuleIds.Count+"; fields="+string.Join(",",restored.Rules[0].Predicates.Where(p=>!same.Facts.TryGetValue(p.Field,out var value)||!PredicateMatcher.Matches(p,value)).Select(p=>p.Field))+"; origin="+same.Origins.Contains(restored.Rules[0].Origin));
            await coordinator.ObserveAsync(Samples.Snapshot(process with { Key=new(99,Samples.Now.AddHours(1),null) }),policy,default);
            await coordinator.WaitForIdleAsync(default); Assert.Equal(0,provider.Calls);
            var other=process with { CommandLine=new("worker.exe --token OTHER_PRIVATE",Samples.Now) };
            var entity=Assert.Single(coordinator.ResolveEntities(Samples.Snapshot(other),policy));
            var result=new LocalAnalysisEngine().Evaluate(entity,await reopened.ReadCandidatesAsync(entity,default),DateTimeOffset.UtcNow);
            Assert.Empty(result.MatchingRuleIds); Assert.Null(result.IdentityLabel);
        }
    }
    [Fact] public async Task LegacyTopLevelIdentityLabelSurvivesDeserializedAssessmentCollections()
    {
        var now=DateTimeOffset.UtcNow; var store=Store();
        var entity=new ProcSight.Core.Identity.IdentityResolver().Resolve(Samples.Snapshot(Samples.Process(name:"worker.exe")),[]).Single();
        var proof=entity.Evidence[0];
        var assessment=new Assessment(AssessmentAxis.Purpose,"BackgroundHelper","Synthetic metadata",Confidence.Medium,[proof.Id],now.AddDays(7),[DataOrigin.ShareableMetadata]);
        var rule=new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,now,now.AddDays(7),now.AddDays(7),
            [new(PredicateKind.Exact,"hash",entity.Facts["hash"],[proof.Id])],[assessment],[proof.Id],100);
        await store.SaveAsync(new(entity.Identity,now,[assessment],[proof],[rule],[],[rule.Id],"Example indexing worker","Worker"),default);
        var result=new LocalAnalysisEngine().Evaluate(entity,await Store().ReadCandidatesAsync(entity,default),now);
        Assert.Equal("Example indexing worker",result.IdentityLabel);
        Assert.Equal("Worker",result.Role);
        var loaded=await Store().ReadCandidatesAsync(entity,default);
        var corrected=loaded with { Corrections=[new(Guid.NewGuid(),CorrectionScope.Executable,entity.Facts["hash"],
            assessment with { Value="Application",Reason="Synthetic user correction" })] };
        Assert.Null(new LocalAnalysisEngine().Evaluate(entity,corrected,now).IdentityLabel);
        await store.SetRuleStateAsync(rule.Id,RuleState.Disabled,default);
        result=new LocalAnalysisEngine().Evaluate(entity,await Store().ReadCandidatesAsync(entity,default),now);
        Assert.Null(result.IdentityLabel);
    }
    public void Dispose()
    { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if(Directory.Exists(folder)) Directory.Delete(folder,true); }
}

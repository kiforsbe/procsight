using System.Text.Json;
using ProcSight.Application.Analysis;
using ProcSight.Application.Providers;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Infrastructure.Providers;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class RuleProvenanceTests : IDisposable
{
    private sealed class WireProvider(bool weakIdentity=false) : IAnalysisProvider
    {
        public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct)=>Task.FromResult(new ProviderCapabilities(true,false));
        public Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct)
        {
            var field=weakIdentity?"product":"hash"; var evidence=payload.Evidence.First(e=>e.Facts.ContainsKey(field));
            var json=JsonSerializer.Serialize(new { assessments=new[]{new { axis="Purpose",value="DeveloperTool",reason="Fixture",confidence="Medium",evidenceIds=new[]{evidence.Id} }},
                rules=new[]{new { predicates=new[]{new { kind="Exact",field,value=evidence.Facts[field],evidenceIds=new[]{evidence.Id} }},assessmentIndexes=new[]{0} }} });
            return Task.FromResult(new ProviderResult(ProviderResponseValidator.Parse(json,payload,DateTimeOffset.UtcNow)));
        }
    }
    [Fact] public async Task CommandRuleReceivesPrivateInvocationScopeBeforeFinalValidation()
    {
        var store=Store(); var policy=new AnalysisPolicy(true,CommandLineMode:CommandLineMode.OnlineFull,Provider:new("fixture",new("https://example.com"),"model"));
        await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>new WireProvider());
        var process=Samples.Process() with { CommandLine=new("node.exe worker.js",DateTimeOffset.UtcNow) }; var snapshot=Samples.Snapshot(process);
        var entity=Assert.Single(coordinator.ResolveEntities(snapshot,policy));
        await coordinator.ObserveAsync(snapshot,policy,default); await coordinator.WaitForIdleAsync(default);
        var rule=Assert.Single((await Store().LoadAsync(entity.Identity,default)).Rules);
        Assert.Equal(RuleState.Active,rule.State); Assert.Empty(rule.RejectionReasons!);
        Assert.Contains(rule.Predicates,p=>p.Field=="commandContext"&&p.Value.Length==64);
    }
    [Fact] public async Task OriginalIdentityRejectionReasonSurvivesLocalScopeEnrichment()
    {
        var store=Store(); var policy=new AnalysisPolicy(true,Provider:new("fixture",new("https://example.com"),"model"));
        await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>new WireProvider(true));
        var snapshot=Samples.Snapshot(Samples.Process(product:"Fixture product")); var entity=Assert.Single(coordinator.ResolveEntities(snapshot,policy));
        await coordinator.ObserveAsync(snapshot,policy,default); await coordinator.WaitForIdleAsync(default);
        var rule=Assert.Single((await Store().LoadAsync(entity.Identity,default)).Rules);
        Assert.Equal(RuleState.Rejected,rule.State); Assert.Contains(RuleRejectionReason.InsufficientIdentity,rule.RejectionReasons!);
    }
    private readonly string folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
    private SqliteKnowledgeStore Store()=>new(Path.Combine(folder,"knowledge.db"));
    private sealed class ProposalProvider(bool invalid=false) : IAnalysisProvider
    {
        public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct)=>Task.FromResult(new ProviderCapabilities(true,false));
        public Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct)
        {
            var now=DateTimeOffset.UtcNow; var evidence=payload.Evidence[0].Id;
            var assessment=new Assessment(AssessmentAxis.Purpose,"DeveloperTool","Fixture",Confidence.High,[evidence],now.AddDays(30),[DataOrigin.ShareableMetadata]);
            var rule=new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,now,now.AddDays(7),now.AddDays(30),
                [new(PredicateKind.Exact,invalid?"invalid":"hash","hash-a",[evidence])],[assessment],[evidence],100,
                Provenance:new("untrusted-provider","untrusted-model",AnalysisDestination.LocalRules,DateTimeOffset.MinValue),
                RejectionReasons:[RuleRejectionReason.UnsupportedThreat]);
            return Task.FromResult(new ProviderResult(new([assessment],[rule])));
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task SelectedProviderProvenanceOverridesProposalAndSurvivesRestart(bool local)
    {
        var store=Store(); var before=DateTimeOffset.UtcNow;
        var selected=new ProviderSettings("trusted-fixture",new("https://example.com"),"trusted-model",AccountId:"PRIVATE_ACCOUNT");
        var policy=new AnalysisPolicy(true,Provider:local?null:selected,LocalModel:local?selected:null);
        await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>new ProposalProvider());
        var snapshot=Samples.Snapshot(Samples.Process());
        var entity=Assert.Single(coordinator.ResolveEntities(snapshot,policy));
        await coordinator.ObserveAsync(snapshot,policy,default); await coordinator.WaitForIdleAsync(default);
        var stored=await Store().LoadAsync(entity.Identity,default); var rule=Assert.Single(stored.Rules);
        Assert.Equal(RuleState.Active,rule.State);
        Assert.NotNull(rule.Provenance); Assert.Equal(selected.Id,rule.Provenance.ProviderId); Assert.Equal(selected.Model,rule.Provenance.Model);
        Assert.Equal(local?AnalysisDestination.LocalModel:AnalysisDestination.CloudModel,rule.Provenance.Destination);
        Assert.InRange(rule.Provenance.GeneratedAt,before,DateTimeOffset.UtcNow);
        Assert.DoesNotContain("PRIVATE_ACCOUNT",JsonSerializer.Serialize(stored));
        Assert.DoesNotContain("untrusted-",JsonSerializer.Serialize(stored));
        Assert.Empty(rule.RejectionReasons!); // Model-supplied decision diagnostics are never trusted.
        Assert.Equal(ResearchOutcome.Disabled,rule.Provenance.Research);
    }
    [Fact] public async Task RejectionDecisionAndResearchSkipSurviveRestartWithoutInventingHistoricalDetails()
    {
        var store=Store(); var selected=new ProviderSettings("chatgpt",new("https://api.openai.com/v1"),"fixture-model");
        var policy=new AnalysisPolicy(true,true,Connections:[new("primary",selected,true,0)],ResearchProfileId:"missing");
        await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>new ProposalProvider(true));
        var snapshot=Samples.Snapshot(Samples.Process()); var entity=Assert.Single(coordinator.ResolveEntities(snapshot,policy));
        await coordinator.ObserveAsync(snapshot,policy,default); await coordinator.WaitForIdleAsync(default);
        var rule=Assert.Single((await Store().LoadAsync(entity.Identity,default)).Rules);
        Assert.Equal(RuleState.Rejected,rule.State);
        Assert.Contains(RuleRejectionReason.InvalidPredicate,rule.RejectionReasons!);
        Assert.DoesNotContain(RuleRejectionReason.UnsupportedThreat,rule.RejectionReasons!);
        Assert.Equal(ResearchOutcome.Unavailable,rule.Provenance!.Research);
    }
    [Fact] public async Task ProvenanceSanitizesSecretsAndBoundsConfiguredNames()
    {
        var store=Store();
        var policy=new AnalysisPolicy(true,Provider:new("token=PRIVATE_TOKEN "+new string('a',200),new("https://example.com"),
            @"C:\Users\PrivatePerson\model secret=PRIVATE_SECRET "+new string('m',400)));
        await using var coordinator=new AnalysisCoordinator(store,store,(_,_)=>new ProposalProvider());
        var snapshot=Samples.Snapshot(Samples.Process()); var entity=Assert.Single(coordinator.ResolveEntities(snapshot,policy));
        await coordinator.ObserveAsync(snapshot,policy,default); await coordinator.WaitForIdleAsync(default);
        var provenance=Assert.Single((await Store().LoadAsync(entity.Identity,default)).Rules).Provenance!;
        Assert.Contains("<RedactedSecret>",provenance.ProviderId); Assert.Contains("<RedactedSecret>",provenance.Model);
        Assert.DoesNotContain("PRIVATE_TOKEN",provenance.ProviderId); Assert.DoesNotContain("PRIVATE_SECRET",provenance.Model);
        Assert.DoesNotContain("PrivatePerson",provenance.Model); Assert.True(provenance.ProviderId.Length<=128); Assert.True(provenance.Model.Length<=256);
    }
    public void Dispose()
    { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if(Directory.Exists(folder)) Directory.Delete(folder,true); }
}

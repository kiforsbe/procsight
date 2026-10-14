using System.Text.Json;
using ProcSight.Application.Analysis;
using ProcSight.Application.Providers;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class RuleProvenanceTests : IDisposable
{
    private readonly string folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
    private SqliteKnowledgeStore Store()=>new(Path.Combine(folder,"knowledge.db"));
    private sealed class ProposalProvider : IAnalysisProvider
    {
        public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct)=>Task.FromResult(new ProviderCapabilities(true,false));
        public Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct)
        {
            var now=DateTimeOffset.UtcNow; var evidence=payload.Evidence[0].Id;
            var assessment=new Assessment(AssessmentAxis.Purpose,"DeveloperTool","Fixture",Confidence.High,[evidence],now.AddDays(30),[DataOrigin.ShareableMetadata]);
            var rule=new RuleDefinition(Guid.NewGuid(),1,RuleState.Active,DataOrigin.ShareableMetadata,now,now.AddDays(7),now.AddDays(30),
                [new(PredicateKind.Exact,"hash","hash-a",[evidence])],[assessment],[evidence],100,
                Provenance:new("untrusted-provider","untrusted-model",AnalysisDestination.LocalRules,DateTimeOffset.MinValue));
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

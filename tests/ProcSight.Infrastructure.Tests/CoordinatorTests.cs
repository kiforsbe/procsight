using ProcSight.Application.Analysis;
using ProcSight.Application.Providers;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Infrastructure.Tests;
public sealed class CountingProvider : IAnalysisProvider
{
    public int Calls; public TaskCompletionSource? Hold;
    public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ProviderErrorKind Kind=ProviderErrorKind.Malformed;
    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct)=>Task.FromResult(new ProviderCapabilities(true,false));
    public async Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct)
    {
        Calls++; Started.TrySetResult(); if(Hold is not null) await Hold.Task.WaitAsync(ct);
        return new(null,new(Kind,"Fixture unknown",TimeSpan.Zero));
    }
}
public sealed class CoordinatorTests : IDisposable
{
    [Fact] public async Task NameOnlyProtectedSnapshotsDoNotSpendAutomaticJobsRepeatedly()
    {
        await using var coordinator=Coordinator();
        for(var i=0;i<3;i++) {
            var p=Samples.Process() with { Key=new(20,null,Guid.NewGuid()),Executable=new("unknown.exe") };
            await coordinator.ObserveAsync(Samples.Snapshot(p),Policy(),default);
            await coordinator.WaitForIdleAsync(default);
        }
        Assert.Equal(0,provider.Calls);
    }
    [Fact] public async Task BundledRulesExplainKnownTasksWithoutCloudPermission()
    {
        await using var coordinator=Coordinator();
        var p=Samples.Process() with { Executable=new("svchost.exe",@"C:\Windows\System32\svchost.exe",
            Product:"Microsoft Windows",Signature:SignatureStatus.Verified,VerifiedSigner:"Microsoft Windows",OriginalName:"svchost.exe") };
        await coordinator.ObserveAsync(Samples.Snapshot(p),new(),default);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var updates=coordinator.Updates(timeout.Token).GetAsyncEnumerator();
        Assert.True(await updates.MoveNextAsync());
        Assert.Equal("Windows service host",updates.Current.Result.IdentityLabel);
        Assert.Contains(AssessmentAxis.Security,updates.Current.Result.UnresolvedAxes);
        Assert.Equal(0,provider.Calls);
    }
    private readonly string folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
    private readonly CountingProvider provider=new();
    private AnalysisCoordinator Coordinator()
    {
        var store=new SqliteKnowledgeStore(Path.Combine(folder,"knowledge.db"));
        return new(store,store,(_,_)=>provider);
    }
    private static AnalysisPolicy Policy()=>new(true,Provider:new("fixture",new("https://example.com/v1"),"fixture"));
    [Fact] public async Task EquivalentInstancesShareOneJob()
    {
        await using var coordinator=Coordinator();
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process(1),Samples.Process(2)),Policy(),default);
        await coordinator.WaitForIdleAsync(default); Assert.Equal(1,provider.Calls);
    }
    [Fact] public async Task UnknownUnchangedDoesNotLoop()
    {
        await using var coordinator=Coordinator(); var snapshot=Samples.Snapshot(Samples.Process());
        await coordinator.ObserveAsync(snapshot,Policy(),default); await coordinator.WaitForIdleAsync(default);
        await coordinator.ObserveAsync(snapshot,Policy(),default); await coordinator.WaitForIdleAsync(default);
        Assert.Equal(1,provider.Calls);
    }
    [Fact] public async Task BudgetLimitsPauseUnresolvedCases()
    {
        await using var coordinator=Coordinator();
        var policy=Policy() with { Limits=new(AutomaticJobs:1,BillableCalls:1) };
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process(1,hash:"a"),Samples.Process(2,hash:"b")),policy,default);
        await coordinator.WaitForIdleAsync(default); Assert.Equal(1,provider.Calls);
    }
    [Fact] public async Task ShutdownCancelsQueuedWork()
    {
        provider.Hold=new(); var coordinator=Coordinator();
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process()),Policy(),default);
        await coordinator.DisposeAsync(); Assert.True(provider.Calls<=1);
    }
    [Fact] public async Task OlderObservationCannotRestoreOnlinePermission()
    {
        await using var coordinator=Coordinator();
        await coordinator.ChangePolicyAsync(new(Revision:2),default);
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process()),Policy() with { Revision=1 },default);
        await coordinator.WaitForIdleAsync(default);
        Assert.Equal(0,provider.Calls);
    }
    [Fact] public async Task TransientRetriesStayWithinBillableCap()
    {
        provider.Kind=ProviderErrorKind.Transient; await using var coordinator=Coordinator();
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process()),Policy() with { Limits=new(BillableCalls:2) },default);
        await coordinator.WaitForIdleAsync(default); Assert.Equal(2,provider.Calls);
    }
    [Fact] public async Task AuthenticationFailurePausesFollowingJobs()
    {
        provider.Kind=ProviderErrorKind.Auth; await using var coordinator=Coordinator();
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process(1,hash:"a"),Samples.Process(2,hash:"b")),Policy(),default);
        await coordinator.WaitForIdleAsync(default); Assert.Equal(1,provider.Calls);
    }
    [Fact] public async Task PolicyChangeCancelsWorkBeforeNextQueuedRequest()
    {
        provider.Hold=new(); await using var coordinator=Coordinator();
        await coordinator.ObserveAsync(Samples.Snapshot(Samples.Process(1,hash:"a"),Samples.Process(2,hash:"b")),Policy() with { Revision=1 },default);
        await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.ChangePolicyAsync(new(Revision:2),default);
        await coordinator.WaitForIdleAsync(default); Assert.Equal(1,provider.Calls);
    }
    [Fact] public async Task LocalOnlyCloudProjectionDoesNotExposePrivateContextIdentity()
    {
        await using var coordinator=Coordinator();
        var snapshot=Samples.Snapshot(Samples.Process() with { CommandLine=new("node.exe --worker --token=SYNTHETIC_SECRET",Samples.Now) });
        var shareable=Assert.Single(coordinator.ResolveEntities(snapshot,new()));
        var privateEntity=Assert.Single(coordinator.ResolveEntities(snapshot,new(CommandLineMode:CommandLineMode.LocalOnly)));
        var payload=new ProcSight.Core.Privacy.AnalysisProjection().Build(privateEntity,new(CommandLineMode:CommandLineMode.LocalOnly),AnalysisDestination.CloudModel);
        Assert.Equal(shareable.Identity,payload.Identity);
        Assert.DoesNotContain(payload.Evidence,e=>e.Origin==DataOrigin.LocalCommandLine);
        Assert.DoesNotContain("SYNTHETIC_SECRET",System.Text.Json.JsonSerializer.Serialize(payload));
    }
    [Fact] public async Task OffDiscardsPreviouslyCollectedCommandLine()
    {
        var coordinator=Coordinator();
        var process=Samples.Process() with { CommandLine=new("node.exe --private-argument",Samples.Now) };
        var entity=Assert.Single(coordinator.ResolveEntities(Samples.Snapshot(process),new()));
        Assert.Null(entity.Process!.CommandLine);
        await coordinator.WaitForIdleAsync(default);
    }
    public void Dispose()
    { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if(Directory.Exists(folder)) Directory.Delete(folder,true); }
}

using ProcSight.Application.Settings;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class ImmediateSettingsTests
{
    [Fact] public async Task PendingIndependentEditsMergeThroughDesiredSnapshot()
    {
        var store=new BlockingStore(); var live=new AnalysisPolicy();
        var controller=new ImmediateSettingsController(store,(p,_)=> { live=p; return Task.CompletedTask; },live,new());
        var first=controller.ApplyAsync(controller.Desired with { AutomaticEnabled=true },new(),default);
        await store.Started.Task;
        var second=controller.ApplyAsync(controller.Desired with { ShareIdentifyingMetadata=true },new(),default);
        store.Release.SetResult(); await Task.WhenAll(first,second);
        Assert.True(live.AutomaticEnabled); Assert.True(live.ShareIdentifyingMetadata);
    }
    [Fact] public async Task SessionReapplyCannotRestoreOlderRevisionAndLimitReductionsSurviveFailure()
    {
        var store=new BlockingStore(); var live=new AnalysisPolicy();
        var controller=new ImmediateSettingsController(store,(p,_)=> { live=p; return Task.CompletedTask; },live,new());
        var first=controller.ApplyAsync(live with { AutomaticEnabled=true },new(),default);
        await store.Started.Task; await controller.ReapplyAsync(default); var revision=live.Revision;
        store.Release.SetResult(); await first; Assert.True(live.Revision>revision);
        var fail=new Store { Fail=true }; controller=new(fail,(p,_)=> { live=p; return Task.CompletedTask; },live,new());
        await controller.ApplyAsync(live with { Limits=new(0,0) },new(),default);
        Assert.Equal(0,live.Limits!.BillableCalls);
    }
    private sealed class BlockingStore : IPreferencesStore
    {
        public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public (AnalysisPolicy,PresentationPreferences) Load()=>(new(),new());
        public void Save(AnalysisPolicy p,PresentationPreferences v) { Started.TrySetResult(); Release.Task.GetAwaiter().GetResult(); }
    }
    [Fact] public async Task InvalidProfileCannotReplaceLastValidConfiguration()
    {
        var store=new Store(); var live=new AnalysisPolicy();
        var controller=new ImmediateSettingsController(store,(p,_)=> { live=p; return Task.CompletedTask; },live,new());
        var result=await controller.ApplyAsync(live with { Connections=[new("one",new("openai",new("https://api.openai.com/v1"),""),true,0)] },new(),default);
        Assert.Equal(SettingsFailure.Invalid,result.Failure); Assert.Null(live.Connections); Assert.Equal(0,store.Saves);
    }
    private sealed class Store : IPreferencesStore
    {
        public bool Fail; public int Saves; public AnalysisPolicy? Saved;
        public (AnalysisPolicy,PresentationPreferences) Load()=>(Saved??new(),new());
        public void Save(AnalysisPolicy p,PresentationPreferences v) { Saves++; if(Fail) throw new IOException(); Saved=p; }
    }
    [Fact] public async Task RestrictiveChangeCancelsBeforeFailedPersistenceAndRemainsEffective()
    {
        var store=new Store { Fail=true }; var live=new AnalysisPolicy(true,ShareIdentifyingMetadata:true);
        var controller=new ImmediateSettingsController(store,(p,_)=> { live=p; return Task.CompletedTask; },live,new());
        var result=await controller.ApplyAsync(live with { AutomaticEnabled=false,ShareIdentifyingMetadata=false },new(),default);
        Assert.False(live.AutomaticEnabled); Assert.False(live.ShareIdentifyingMetadata); Assert.False(result.Persisted);
    }
    [Fact] public async Task FailedEnablingNeverBecomesEffectiveAndUnchangedDoesNotSave()
    {
        var store=new Store { Fail=true }; var live=new AnalysisPolicy();
        var controller=new ImmediateSettingsController(store,(p,_)=> { live=p; return Task.CompletedTask; },live,new());
        Assert.True((await controller.ApplyAsync(live,new(),default)).Persisted); Assert.Equal(0,store.Saves);
        await controller.ApplyAsync(live with { AutomaticEnabled=true },new(),default);
        Assert.False(live.AutomaticEnabled);
    }
    [Fact] public async Task RapidUpdatesCannotRestoreOlderPolicy()
    {
        var store=new Store(); var live=new AnalysisPolicy();
        var controller=new ImmediateSettingsController(store,(p,_)=> { live=p; return Task.CompletedTask; },live,new());
        var a=controller.ApplyAsync(live with { AutomaticEnabled=true },new(),default);
        var b=controller.ApplyAsync(live with { AutomaticEnabled=false },new(),default);
        await Task.WhenAll(a,b); Assert.False(live.AutomaticEnabled); Assert.False(store.Saved!.AutomaticEnabled);
    }
}

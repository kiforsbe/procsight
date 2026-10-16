using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Application.Analysis;
using ProcSight.Application.Providers;
using ProcSight.Application.Research;
using ProcSight.Core.Models;
using ProcSight.Core.Privacy;
using ProcSight.Core.Rules;
using ProcSight.Infrastructure.Knowledge;
using ProcSight.Infrastructure.Providers;
using ProcSight.Infrastructure.Research;
using ProcSight.Windows.Storage;
using ProcSight.Application.Presentation;
using System.Security.Cryptography;
using System.Text.Json;
using ProcSight.Infrastructure.Authentication;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private readonly string dataFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ProcSight");
    private AnalysisPolicy savedPolicy=new();
    private AnalysisPolicy policy { get=>settingsController?.Desired??savedPolicy; set=>savedPolicy=value; }
    private PresentationPreferences presentation=new();
    private PreferencesStore preferences=null!;
    private readonly CredentialStore credentials=new();
    private ChatGptSessionService? chatGpt;
    private ChatGptSessionService ChatGpt=>chatGpt ??=new(credentials);
    private SqliteKnowledgeStore knowledge=null!,usage=null!;
    private AnalysisCoordinator coordinator=null!;
    private readonly Dictionary<AnalysisIdentity,AnalysisUpdate> analysis=[];
    private readonly AnalysisActivityLog aiActivity=new();
    private string? runtimeWarning;
    private readonly HttpClient providerHttp=ProviderHttp.Create();
    private void LoadRuntime()
    {
        preferences=new(Path.Combine(dataFolder,"settings.json"));
        try {
            (policy,presentation)=preferences.Load();
            var migrated=ProcSight.Application.Settings.ConnectionSettingsMigration.Migrate(policy,credentials);
            if(migrated!=policy) { preferences.Save(migrated,presentation); policy=migrated; }
        }
        catch { runtimeWarning="Settings could not be loaded. Online analysis is disabled until valid settings are configured."; policy=new(); }
        knowledge=new(Path.Combine(dataFolder,"knowledge.db"));
        if(Enum.TryParse<ProcessSort>(presentation.Sort,out var savedSort)) sort=savedSort;
        sortDescending=presentation.SortDescending;
        usage=new(Path.Combine(dataFolder,"usage.db"));
        byte[] key;
        try
        {
            var saved=credentials.Get("context-key");
            key=saved is null?RandomNumberGenerator.GetBytes(32):Convert.FromBase64String(saved);
            if(saved is null) credentials.Set("context-key",Convert.ToBase64String(key));
        }
        catch { key=RandomNumberGenerator.GetBytes(32); runtimeWarning="Credential storage unavailable; local context keys will not survive restart."; }
        coordinator=new(knowledge,usage,CreateProvider,CreateResearch,contextKey:key,activity:aiActivity);
    }
    private IAnalysisProvider CreateProvider(ProviderSettings settings,bool local) => settings.Id=="chatgpt"&&!local
        ? new ChatGptPlanProvider(settings,ct=>settings.AccountId is { } id?ChatGpt.GetAccessTokenAsync(id,ct):Task.FromResult<string?>(null),providerHttp)
        : settings.Id=="openai"&&!local
        ? new OpenAiResponsesProvider(settings,()=>ProcSight.Application.Settings.ConnectionCredentials.Read(settings,policy.Connections is not null,credentials),providerHttp) :
        new CompatibleChatProvider(settings,()=>ProcSight.Application.Settings.ConnectionCredentials.Read(settings,policy.Connections is not null,credentials),providerHttp,local:local);
    private IResearchProvider? CreateResearch(ProviderSettings settings)=>settings.Id=="openai"?
        new OpenAiWebResearchProvider(settings,()=>ProcSight.Application.Settings.ConnectionCredentials.Read(settings,policy.Connections is not null,credentials),providerHttp):null;
    private async Task ReadAnalysisAsync()
    {
        try
        {
            await foreach(var update in coordinator.Updates(lifetime.Token))
                DispatcherQueue.TryEnqueue(()=>
                {
                    analysis[update.Identity]=update;
                    foreach(var row in rows.Where(r=>r.Entity.Identity==update.Identity)) row.SetAnalysis(update);
                    if(table?.SelectedItem is ViewModels.ProcessRow selected && selected.Entity.Identity==update.Identity) ShowDetail(selected);
                });
        }
        catch(OperationCanceledException) { }
    }
    private async Task PreviewAsync()
    {
        var entity=(table?.SelectedItem as ViewModels.ProcessRow)?.Entity ?? entities.FirstOrDefault();
        if(entity is null) { await MessageAsync("No metadata available","Wait for collection or select an item first."); return; }
        var destination=ConnectionRouter.Resolve(EffectivePolicy).FirstOrDefault()?.IsLocal==true?
            AnalysisDestination.LocalModel:AnalysisDestination.CloudModel;
        var payload=new AnalysisProjection().Build(entity,EffectivePolicy,destination);
        await MessageAsync("Analysis payload preview",JsonSerializer.Serialize(payload,new JsonSerializerOptions { WriteIndented=true })+
            (policy.CommandLineMode!=CommandLineMode.Off&&payload.CommandLine is null?"\n\nFull command line is unavailable or not permitted for this destination.":""));
    }
    private async Task MessageAsync(string title,string message)
    {
        var text=new TextBlock { Text=message,TextWrapping=TextWrapping.Wrap,IsTextSelectionEnabled=true };
        await new ContentDialog { Title=title,Content=new ScrollViewer { Content=text,MaxHeight=500 },CloseButtonText="Close",XamlRoot=PageContent.XamlRoot }.ShowAsync();
    }
    private async Task CorrectSelectedAsync()
    {
        if(table?.SelectedItem is not ViewModels.ProcessRow row) return;
        var axis=new ComboBox { Header="Assessment",ItemsSource=Enum.GetValues<AssessmentAxis>(),SelectedIndex=0 };
        var value=new ComboBox { Header="Classification",ItemsSource=RuleValidator.Values[AssessmentAxis.Purpose].ToArray(),SelectedIndex=0 };
        axis.SelectionChanged+=(_,_)=> { value.ItemsSource=RuleValidator.Values[(AssessmentAxis)axis.SelectedItem].ToArray(); value.SelectedIndex=0; };
        var scope=new ComboBox { Header="Apply to",ItemsSource=Enum.GetValues<CorrectionScope>(),SelectedItem=CorrectionScope.InvocationContext };
        var reason=new TextBox { Header="Why",PlaceholderText="Your assessment; do not enter secrets or full command lines." };
        var panel=new StackPanel { Spacing=8 }; panel.Children.Add(axis); panel.Children.Add(value); panel.Children.Add(scope); panel.Children.Add(reason);
        var dialog=new ContentDialog { Title="Correct classification",Content=panel,PrimaryButtonText="Save correction",CloseButtonText="Cancel",XamlRoot=PageContent.XamlRoot };
        if(await dialog.ShowAsync()!=ContentDialogResult.Primary) return;
        var selectedScope=(CorrectionScope)scope.SelectedItem;
        var scopeValue=selectedScope switch { CorrectionScope.Executable=>row.Entity.Facts.GetValueOrDefault("hash"),
            CorrectionScope.ApplicationFamily=>row.Entity.Owner,_=>row.Entity.Identity.Value };
        if(string.IsNullOrWhiteSpace(scopeValue)) { await MessageAsync("Scope unavailable","This scope needs verified executable or application identity. Use invocation context instead."); return; }
        try
        {
            var entry=await knowledge.LoadAsync(row.Entity.Identity,lifetime.Token);
            var selectedAxis=(AssessmentAxis)axis.SelectedItem;
            entry.Corrections.Add(new(Guid.NewGuid(),selectedScope,scopeValue,new(selectedAxis,(string)value.SelectedItem,
                MetadataSanitizer.Normalize(reason.Text),Confidence.High,[],DateTimeOffset.UtcNow.AddDays(selectedAxis==AssessmentAxis.Purpose?30:7),[DataOrigin.UserCorrection])));
            await knowledge.SaveAsync(entry with { UpdatedAt=DateTimeOffset.UtcNow },lifetime.Token);
            coordinator.InvalidateKnowledge();
        }
        catch { await MessageAsync("Correction not saved","Knowledge storage is unavailable. Local collection continues."); }
    }
}

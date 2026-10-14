using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Providers;
using ProcSight.Application.Analysis;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private void RenderSettings()
    {
        var panel=new StackPanel { Spacing=8,MaxWidth=1000,HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(24,16,24,24) };
        panel.Children.Add(new TextBlock { Text="Settings",FontSize=24,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Margin=new Thickness(0,0,0,12) });
        SettingsSection(panel,"Appearance");
        var theme=new ComboBox { ItemsSource=new[]{"System","Light","Dark"},SelectedItem=presentation.Theme,MinWidth=170 };
        theme.SelectionChanged+=(_,_)=> { presentation=presentation with { Theme=(string)theme.SelectedItem }; ConfigureTheme(); SavePresentation(); };
        panel.Children.Add(SettingsCard("\uE790","App theme","Choose which theme the app displays",theme));
        SettingsSection(panel,"AI connections");
        if(runtimeWarning is not null) panel.Children.Add(new InfoBar { IsOpen=true,Severity=InfoBarSeverity.Warning,Message=runtimeWarning,IsClosable=false });
        var automatic=new CheckBox { Content="Enable automatic AI analysis of sanitized process metadata",IsChecked=policy.AutomaticEnabled };
        var researchOption=new CheckBox { Content="Enable online research (OpenAI profile only; separate identity-only requests)",IsChecked=policy.ResearchEnabled };
        var mode=new ComboBox { Header="Command-line sharing",ItemsSource=new[]{"Exclude from online analysis","Prefer local model; exclude online","Include in online analysis"},SelectedIndex=(int)policy.CommandLineMode };
        var identifying=new CheckBox { Content="Include file paths and ownership, package, and service identifiers online",IsChecked=policy.ShareIdentifyingMetadata };
        var disclosure=new TextBlock { Text="Command lines are always available locally when Windows permits collection. Online inclusion sends the verbatim line, which can contain credentials and personal paths, regardless of the metadata checkbox. Excluded lines and their derived findings stay local. Research never receives full command lines.",TextWrapping=TextWrapping.Wrap };
        var type=new ComboBox { Header="Online provider",ItemsSource=new[]{"OpenAI API key","Compatible API","ChatGPT plan"},SelectedIndex=policy.Provider?.Id=="chatgpt"?2:policy.Provider?.Id=="compatible"?1:0 };
        var endpoint=new TextBox { Header="Online API base URL",Text=policy.Provider?.Endpoint.ToString() ?? "https://api.openai.com/v1" };
        var model=new TextBox { Header="Online model",Text=policy.Provider?.Model ?? "",PlaceholderText="Enter a model available to your API account" };
        var apiKey=new PasswordBox { Header="API key",PlaceholderText="Enter to save or replace the Windows-protected credential" };
        var strict=new CheckBox { Content="Compatible provider supports schema-constrained JSON",IsChecked=policy.Provider?.StructuredOutput ?? true };
        var localEndpoint=new TextBox { Header="Optional local model base URL",Text=policy.LocalModel?.Endpoint.ToString() ?? "",PlaceholderText="http://127.0.0.1:1234/v1" };
        var localModel=new TextBox { Header="Local model",Text=policy.LocalModel?.Model ?? "" };
        var localStrict=new CheckBox { Content="Local model supports schema-constrained JSON",IsChecked=policy.LocalModel?.StructuredOutput ?? false };
        var jobs=new NumberBox { Header="Automatic jobs per day",Value=policy.Limits?.AutomaticJobs ?? 20,Minimum=0,Maximum=10000,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
        var calls=new NumberBox { Header="Billable calls per day (includes retries and reserved research calls)",Value=policy.Limits?.BillableCalls ?? 60,Minimum=0,Maximum=10000,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
        var feedback=new TextBlock { TextWrapping=TextWrapping.Wrap };
        panel.Children.Add(BuildChatGptSettings(type,model,()=>automatic.IsChecked=false));
        panel.Children.Add(SettingsExpander("\uE753","Online provider","Choose ChatGPT plan usage or separately billed API access",type,endpoint,model,apiKey,strict));
        void UpdateProviderFields() { var plan=type.SelectedIndex==2; endpoint.IsEnabled=!plan; apiKey.IsEnabled=!plan; model.IsReadOnly=plan; strict.IsEnabled=!plan; researchOption.IsEnabled=!plan; if(plan) { endpoint.Text="https://api.openai.com/v1"; researchOption.IsChecked=false; } }
        type.SelectionChanged+=(_,_)=>UpdateProviderFields(); UpdateProviderFields();
        panel.Children.Add(SettingsExpander("\uE8B7","Local model","Optional model running on this computer",localEndpoint,localModel,localStrict));
        SettingsSection(panel,"Analysis and privacy");
        panel.Children.Add(SettingsExpander("\uE9CE","Automatic analysis","Local rules and saved knowledge are always checked first",automatic,researchOption));
        panel.Children.Add(SettingsExpander("\uE72E","Online sharing","Choose which identifying metadata may leave this computer",mode,identifying,disclosure));
        panel.Children.Add(SettingsExpander("\uE9D9","Request limits","Bound automatic work and online requests, including retries",jobs,calls));
        var save=new Button { Content="Save settings" };
        save.Click+=async(_,_)=>
        {
            try
            {
                ProviderSettings? online=null,local=null;
                if(type.SelectedIndex==2&&string.IsNullOrWhiteSpace(model.Text)) throw new ArgumentException("Choose an available model under ChatGPT account before saving.");
                if(!string.IsNullOrWhiteSpace(model.Text))
                {
                    var account=type.SelectedIndex==2?settingsChatGptAccountId:null;
                    if(type.SelectedIndex==2&&account is null) throw new ArgumentException("Connect and select a ChatGPT account first.");
                    if(type.SelectedIndex==2&&settingsChatGptModel!=model.Text.Trim()) throw new ArgumentException("Choose a model returned for the selected ChatGPT account.");
                    online=new(type.SelectedIndex==2?"chatgpt":type.SelectedIndex==0?"openai":"compatible",new Uri(endpoint.Text.Trim()),model.Text.Trim(),strict.IsChecked==true,account);
                    _=ProviderHttp.Route(online,"responses");
                }
                if(!string.IsNullOrWhiteSpace(localEndpoint.Text)||!string.IsNullOrWhiteSpace(localModel.Text))
                {
                    local=new("local",new Uri(localEndpoint.Text.Trim()),localModel.Text.Trim(),localStrict.IsChecked==true);
                    LocalEndpointPolicy.EnsureLocal(local.Endpoint);
                    if(local.Model.Length==0) throw new ArgumentException("Enter a local model name.");
                }
                if(automatic.IsChecked==true && online is null&&local is null) throw new ArgumentException("Configure a model before enabling automatic analysis.");
                if(!double.IsFinite(jobs.Value)||!double.IsFinite(calls.Value)) throw new ArgumentException("Enter valid request limits.");
                if(online is not null&&online.Id!="chatgpt"&&apiKey.Password.Length>0) credentials.Set(online.Id,apiKey.Password);
                var next=new AnalysisPolicy(automatic.IsChecked==true,researchOption.IsChecked==true,(CommandLineMode)mode.SelectedIndex,
                    online,local,new((int)jobs.Value,(int)calls.Value),policy.Revision+1,identifying.IsChecked==true);
                preferences.Save(next,presentation); policy=next;
                await coordinator.ChangePolicyAsync(policy,lifetime.Token);
                apiKey.Password=""; runtimeWarning=null; feedback.Text="Saved. New queued requests follow this policy; requests already sent cannot be recalled.";
            }
            catch(Exception e) when(e is ArgumentException or UriFormatException) { feedback.Text=e.Message; }
            catch { feedback.Text="Settings could not be saved. Check local storage and credential access."; }
        };
        var test=new Button { Content="Test saved provider with synthetic metadata" };
        test.Click+=async(_,_)=>
        {
            var selected=policy.Provider ?? policy.LocalModel;
            if(selected is null) { feedback.Text="Save a provider and model first."; return; }
            test.IsEnabled=false;
            Guid? activityId=null;
            try
            {
                var local=policy.Provider is null;
                if(!local&&!await usage.TryReserveAsync(UsageKind.BillableCall,DateOnly.FromDateTime(DateTime.Now),policy.Limits ?? new(),lifetime.Token))
                { feedback.Text="Daily call budget reached."; return; }
                var payload=new AnalysisPayload(new("connection-test"),new(){["name"]="synthetic-connection-test.exe"},[],null,
                    policy.Revision,local?AnalysisDestination.LocalModel:AnalysisDestination.CloudModel);
                activityId=aiActivity.Begin(payload,selected,"Synthetic connection test",kind:AnalysisActivityKind.ConnectionTest);
                var result=await CreateProvider(selected,local).AnalyzeAsync(payload,lifetime.Token);
                aiActivity.Complete(activityId,result,payload);
                feedback.Text=result.Error?.Message ?? "Provider returned a validated response. Research support requires a research-capable model.";
            }
            catch(OperationCanceledException) { aiActivity.Fail(activityId,cancelled:true); feedback.Text="Connection test cancelled."; }
            catch { aiActivity.Fail(activityId); feedback.Text="Connection test failed; no real process metadata was sent."; }
            finally { test.IsEnabled=true; }
        };
        var preview=new Button { Content="Preview current analysis payload…" }; preview.Click+=async(_,_)=>await PreviewAsync();
        var activityButton=new Button { Content="View AI activity" }; activityButton.Click+=(_,_)=> {
            Navigation.SelectedItem=Navigation.MenuItems.OfType<NavigationViewItem>().First(i=>i.Tag?.ToString()=="AI activity"); };
        var remove=new Button { Content="Remove saved online API key" }; remove.Click+=(_,_)=>
        { try { credentials.Remove(policy.Provider?.Id ?? "openai"); feedback.Text="Saved credential removed."; } catch { feedback.Text="Credential removal failed."; } };
        var recovery=new Button { Content="Recover unavailable knowledge database…" }; recovery.Click+=async(_,_)=>await RecoverKnowledgeAsync();
        var actions=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 }; actions.Children.Add(save); actions.Children.Add(test);
        actions.SizeChanged+=(_,e)=>actions.Orientation=e.NewSize.Width<620?Orientation.Vertical:Orientation.Horizontal;
        panel.Children.Add(actions); panel.Children.Add(feedback);
        SettingsSection(panel,"Advanced");
        panel.Children.Add(SettingsCard("\uE81C","AI activity","See queried processes, returned assessments and provider failures",activityButton));
        panel.Children.Add(SettingsCard("\uE890","Payload preview","Review what the current sharing policy sends",preview));
        panel.Children.Add(SettingsCard("\uE8D7","API credentials","Remove the selected API key; ChatGPT sessions have their own sign-out action",remove));
        panel.Children.Add(SettingsCard("\uE713","Knowledge storage","Preserve an unavailable database and create a clean replacement",recovery));
        PageContent.Children.Add(new ScrollViewer { Content=panel,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalScrollMode=ScrollMode.Disabled });
    }
    private async Task RecoverKnowledgeAsync()
    {
        var dialog=new ContentDialog { Title="Recover knowledge database?",Content="Preserve the existing database as a backup and start fresh generated knowledge and corrections. Usage accounting stays intact. Use this only when the database is unavailable.",PrimaryButtonText="Preserve and recover",CloseButtonText="Cancel",XamlRoot=PageContent.XamlRoot };
        if(await dialog.ShowAsync()!=ContentDialogResult.Primary) return;
        try
        {
            await knowledgeMaintenance.WaitAsync(lifetime.Token);
            try
            {
            var next=policy with { AutomaticEnabled=false,Revision=policy.Revision+1 }; preferences.Save(next,presentation); policy=next;
            await coordinator.ChangePolicyAsync(policy,lifetime.Token); await coordinator.WaitForIdleAsync(lifetime.Token);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            var target=Path.GetFullPath(Path.Combine(dataFolder,"knowledge.db"));
            if(!target.StartsWith(Path.GetFullPath(dataFolder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            if(File.Exists(target)) File.Move(target,target+".recovered-"+Guid.NewGuid());
            analysis.Clear(); coordinator.InvalidateKnowledge(); await MessageAsync("Recovery ready","The original is preserved locally. Automatic analysis remains disabled; usage counts are unchanged.");
            } finally { knowledgeMaintenance.Release(); }
        }
        catch { await MessageAsync("Recovery failed","The database could not be preserved. No usage accounting was cleared."); }
    }
}

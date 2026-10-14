using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Core.Models;
using ProcSight.Infrastructure.Providers;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private void RenderSettings()
    {
        if(coordinator is null) return;
        var panel=new StackPanel { Spacing=16,MaxWidth=720,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(24) };
        panel.Children.Add(new TextBlock { Text="Settings",FontSize=24 });
        var theme=new ComboBox { Header="Appearance",ItemsSource=new[]{"System","Light","Dark"},SelectedItem=presentation.Theme };
        theme.SelectionChanged+=(_,_)=> { presentation=presentation with { Theme=(string)theme.SelectedItem }; ConfigureTheme(); SavePresentation(); };
        panel.Children.Add(theme);
        panel.Children.Add(new TextBlock { Text=runtimeWarning ?? "Analysis is local first. Enable automatic AI analysis only after configuring a provider. API access is separate from a ChatGPT subscription.",TextWrapping=TextWrapping.Wrap });
        var automatic=new CheckBox { Content="Enable automatic AI analysis of sanitized process metadata",IsChecked=policy.AutomaticEnabled };
        var researchOption=new CheckBox { Content="Enable online research (OpenAI profile only; separate identity-only requests)",IsChecked=policy.ResearchEnabled };
        var mode=new ComboBox { Header="Full command-line analysis",ItemsSource=new[]{"Off","Local only","Include in online analysis"},SelectedIndex=(int)policy.CommandLineMode };
        var disclosure=new TextBlock { Text="Online full mode sends the verbatim command line to the selected analysis provider. Arguments may contain credentials and personal paths. Local-only lines and derived findings stay out of cloud requests. Neither mode sends full lines as research queries.",TextWrapping=TextWrapping.Wrap };
        var type=new ComboBox { Header="Online provider",ItemsSource=new[]{"OpenAI","Compatible API"},SelectedIndex=policy.Provider?.Id=="compatible"?1:0 };
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
        foreach(var control in new UIElement[]{automatic,researchOption,mode,disclosure,type,endpoint,model,apiKey,strict,localEndpoint,localModel,localStrict,jobs,calls}) panel.Children.Add(control);
        var save=new Button { Content="Save settings" };
        save.Click+=async(_,_)=>
        {
            try
            {
                ProviderSettings? online=null,local=null;
                if(!string.IsNullOrWhiteSpace(model.Text))
                {
                    online=new(type.SelectedIndex==0?"openai":"compatible",new Uri(endpoint.Text.Trim()),model.Text.Trim(),strict.IsChecked==true);
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
                if(online is not null&&apiKey.Password.Length>0) credentials.Set(online.Id,apiKey.Password);
                var next=new AnalysisPolicy(automatic.IsChecked==true,researchOption.IsChecked==true,(CommandLineMode)mode.SelectedIndex,
                    online,local,new((int)jobs.Value,(int)calls.Value),policy.Revision+1);
                preferences.Save(next,presentation); policy=next;
                await coordinator.ChangePolicyAsync(policy,lifetime.Token);
                if(policy.CommandLineMode==CommandLineMode.Off)
                {
                    lastSnapshot=lastSnapshot with { Processes=lastSnapshot.Processes.Select(p=>p with { CommandLine=null }).ToList() };
                    entities=entities.Select(e=>e.Process is null?e:e with { Process=e.Process with { CommandLine=null } }).ToList();
                    knownRows.Clear(); rows.Clear();
                }
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
            try
            {
                var local=policy.Provider is null;
                if(!local&&!await usage.TryReserveAsync(UsageKind.BillableCall,DateOnly.FromDateTime(DateTime.Now),policy.Limits ?? new(),lifetime.Token))
                { feedback.Text="Daily call budget reached."; return; }
                var payload=new AnalysisPayload(new("connection-test"),new(){["name"]="synthetic-connection-test.exe"},[],null,
                    policy.Revision,local?AnalysisDestination.LocalModel:AnalysisDestination.CloudModel);
                var result=await CreateProvider(selected,local).AnalyzeAsync(payload,lifetime.Token);
                feedback.Text=result.Error?.Message ?? "Provider returned a validated response. Research support requires a research-capable model.";
            }
            catch { feedback.Text="Connection test failed; no real process metadata was sent."; }
            finally { test.IsEnabled=true; }
        };
        var preview=new Button { Content="Preview current analysis payload…" }; preview.Click+=async(_,_)=>await PreviewAsync();
        var remove=new Button { Content="Remove saved online API key" }; remove.Click+=(_,_)=>
        { try { credentials.Remove(policy.Provider?.Id ?? "openai"); feedback.Text="Saved credential removed."; } catch { feedback.Text="Credential removal failed."; } };
        var recovery=new Button { Content="Recover unavailable knowledge database…" }; recovery.Click+=async(_,_)=>await RecoverKnowledgeAsync();
        panel.Children.Add(save); panel.Children.Add(test); panel.Children.Add(preview); panel.Children.Add(remove); panel.Children.Add(recovery); panel.Children.Add(feedback);
        PageContent.Children.Add(new ScrollViewer { Content=panel });
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

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Core.Models;
using ProcSight.Application.Analysis;
using ProcSight.Application.Settings;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private ImmediateSettingsController? settingsController;
    private Task<SettingsApplyResult> ApplySettingsAsync(AnalysisPolicy next)
    {
        settingsController??=new(preferences,async (value,ct)=> { savedPolicy=value; CancelConnectionTests(); await coordinator.ChangePolicyAsync(EffectivePolicy,ct); },policy,presentation);
        return settingsController.ApplyAsync(next,presentation,lifetime.Token);
    }
    private static string SettingsResultText(SettingsApplyResult result)=>result.Failure switch {
        SettingsFailure.None=>"",
        SettingsFailure.Superseded=>"",
        SettingsFailure.Invalid=>"Complete the highlighted configuration. The last valid settings remain active.",
        _=>"Changes could not be saved. Privacy restrictions remain active for this session. Check storage access."
    };
    private void RenderSettings()
    {
        var panel=new StackPanel { Spacing=8,MaxWidth=1000,HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(24,16,24,24) };
        panel.Children.Add(new TextBlock { Text="Settings",FontSize=24,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Margin=new Thickness(0,0,0,12) });
        var feedback=SettingsFeedback();
        async Task Apply(AnalysisPolicy next) { try { var result=await ApplySettingsAsync(next); if(result.Failure!=SettingsFailure.Superseded) feedback.Text=SettingsResultText(result); } catch { feedback.Text="Settings are unavailable. Check local storage access."; } }
        SettingsSection(panel,"Appearance");
        var theme=new ComboBox { ItemsSource=new[]{"System","Light","Dark"},SelectedItem=presentation.Theme,MinWidth=170 };
        theme.SelectionChanged+=async(_,_)=> { presentation=presentation with { Theme=(string)theme.SelectedItem }; ConfigureTheme(); await Apply(policy); };
        panel.Children.Add(SettingsCard("\uE790","App theme","Choose which theme the app displays",theme));
        panel.Children.Add(BuildTraySettings());
        SettingsSection(panel,"AI connections");
        if(runtimeWarning is not null) panel.Children.Add(new InfoBar { IsOpen=true,Severity=InfoBarSeverity.Warning,Message=runtimeWarning,IsClosable=false });
        panel.Children.Add(BuildConnectionSettings());
        SettingsSection(panel,"Analysis and privacy");
        var automatic=new ToggleSwitch { Header="Automatic AI analysis",IsOn=policy.AutomaticEnabled };
        automatic.Toggled+=async(_,_)=>await Apply(policy with { AutomaticEnabled=automatic.IsOn });
        var research=new ToggleSwitch { Header="Online research (separately billed API requests)",IsOn=policy.ResearchEnabled };
        research.Toggled+=async(_,_)=>await Apply(policy with { ResearchEnabled=research.IsOn });
        panel.Children.Add(SettingsExpander("\uE9CE","Automatic analysis","Local rules and saved knowledge are always checked first",automatic,research));
        var mode=new ComboBox { Header="Command-line sharing",ItemsSource=new[]{"Exclude from online analysis","Prefer local model; exclude online","Include in online analysis"},SelectedIndex=(int)policy.CommandLineMode };
        mode.SelectionChanged+=async(_,_)=>await Apply(policy with { CommandLineMode=(CommandLineMode)mode.SelectedIndex });
        var identifying=new CheckBox { Content="Include file paths and ownership, package, and service identifiers online",IsChecked=policy.ShareIdentifyingMetadata };
        identifying.Click+=async(_,_)=>await Apply(policy with { ShareIdentifyingMetadata=identifying.IsChecked==true });
        panel.Children.Add(SettingsExpander("\uE72E","Online sharing","Choose which identifying metadata may leave this computer",mode,identifying,
            new TextBlock { Text="Full command lines always remain available locally. Online inclusion may transmit credentials and personal paths. Research never receives full commands. Changes cancel queued work; already transmitted requests cannot be recalled.",TextWrapping=TextWrapping.Wrap }));
        var jobs=new NumberBox { Header="Automatic jobs per day",Value=policy.Limits?.AutomaticJobs??20,Minimum=0,Maximum=10000,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
        var calls=new NumberBox { Header="Billable calls per day (including retries and research)",Value=policy.Limits?.BillableCalls??60,Minimum=0,Maximum=10000,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
        async void LimitsChanged(NumberBox sender,NumberBoxValueChangedEventArgs args) {
            if(double.IsFinite(jobs.Value)&&double.IsFinite(calls.Value)) await Apply(policy with { Limits=new((int)jobs.Value,(int)calls.Value) });
            else feedback.Text="Enter a valid request limit. The last valid limit remains active.";
        }
        jobs.ValueChanged+=LimitsChanged; calls.ValueChanged+=LimitsChanged;
        panel.Children.Add(SettingsExpander("\uE9D9","Request limits","Bound automatic work and online requests",jobs,calls));
        panel.Children.Add(feedback);
        SettingsSection(panel,"Advanced");
        panel.Children.Add(BuildPrivilegeSettings());
        var preview=new Button { Content="Preview payload…" }; preview.Click+=async(_,_)=>await PreviewAsync();
        var activity=new Button { Content="View AI activity" }; activity.Click+=(_,_)=>Navigation.SelectedItem=Navigation.MenuItems.OfType<NavigationViewItem>().First(i=>i.Tag?.ToString()=="AI activity");
        var recovery=new Button { Content="Recover knowledge database…" }; recovery.Click+=async(_,_)=>await RecoverKnowledgeAsync();
        panel.Children.Add(SettingsCard("\uE81C","AI activity","See actual provider attempts, returned assessments and failures",activity));
        panel.Children.Add(SettingsCard("\uE890","Payload preview","Review the current sharing policy",preview));
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
            await ApplySettingsAsync(policy with { AutomaticEnabled=false }); await coordinator.WaitForIdleAsync(lifetime.Token);
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

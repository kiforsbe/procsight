using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Application.Settings;
using ProcSight.Core.Models;
using ProcSight.Windows.Security;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private bool elevated=PrivilegeContext.IsElevated;
    private SessionConsent sessionConsent=new();
    private AnalysisPolicy? cachedSaved,cachedEffective;
    private SessionConsent? cachedConsent;
    private AnalysisPolicy EffectivePolicy {
        get {
            var applied=settingsController?.Current??savedPolicy;
            if(!elevated) return applied;
            if(!ReferenceEquals(cachedSaved,applied)||cachedConsent!=sessionConsent) {
                cachedSaved=applied; cachedConsent=sessionConsent; cachedEffective=SessionPolicy.Effective(applied,true,sessionConsent);
            }
            return cachedEffective!;
        }
    }
    private FrameworkElement BuildPrivilegeSettings()
    {
        var panel=new StackPanel { Spacing=12 };
        var feedback=new TextBlock { Text=elevated?
            "Administrator · This entire app is elevated. Protected Windows processes can still deny metadata access. To run normally, close ProcSight and reopen it from the unelevated Windows shell.":
            "Standard user · Administrator mode can reveal more process metadata. It elevates the entire app, including storage and network code. Only elevate a trusted build and its dependencies.",TextWrapping=TextWrapping.Wrap };
        panel.Children.Add(feedback);
        if(!elevated) {
            var package=PackageContext.Inspect();
            if(!package.CanRestartElevated) panel.Children.Add(new InfoBar { IsOpen=true,IsClosable=false,Severity=InfoBarSeverity.Informational,
                Title="Administrator restart unavailable",Message=package.InspectionSucceeded?
                "This installed package does not declare permission to restart elevated. Normal process investigation remains available.":
                "Package elevation permissions could not be verified. Restart is disabled for this session." });
            var restart=new Button { IsEnabled=package.CanRestartElevated,Content=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8,Children={new FontIcon { Glyph="\uEA18",FontSize=16 },new TextBlock { Text="Restart as administrator" } } } };
            restart.Click+=async(_,_)=> {
                restart.IsEnabled=false; feedback.Text="Waiting for Windows elevation consent and application startup…";
                try {
                    var result=await new ElevationLauncher().RestartAsync(lifetime.Token);
                    if(result==ElevationResult.Ready) { Close(); return; }
                    feedback.Text=result switch { ElevationResult.Cancelled=>"Elevation cancelled. This instance is still available.",
                        ElevationResult.TimedOut=>"The administrator instance did not confirm startup in time. This instance remains available; check whether another window opened.",
                        _=>"Administrator startup failed. This instance is still available. Check the Windows App Runtime installation and executable access." };
                } catch(OperationCanceledException) { feedback.Text="Restart cancelled."; }
                catch { feedback.Text="Administrator startup could not be confirmed. This instance remains available."; }
                finally { restart.IsEnabled=true; }
            };
            panel.Children.Add(restart);
        } else {
            var external=new ToggleSwitch { Header="Allow online analysis for this administrator session",IsOn=sessionConsent.ExternalAnalysis };
            var commands=new CheckBox { Content="Also allow saved command-line sharing in this session",IsChecked=sessionConsent.CommandLine };
            var identifying=new CheckBox { Content="Also allow saved identifying-metadata sharing in this session",IsChecked=sessionConsent.IdentifyingMetadata };
            var disclosure=new TextBlock { Text="These permissions reset on restart and do not change your normal saved settings. Newly readable metadata may belong to another user. Online analysis, tests and research require session consent; command/path sharing also requires the normal sharing settings.",TextWrapping=TextWrapping.Wrap };
            async Task Consent() {
                sessionConsent=new(external.IsOn,commands.IsChecked==true,identifying.IsChecked==true);
                CancelConnectionTests();
                if(settingsController is not null) await settingsController.ReapplyAsync(lifetime.Token);
                else { policy=policy with { Revision=policy.Revision+1 }; await coordinator.ChangePolicyAsync(EffectivePolicy,lifetime.Token); }
                feedback.Text="Administrator-session permissions updated. Already transmitted requests cannot be recalled.";
            }
            external.Toggled+=async(_,_)=>await Consent(); commands.Click+=async(_,_)=>await Consent(); identifying.Click+=async(_,_)=>await Consent();
            panel.Children.Add(external); panel.Children.Add(commands); panel.Children.Add(identifying); panel.Children.Add(disclosure);
        }
        var section=SettingsExpander("\uEA18","Administrator access",elevated?"Administrator · session sharing permissions":"Explicit restart through Windows UAC",panel);
        section.IsExpanded=elevated; return section;
    }
}

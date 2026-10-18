using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
using ProcSight.Windows.Tray;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private NotificationIcon? tray;
    private readonly TrayMetricSampler traySampler=new();
    private Task? trayLoop;
    private void ConfigureTray()
    {
        if(!presentation.ShowTrayIcon) {
            tray?.Hide();
            // A previously hidden window must stay recoverable when the tray is disabled.
            if(!AppWindow.IsVisible) RestoreFromTray();
            return;
        }
        try {
            if(tray is null) {
                tray=new(WinRT.Interop.WindowNative.GetWindowHandle(this));
                tray.Command+=command=> {
                    if(command==TrayCommand.Exit) { RequestShutdown(); return; }
                    if(command==TrayCommand.Open) RestoreFromTray();
                    else if(command==TrayCommand.Settings) { RestoreFromTray(); Navigation.SelectedItem=Navigation.SettingsItem; }
                    else if((int)command>=10) { presentation=presentation with { TrayMetric=(TrayMetric)((int)command-10) }; SavePresentation(); }
                };
                tray.Minimized+=()=>DispatcherQueue.TryEnqueue(()=> {
                    if(presentation.MinimizeToTray&&presentation.ShowTrayIcon&&tray.Registered) AppWindow.Hide();
                });
                tray.Unavailable+=()=>DispatcherQueue.TryEnqueue(RestoreFromTray);
            }
            if(!tray.Show()) { runtimeWarning="The tray icon is unavailable. ProcSight will remain accessible as a normal window."; RestoreFromTray(); }
            trayLoop??=RunTrayAsync();
        } catch { runtimeWarning="The tray icon could not be created. The main window remains available."; RestoreFromTray(); }
    }
    private void RestoreFromTray() {
        AppWindow.Show(); if(AppWindow.Presenter is OverlappedPresenter presenter) presenter.Restore(); Activate();
    }
    private async Task RunTrayAsync() {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1));
        try {
            do {
                if(presentation.ShowTrayIcon) {
                    var metric=presentation.TrayMetric;
                    var value=await traySampler.SampleAsync(metric,lastSnapshot.SystemResources,lifetime.Token);
                    if(lifetime.IsCancellationRequested) return;
                    DispatcherQueue.TryEnqueue(()=> { if(!lifetime.IsCancellationRequested&&presentation.ShowTrayIcon&&presentation.TrayMetric==metric) tray?.Update(value); });
                }
            } while(await timer.WaitForNextTickAsync(lifetime.Token));
        } catch(OperationCanceledException) { }
        catch { DispatcherQueue.TryEnqueue(()=>runtimeWarning="Tray monitoring is unavailable; process collection continues."); }
    }
    private FrameworkElement BuildTraySettings()
    {
        var show=new ToggleSwitch { Header="Show system tray icon",IsOn=presentation.ShowTrayIcon };
        var metric=new ComboBox { Header="System load to display",ItemsSource=new[]{"CPU","Memory","Disk busy","Network throughput","Busiest GPU engine"},SelectedIndex=(int)presentation.TrayMetric };
        var minimize=new ToggleSwitch { Header="Hide the window in the tray when minimized",IsOn=presentation.MinimizeToTray };
        var feedback=SettingsFeedback();
        async Task Apply() {
            presentation=presentation with { ShowTrayIcon=show.IsOn,TrayMetric=(TrayMetric)metric.SelectedIndex,MinimizeToTray=minimize.IsOn };
            ConfigureTray();
            try { feedback.Text=SettingsResultText(await ApplySettingsAsync(policy)); } catch { feedback.Text="Tray preference could not be saved."; }
        }
        show.Toggled+=async(_,_)=>await Apply(); metric.SelectionChanged+=async(_,_)=>await Apply(); minimize.Toggled+=async(_,_)=>await Apply();
        return SettingsExpander("\uE9D9","System tray","Choose a live load metric and minimize behavior",show,metric,minimize,feedback);
    }
}

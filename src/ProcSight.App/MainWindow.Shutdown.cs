using ProcSight.Application.Lifecycle;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private readonly ShutdownWork shutdown=new();
    private bool shutdownComplete;
    private bool shutdownStarted;
    private Func<Task>? cleanupResources;
    private Task? collectionLoop;
    private void ConfigureShutdown(Func<Task> cleanup) {
        cleanupResources=cleanup;
        AppWindow.Closing+=(_,args)=> {
            if(shutdownComplete) return;
            args.Cancel=true;
            RequestShutdown();
        };
    }
    private async void RequestShutdown() {
        if(shutdownStarted) return; shutdownStarted=true;
        lifetime.Cancel(); resourceShadeTimer?.Stop(); tray?.Dispose(); AppWindow.Hide();
        try { if(cleanupResources is { } cleanup) await shutdown.RunAsync(cleanup); }
        catch { /* Cleanup failures must not escape the native closing callback. */ }
        finally { shutdownComplete=true; Close(); }
    }
    private async Task CleanupResourcesAsync() {
        // Keep the window dispatcher alive until UI-awaiting loops can observe cancellation.
        // ETW stop and native counter disposal may block: ShutdownWork keeps them off the UI thread.
        try { await collector.DisposeAsync().ConfigureAwait(false); }
        finally {
            try { await Task.WhenAll(new[]{trayLoop,performanceLoop,collectionLoop}.OfType<Task>()).ConfigureAwait(false); }
            finally {
                traySampler.Dispose();
                try { await performanceSampler.DisposeAsync().ConfigureAwait(false); }
                finally { try { await coordinator.DisposeAsync().ConfigureAwait(false); } finally { providerHttp.Dispose(); } }
            }
        }
    }
}

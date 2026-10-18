using Microsoft.UI.Xaml;
namespace ProcSight.App;
public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? window;
    public App() {
        InitializeComponent();
        UnhandledException+=(_,args)=> {
            // Retain default crash behavior; record a numeric code, never exception text.
            if(window is MainWindow main) main.TraceUnhandledUiException(args.Exception.HResult);
        };
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new MainWindow(); window.Activate();
    }
}

#if DEBUG
using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using ProcSight.Application.Presentation;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private async Task CheckTableMemoryAsync(string output,double cacheLength) {
        var panel=(ItemsStackPanel)table!.ItemsPanelRoot;
        if(cacheLength==-1) {
            if(panel.CacheLength!=1) throw new InvalidOperationException("Table must retain one viewport of off-screen rows, not the WinUI four-viewport default.");
            cacheLength=panel.CacheLength;
        } else {
            if(cacheLength is <0.5 or >4) throw new ArgumentOutOfRangeException(nameof(cacheLength));
            panel.CacheLength=cacheLength;
        }
        var seed=lastSnapshot.Processes[0];
        lastSnapshot=lastSnapshot with { Processes=Enumerable.Range(0,1000).Select(i=>seed with {
            Key=new(10000+i,seed.Key.StartedAt,null),ParentPid=null,
            Executable=new("synthetic.exe",$@"C:\Synthetic\worker-{i}.exe",Product:"Synthetic application"),CommandLine=null }).ToArray() };
        entities=new ProcSight.Core.Identity.IdentityResolver().Resolve(lastSnapshot,[]);
        RefreshRows(true); await Task.Delay(1200);
        var scroller=FindTableScrollViewer(table)!;
        scroller.ChangeView(null,scroller.ScrollableHeight/2,null,true); await Task.Delay(1500); WindowRoot.UpdateLayout();
        using var process=Process.GetCurrentProcess();
        process.Refresh();
        var initial=new { workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,
            managedBytes=GC.GetTotalMemory(false),realizedRows=RealizedColumnRows().Count() };
        var timings=new List<double>();
        for(var i=0;i<90;i++) {
            var at=Stopwatch.GetTimestamp();
            scroller.ChangeView(null,scroller.ScrollableHeight/2+(i%30-15)*112,null,true); WindowRoot.UpdateLayout();
            timings.Add(Stopwatch.GetElapsedTime(at).TotalMilliseconds); await Task.Delay(16);
        }
        process.Refresh();
        File.WriteAllText(output+".table-memory.json",JsonSerializer.Serialize(new { cacheLength,initial,
            workingSet=process.WorkingSet64,privateBytes=process.PrivateMemorySize64,realizedRows=RealizedColumnRows().Count(),
            scrollMeanMs=timings.Average(),scrollP95Ms=timings.Order().ElementAt(85),scrollMaxMs=timings.Max(),
            overFrame=timings.Count(t=>t>16.67) },new JsonSerializerOptions { WriteIndented=true }));
        if(rows.Count!=1000||RealizedColumnRows().Count()>=300) throw new InvalidOperationException("Table virtualization failed.");
    }
    private async Task CheckMemoryPerformanceAsync(string output) {
        var readings=new List<object>();
        var retired=new List<WeakReference>();
        void Sample(string stage,double ms=0,long allocated=0) {
            using var process=Process.GetCurrentProcess();
            readings.Add(new { stage,ms,allocated,managedBytes=GC.GetTotalMemory(false),heapBytes=GC.GetGCMemoryInfo().HeapSizeBytes,
                privateBytes=process.PrivateMemorySize64,workingSet=process.WorkingSet64,handles=process.HandleCount,rows=knownRows.Count,
                retiredViewsAlive=retired.Count(r=>r.IsAlive) });
            File.WriteAllText(output+".memory.json",JsonSerializer.Serialize(readings,new JsonSerializerOptions { WriteIndented=true }));
        }
        async Task Retained(string stage) { await Task.Delay(150); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); await Task.Delay(150); Sample(stage); }
        await Retained("initial-process-page");
        page="Performance"; RenderPage(); await Task.Delay(200);
        var missing=ProcSight.Core.Models.Observation<double>.Available(25);
        performanceHistory.Clear();
        for(var i=0;i<60;i++) performanceHistory.Add(new(DateTimeOffset.UtcNow.AddSeconds(i-60),missing,missing,missing,missing,missing,missing,16L<<30,8L<<30,8));
        for(var pass=0;pass<3;pass++) {
            var bytes=GC.GetAllocatedBytesForCurrentThread(); var at=Stopwatch.GetTimestamp();
            for(var i=0;i<60;i++) { performanceSubsystem=(PerformanceSubsystem)(i%6); changePerformanceSection?.Invoke(); WindowRoot.UpdateLayout(); await Task.Delay(16); }
            Sample("dashboard-navigation-"+pass,Stopwatch.GetElapsedTime(at).TotalMilliseconds,GC.GetAllocatedBytesForCurrentThread()-bytes);
            await Retained("dashboard-retained-"+pass);
        }
        for(var i=0;i<20;i++) {
            retired.Add(new(PageContent.Children[0])); page="Processes"; RenderPage(); WindowRoot.UpdateLayout(); await Task.Delay(20);
            retired.Add(new(PageContent.Children[0])); page="Performance"; RenderPage(); WindowRoot.UpdateLayout(); await Task.Delay(20);
        }
        await Retained("page-navigation-retained");
        page="Processes"; RenderPage(); await Retained("final-process-page");
        File.WriteAllText(output+".checks","Synthetic native dashboard/process memory measurements completed; no provider or credentials.");
    }
    private async Task CheckShutdownAsync(string output) {
        using var entered=new ManualResetEventSlim(); using var release=new ManualResetEventSlim(); var calls=0;
        ConfigureShutdown(()=> { Interlocked.Increment(ref calls); entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); return Task.CompletedTask; });
        var at=Stopwatch.GetTimestamp(); SendCloseMessage(WinRT.Interop.WindowNative.GetWindowHandle(this),0x10,IntPtr.Zero,IntPtr.Zero);
        var closeMs=Stopwatch.GetElapsedTime(at).TotalMilliseconds;
        try {
            await Task.Delay(100);
            if(!entered.IsSet||shutdownComplete||AppWindow.IsVisible||closeMs>250) throw new InvalidOperationException("Closing blocked the dispatcher or destroyed it before cleanup.");
            RequestShutdown(); await Task.Delay(50);
            if(calls!=1) throw new InvalidOperationException("Repeated closing duplicated cleanup.");
            File.WriteAllText(output+".checks",$"Close returned in {closeMs:F2} ms; dispatcher responsive during blocked cleanup; cleanup called once.");
        } finally { release.Set(); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SendMessageW")]
    private static extern IntPtr SendCloseMessage(IntPtr hwnd,uint message,IntPtr wparam,IntPtr lparam);
}
#endif

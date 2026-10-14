#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    // Developer-only rendering of the actual XAML tree. No machine collection, preferences,
    // credential access or provider requests occur in this mode.
    private bool TryStartVisualFixture()
    {
        var args=Environment.GetCommandLineArgs();
        if(args.Length is <3 or >6 || args[1]!="--visual-fixture") return false;
        var output=Path.GetFullPath(args[2]);
        presentation=new(Theme:args.Length>3&&args[3]=="Light"?"Light":"Dark",Grouped:args.Length>5&&args[5]=="groups"); sort=ProcSight.Application.Presentation.ProcessSort.Name;
        ConfigureChrome(); WindowRoot.Background=new SolidColorBrush(presentation.Theme=="Light"?Microsoft.UI.Colors.White:global::Windows.UI.Color.FromArgb(255,32,32,32));
        if(args.Length>4&&int.TryParse(args[4],out var width)&&width is >=600 and <=1600)
        { var scale=GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))/96.0; AppWindow.Resize(new((int)(width*scale),(int)(780*scale))); }
        var now=DateTimeOffset.UtcNow;
        var names=new[]{"Code.exe","chrome.exe","node.exe","node.exe","node.exe","svchost.exe","svchost.exe",
            "OneDrive.exe","Plex Media Server.exe","NVDisplay.Container.exe","Notepad.exe","pwsh.exe",
            "RuntimeBroker.exe","SearchIndexer.exe","Widgets.exe","msedgewebview2.exe","msedgewebview2.exe",
            "explorer.exe","ApplicationFrameHost.exe","dllhost.exe","conhost.exe","spoolsv.exe","SecurityHealthService.exe"};
        var processes=names.Select((name,i)=>new ProcessSnapshot(new(1000+i,now,null),null,
            new(name,name=="Notepad.exe"?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe"):$@"C:\Program Files\Example\{name}","fixture-"+i,Product:name=="node.exe"?"Example editor":null),
            new(Observation<double>.Available(i==0?3.2:0),Observation<long>.Available((12+i*5)*1048576L),
                Observation<double>.Missing("Not collected"),Observation<double>.Missing("Not collected"),now,TimeSpan.FromSeconds(1)),[],
                name=="node.exe"?new CommandLineValue(i==2?"node.exe --type=extensionHost":i==3?"node.exe --type=renderer":"node.exe --type=gpu-process",now):null)).ToList();
        lastSnapshot=new(Guid.NewGuid(),now,processes,[],new(36,16L*1024*1024*1024,7L*1024*1024*1024));
        entities=new IdentityResolver().Resolve(lastSnapshot,[]);
        var node=entities.First(e=>e.Process?.Executable.Name=="node.exe");
        analysis[node.Identity]=new(node.Identity,AnalysisState.Local,new([
            new(AssessmentAxis.Purpose,"DeveloperTool","Synthetic fixture: application worker",Confidence.Medium,[],now.AddDays(7),[DataOrigin.ShareableMetadata])],
            "Example editor",[],[],new HashSet<AssessmentAxis>{AssessmentAxis.Security,AssessmentAxis.Optionality},"Example editor — extension host","Worker"));
        RenderPage();
        if(args.Length>5&&args[5]=="details")
        { detailsOpen=true; table!.SelectedItem=rows.First(r=>r.Entity.Identity==node.Identity); UpdateDetailsLayout(); ShowDetail(table.SelectedItem as ViewModels.ProcessRow); }
        WindowRoot.Loaded+=async(_,_)=>
        {
            try
            {
                await Task.Delay(500);
                if(args.Length>5&&args[5]=="groups")
                {
                    var group=rows.First(r=>r.IsGroup&&r.Projection.Members.Count>1); table!.SelectedItem=group;
                    var before=rows.Count; var button=new Microsoft.UI.Xaml.Controls.Button { Tag=group.Key };
                    ToggleExpansion(button,new RoutedEventArgs());
                    if(rows.Count<=before) throw new InvalidOperationException("Group did not expand.");
                    ToggleExpansion(button,new RoutedEventArgs());
                    if(rows.Count!=before) throw new InvalidOperationException("Selected group did not collapse.");
                    ToggleExpansion(button,new RoutedEventArgs());
                    File.WriteAllText(output+".checks","Selected group expands, collapses and re-expands without reselection.");
                    await Task.Delay(250); WindowRoot.UpdateLayout();
                }
                if(args.Length>5&&args[5]=="details")
                { detailsOpen=true; table!.SelectedItem=rows.First(r=>r.Entity.Identity==node.Identity); UpdateDetailsLayout(); ShowDetail(table.SelectedItem as ViewModels.ProcessRow); }
                var bitmap=new RenderTargetBitmap(); await bitmap.RenderAsync(WindowRoot);
                var pixels=await bitmap.GetPixelsAsync();
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                var folder=await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(output)!);
                var file=await folder.CreateFileAsync(Path.GetFileName(output),CreationCollisionOption.ReplaceExisting);
                using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);
                var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,pixels.ToArray());
                await encoder.FlushAsync();
            }
            catch(Exception e) { File.WriteAllText(output+".error",e.GetType().Name+": "+e.Message); }
            finally { Close(); }
        };
        Activate(); return true;
    }
}
#endif

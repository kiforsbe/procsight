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
        if(args.Length>5&&args[5].StartsWith("settings",StringComparison.Ordinal)) {
            chatGpt=new(new FixtureCredentials()); page="Settings";
        }
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
        if(args.Length>5&&args[5]=="scroll") {
            processes.AddRange(Enumerable.Range(0,60).Select(i=>processes[0] with { Key=new(2000+i,now,null),
                Executable=new("python.exe",Product:"Python"),CommandLine=new($"python.exe script_{i:00}.py --token PRIVATE",now) }));
        }
        lastSnapshot=new(Guid.NewGuid(),now,processes,[],new(36,16L*1024*1024*1024,7L*1024*1024*1024));
        entities=new IdentityResolver().Resolve(lastSnapshot,[]);
        var node=entities.First(e=>e.Process?.Executable.Name=="node.exe");
        analysis[node.Identity]=new(node.Identity,AnalysisState.Local,new([
            new(AssessmentAxis.Purpose,"DeveloperTool","Synthetic fixture: application worker",Confidence.Medium,[],now.AddDays(7),[DataOrigin.ShareableMetadata])],
            "Example editor",[],[],new HashSet<AssessmentAxis>{AssessmentAxis.Security,AssessmentAxis.Optionality},"Example editor — extension host","Worker"));
        if(args.Length>5&&args[5].StartsWith("settings",StringComparison.Ordinal)) page="Settings";
        if(args.Length>5&&args[5].StartsWith("activity",StringComparison.Ordinal)) {
            page="AI activity";
            for(var i=0;i<8;i++) {
                var payload=new AnalysisPayload(new("fixture-activity-"+i),new(){["name"]=i%2==0?"node.exe":"example-service.exe",["product"]="Example editor"},[],null,0,AnalysisDestination.CloudModel);
                var request=aiActivity.Begin(payload,new("chatgpt",new("https://api.openai.com/v1"),"example-model"),i%2==0?"Example editor — Renderer":"Example service",i%2==0?1000+i:null);
                if(i==7) continue;
                aiActivity.Complete(request,i==6?new(null,new(ProviderErrorKind.RateLimited,"Synthetic limit")):new(new([
                    new(AssessmentAxis.Purpose,"DeveloperTool","Synthetic fixture: this worker renders the application's interface.",Confidence.Medium,[],now.AddDays(1),[DataOrigin.ShareableMetadata]),
                    new(AssessmentAxis.Security,"Unknown","No threat evidence was submitted.",Confidence.Low,[],now.AddDays(1),[DataOrigin.ShareableMetadata])],[])),payload);
            }
        }
        RenderPage();
        if(args.Length>5&&args[5]=="details")
        { detailsOpen=true; table!.SelectedItem=rows.First(r=>r.Entity.Identity==node.Identity); UpdateDetailsLayout(); ShowDetail(table.SelectedItem as ViewModels.ProcessRow); }
        WindowRoot.Loaded+=async(_,_)=>
        {
            try
            {
                await Task.Delay(500);
                if(args.Length>5&&args[5].StartsWith("settings",StringComparison.Ordinal)) {
                    Navigation.SelectedItem=Navigation.SettingsItem; page="Settings"; RenderPage();
                    if(args[5]=="settings-expanded"&&PageContent.Children.FirstOrDefault() is Microsoft.UI.Xaml.Controls.ScrollViewer { Content:Microsoft.UI.Xaml.Controls.StackPanel settingsPanel })
                        settingsPanel.Children.OfType<Microsoft.UI.Xaml.Controls.Expander>().First().IsExpanded=true;
                    await Task.Delay(250); WindowRoot.UpdateLayout();
                }
                if(args.Length>5&&args[5].StartsWith("activity",StringComparison.Ordinal)) {
                    Navigation.SelectedItem=Navigation.MenuItems.OfType<Microsoft.UI.Xaml.Controls.NavigationViewItem>().First(i=>i.Tag?.ToString()=="AI activity");
                    page="AI activity"; RenderPage(); await Task.Delay(250); WindowRoot.UpdateLayout();
                    if(args[5]=="activity-details") {
                        Microsoft.UI.Xaml.Controls.ListView? FindList(DependencyObject parent) {
                            if(parent is Microsoft.UI.Xaml.Controls.ListView found) return found;
                            for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) if(FindList(VisualTreeHelper.GetChild(parent,i)) is { } nested) return nested;
                            return null;
                        }
                        var activityList=FindList(PageContent)!; activityList.SelectedIndex=2; await Task.Delay(150);
                    }
                }
                if(args.Length>5&&args[5]=="scroll") {
                    var scrolling=FindTableScrollViewer(table!) ?? throw new InvalidOperationException("Missing native scrolling viewport.");
                    if(scrolling.ScrollableWidth<=0 || scrolling.ScrollableHeight<=0) throw new InvalidOperationException("Fixture must overflow both axes.");
                    DependencyObject? FindBar(DependencyObject parent) {
                        if(parent is Microsoft.UI.Xaml.Controls.Primitives.ScrollBar { Orientation:Microsoft.UI.Xaml.Controls.Orientation.Vertical }) return parent;
                        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
                            if(FindBar(VisualTreeHelper.GetChild(parent,i)) is { } found) return found;
                        return null;
                    }
                    var bar=(FrameworkElement)(FindBar(scrolling) ?? throw new InvalidOperationException("Missing vertical scrollbar."));
                    var origin=bar.TransformToVisual(WindowRoot).TransformPoint(new(0,0));
                    if(origin.X<0 || origin.X+bar.ActualWidth>WindowRoot.ActualWidth+1) throw new InvalidOperationException("Vertical bar is outside window.");
                    scrolling.ChangeView(scrolling.ScrollableWidth/2,100,null,true); await Task.Delay(250);
                    var moved=bar.TransformToVisual(WindowRoot).TransformPoint(new(0,0));
                    if(Math.Abs(moved.X-origin.X)>1) throw new InvalidOperationException("Horizontal scroll moved vertical bar.");
                    foreach(var header in sortHeaders.Values)
                        if(header.Button.ActualHeight<49) throw new InvalidOperationException("Header button does not fill the header height.");
                    SelectSort(ProcSight.Application.Presentation.ProcessSort.Pid);
                    if(sortDescending) throw new InvalidOperationException("PID sort must initially ascend.");
                    SelectSort(ProcSight.Application.Presentation.ProcessSort.Pid);
                    if(!sortDescending) throw new InvalidOperationException("Repeated click must descend.");
                    SelectSort(ProcSight.Application.Presentation.ProcessSort.Name);
                    scrolling.ChangeView(0,0,null,true); await Task.Delay(150);
                    File.WriteAllText(output+".checks","Both axes overflow. Vertical bar stays within the window during horizontal scrolling. Full-height header hit targets and sort direction toggling pass.");
                }
                if(args.Length>5&&args[5]=="groups")
                {
                    var group=rows.First(r=>r.IsGroup&&r.Projection.Members.Count>1); table!.SelectedItem=group;
                    var before=rows.Count; var button=new Microsoft.UI.Xaml.Controls.Button { Tag=group.Key };
                    ToggleExpansion(button,new RoutedEventArgs());
                    if(rows.Count<=before) throw new InvalidOperationException("Group did not expand.");
                    ToggleExpansion(button,new RoutedEventArgs());
                    if(rows.Count!=before) throw new InvalidOperationException("Selected group did not collapse.");
                    ToggleExpansion(button,new RoutedEventArgs());
                    var child=rows.First(r=>!r.IsGroup&&r.Projection.Depth>0); table.SelectedItem=child;
                    var originalSnapshot=lastSnapshot; var originalEntities=entities;
                    var originalAnalysis=analysis.ToDictionary(p=>p.Key,p=>p.Value);
                    var siblings=group.Projection.Members.Select(m=>m.Process!.Key.Pid).ToHashSet();
                    lastSnapshot=lastSnapshot with { Processes=lastSnapshot.Processes.Where(p=>!siblings.Contains(p.Key.Pid)||p.Key==child.Entity.Process!.Key).ToList() };
                    entities=new IdentityResolver().Resolve(lastSnapshot,[]); RefreshRows();
                    if(table.SelectedItem is not ViewModels.ProcessRow { IsGroup:false } single ||
                        single.Pid!=child.Entity.Process!.Key.Pid.ToString() || single.Projection.HasChildren || single.Name.EndsWith("(1)"))
                        throw new InvalidOperationException("Singleton must be a selected process with PID and no group header.");
                    lastSnapshot=originalSnapshot; entities=originalEntities;
                    foreach(var pair in originalAnalysis) analysis[pair.Key]=pair.Value;
                    RefreshRows();
                    File.WriteAllText(output+".checks","Group expands/collapses without reselection; selected child survives as a standalone process with PID and no group header when siblings exit.");
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
    private sealed class FixtureCredentials : ProcSight.Application.Settings.ICredentialStore
    {
        private readonly Dictionary<string,string> values=[];
        public string? Get(string id)=>values.GetValueOrDefault(id);
        public void Set(string id,string value)=>values[id]=value;
        public void Remove(string id)=>values.Remove(id);
    }
}
#endif

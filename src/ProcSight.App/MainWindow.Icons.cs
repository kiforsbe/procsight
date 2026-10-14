using Microsoft.UI.Xaml.Media.Imaging;
using ProcSight.App.ViewModels;
using ProcSight.Windows.Collection;
using System.Runtime.InteropServices.WindowsRuntime;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private readonly Dictionary<string,WriteableBitmap?> iconCache=new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> pendingIcons=new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim iconGate=new(2);
    private void QueueIcon(ProcessRow row)
    {
        var exe=row.Entity.Process?.Executable;
        if(exe?.FullPath is not { } path) return;
        var key=path+"|"+exe.FileIdentity;
        if(iconCache.TryGetValue(key,out var cached)) { if(cached is not null&&!ReferenceEquals(row.Icon,cached)) row.SetIcon(cached); return; }
        if(!pendingIcons.Add(key)) return;
        _=LoadIconAsync(key,path);
    }
    private async Task LoadIconAsync(string key,string path)
    {
        try {
            await iconGate.WaitAsync(lifetime.Token);
            ExecutableIcon? data;
            try { data=await Task.Run(()=>ExecutableIconReader.Read(path),lifetime.Token); }
            finally { iconGate.Release(); }
            if(lifetime.IsCancellationRequested) return;
            WriteableBitmap? image=null;
            if(data is not null) {
                image=new(data.Width,data.Height);
                using var stream=image.PixelBuffer.AsStream(); stream.Write(data.Pixels); image.Invalidate();
            }
            if(iconCache.Count>=512) iconCache.Clear();
            iconCache[key]=image;
            if(image is not null) foreach(var row in knownRows.Values.Where(r=>r.Entity.Process?.Executable is { } e && e.FullPath+"|"+e.FileIdentity==key)) row.SetIcon(image);
        } catch(OperationCanceledException) { }
        catch { iconCache[key]=null; }
        finally { pendingIcons.Remove(key); }
    }
}

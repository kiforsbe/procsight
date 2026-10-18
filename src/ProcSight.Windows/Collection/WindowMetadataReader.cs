using System.Runtime.InteropServices;
using System.Text;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Collection;
/// <summary>Read visible desktop captions only; never query controls or automate another app.</summary>
public static class WindowMetadataReader
{
    private delegate bool EnumWindowCallback(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(EnumWindowCallback callback,IntPtr parameter);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr window,StringBuilder text,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassNameW(IntPtr window,StringBuilder text,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr SendMessageTimeoutW(IntPtr window,uint message,UIntPtr count,
        StringBuilder text,uint flags,uint timeout,out UIntPtr result);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
    public static IReadOnlyDictionary<int,IReadOnlyList<ProcessWindow>> Read(IReadOnlyCollection<ProcessKey> processes)
    {
        var expected=processes.ToDictionary(p=>p.Pid); var result=new Dictionary<int,List<ProcessWindow>>();
        var foreground=GetForegroundWindow(); var visited=0;
        var complete=EnumWindows((window,parameter)=> {
            if(++visited>4096) return false;
            if(!IsWindowVisible(window)||((long)GetWindowLongPtr(window,-20)&0x80)!=0) return true;
            if(GetWindowThreadProcessId(window,out var id)==0||id==0) return true;
            var pid=(int)id;
            if(!expected.ContainsKey(pid)) return true;
            if(!result.TryGetValue(pid,out var windows)) result[pid]=windows=[];
            if(windows.Count>=32) return true;
            var title=new StringBuilder(513); var name=new StringBuilder(256);
            // GetWindowText sends synchronous messages for same-process windows. Bound those waits.
            if(id==(uint)Environment.ProcessId) SendMessageTimeoutW(window,0xD,new UIntPtr((uint)title.Capacity),title,0x2|0x20,50,out _);
            else GetWindowTextW(window,title,title.Capacity);
            GetClassNameW(window,name,name.Capacity);
            GetWindowThreadProcessId(window,out var confirmed);
            if(confirmed==id) windows.Add(new(title.ToString(),name.ToString(),window==foreground,IsIconic(window)));
            return true;
        },IntPtr.Zero);
        if(!complete) return new Dictionary<int,IReadOnlyList<ProcessWindow>>();
        var output=new Dictionary<int,IReadOnlyList<ProcessWindow>>();
        foreach(var (pid,key) in expected) {
            // Known process lifetimes must still match; unknown lifetimes are snapshot-only observations.
            if(result.ContainsKey(pid) && key.StartedAt is { } started && WindowsNativeMethods.ReadCounters(pid).Started!=started) continue;
            output[pid]=result.TryGetValue(pid,out var windows)?windows:[];
        }
        return output;
    }
}

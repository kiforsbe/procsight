using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace ProcSight.Windows.Security;
public enum ElevationResult { Ready,Cancelled,Failed,TimedOut }
public sealed record ElevationProcess(int Pid,Func<bool> Exited,Func<bool> Ready,IDisposable Lifetime);
public sealed record ElevationStart(ElevationResult? Failure,ElevationProcess? Process);
public sealed class ElevationLauncher(Func<ElevationStart>? start=null,TimeSpan? timeout=null)
{
    private const string ReadyProperty="ProcSight.ElevatedStartupReady.v1";
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct ExecuteInfo {
        public uint Size,Mask; public IntPtr Window;
        [MarshalAs(UnmanagedType.LPWStr)] public string Verb;
        [MarshalAs(UnmanagedType.LPWStr)] public string File;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Parameters;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Directory;
        public int Show; public IntPtr Instance,List,Class,ClassKey; public uint HotKey; public IntPtr Icon,Process;
    }
    private delegate bool EnumProc(IntPtr window,IntPtr data);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool ShellExecuteExW(ref ExecuteInfo info);
    [DllImport("kernel32.dll")] private static extern uint GetProcessId(SafeProcessHandle process);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(SafeProcessHandle process,uint milliseconds);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback,IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetPropW(IntPtr window,string name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool SetPropW(IntPtr window,string name,IntPtr value);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved,uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    public static bool MarkReady(IntPtr window)=>SetPropW(window,ReadyProperty,new(1));
    public async Task<ElevationResult> RestartAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Do not cancel/discard the shell task: its returned process handle must always be disposed.
        var launched=await Task.Run(start??StartNative);
        if(launched.Process is not { } process) return launched.Failure??ElevationResult.Failed;
        using(process.Lifetime) {
            var timer=System.Diagnostics.Stopwatch.StartNew();
            while(timer.Elapsed<(timeout??TimeSpan.FromSeconds(20))) {
                ct.ThrowIfCancellationRequested();
                if(process.Exited()) return ElevationResult.Failed;
                if(process.Ready()&&!process.Exited()) return ElevationResult.Ready;
                await Task.Delay(100,ct);
            }
            return ElevationResult.TimedOut;
        }
    }
    private static ElevationStart StartNative()
    {
        var path=Environment.ProcessPath;
        if(path is null||!Path.IsPathFullyQualified(path)||!Path.GetFileName(path).Equals("ProcSight.App.exe",StringComparison.OrdinalIgnoreCase)) return new(ElevationResult.Failed,null);
        var initialized=CoInitializeEx(IntPtr.Zero,0)>=0;
        try {
            var info=new ExecuteInfo { Size=(uint)Marshal.SizeOf<ExecuteInfo>(),Mask=0x40|0x100|0x400,Verb="runas",File=Path.GetFullPath(path),Directory=AppContext.BaseDirectory,Show=1 };
            var success=ShellExecuteExW(ref info); var error=Marshal.GetLastWin32Error();
            var handle=new SafeProcessHandle(info.Process,true);
            if(!success||handle.IsInvalid) { handle.Dispose(); return new(error==1223?ElevationResult.Cancelled:ElevationResult.Failed,null); }
            var pid=GetProcessId(handle);
            if(pid==0) { handle.Dispose(); return new(ElevationResult.Failed,null); }
            return new(null,new((int)pid,()=>WaitForSingleObject(handle,0)!=258,()=>ReadyWindow(pid),handle));
        } finally { if(initialized) CoUninitialize(); }
    }
    private static bool ReadyWindow(uint pid)
    {
        var found=false; var count=0;
        EnumWindows((window,_)=> {
            if(++count>4096) return false;
            GetWindowThreadProcessId(window,out var owner);
            if(owner==pid&&IsWindowVisible(window)&&GetPropW(window,ReadyProperty)==new IntPtr(1)) { found=true; return false; }
            return true;
        },IntPtr.Zero);
        return found;
    }
}

using System.Runtime.InteropServices;
using ProcSight.Core.Models;
namespace ProcSight.Windows.Tray;
public enum TrayCommand { Open=1,Settings=2,Exit=3,Cpu=10,Memory=11,Disk=12,Network=13,Gpu=14 }
public sealed class NotificationIcon : IDisposable
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct NotifyData {
        public uint Size; public IntPtr Window; public uint Id,Flags,Callback; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Tip;
        public uint State,Mask; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string Info;
        public uint Version; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string Title;
        public uint InfoFlags; public Guid Guid; public IntPtr Balloon;
    }
    private delegate IntPtr SubclassProc(IntPtr window,uint message,UIntPtr w,IntPtr l,UIntPtr id,UIntPtr data);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern bool Shell_NotifyIconW(uint action,ref NotifyData data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr window,SubclassProc proc,UIntPtr id,UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr window,SubclassProc proc,UIntPtr id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr window,uint message,UIntPtr w,IntPtr l);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterWindowMessageW(string message);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern bool AppendMenuW(IntPtr menu,uint flags,UIntPtr id,string? label);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(IntPtr menu,uint flags,int x,int y,IntPtr window,IntPtr parameters);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr window,uint message,UIntPtr w,IntPtr l);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X,Y; }
    private readonly IntPtr window; private readonly SubclassProc callback; private readonly uint recreated;
    private readonly NotificationRegistration registration; private readonly TrayIconRenderer renderer=new();
    private IconHandle? icon; private TraySample sample=new(TrayMetric.Cpu,null,"%",TraySampleState.WarmingUp); private bool disposed;
    public event Action<TrayCommand>? Command;
    public event Action? Minimized;
    public event Action? Unavailable;
    public bool Registered=>registration.Registered;
    public NotificationIcon(IntPtr window) {
        this.window=window; callback=Message; recreated=RegisterWindowMessageW("TaskbarCreated");
        if(!SetWindowSubclass(window,callback,new(0x5053),UIntPtr.Zero)) throw new InvalidOperationException("Tray callbacks unavailable.");
        registration=new(Shell);
        registration.Unavailable+=()=>Unavailable?.Invoke();
    }
    private bool Shell(uint action) {
        var tip=sample.Tooltip; var data=new NotifyData { Size=(uint)Marshal.SizeOf<NotifyData>(),Window=window,Id=1,Flags=1|2|4|0x80,Callback=0x8001,
            Icon=icon?.DangerousGetHandle()??IntPtr.Zero,Tip=tip[..Math.Min(127,tip.Length)],Info="",Title="",Version=4 };
        return Shell_NotifyIconW(action,ref data);
    }
    public bool Show() { if(disposed) return false; if(icon is null) Update(sample); return registration.Show(); }
    public void Hide()=>registration.Hide();
    public void Update(TraySample next) {
        if(disposed) return; sample=next;
        var size=(int)Math.Clamp(16*GetDpiForWindow(window)/96,16,64); var replacement=renderer.Render(next,size);
        if(replacement.IsInvalid) { replacement.Dispose(); return; }
        var old=icon; icon=replacement; registration.Update(); old?.Dispose();
    }
    private IntPtr Message(IntPtr hwnd,uint message,UIntPtr w,IntPtr l,UIntPtr id,UIntPtr data) {
        try {
            if(message==5&&w.ToUInt64()==1) Minimized?.Invoke();
            if(message==recreated) { registration.ExplorerRestarted(); if(!registration.Registered) Unavailable?.Invoke(); }
            if(message==0x8001) {
                var code=(uint)(l.ToInt64()&0xffff);
                if(code is 0x400 or 0x401) Command?.Invoke(TrayCommand.Open);
                if(code==0x7b) ShowMenu();
            }
        } catch { /* Shell/UI callbacks must never unwind through native code. */ }
        return DefSubclassProc(hwnd,message,w,l);
    }
    private void ShowMenu() {
        var menu=CreatePopupMenu(); if(menu==IntPtr.Zero) return;
        try {
            AppendMenuW(menu,0,new(1),"Open ProcSight"); AppendMenuW(menu,0,new(2),"Settings"); AppendMenuW(menu,0x800,UIntPtr.Zero,null);
            foreach(var metric in Enum.GetValues<TrayMetric>()) AppendMenuW(menu,metric==sample.Metric?8u:0u,new((uint)(10+(int)metric)),metric.ToString());
            AppendMenuW(menu,0x800,UIntPtr.Zero,null); AppendMenuW(menu,0,new(3),"Exit");
            GetCursorPos(out var point); SetForegroundWindow(window);
            var command=TrackPopupMenuEx(menu,0x100|0x2,point.X,point.Y,window,IntPtr.Zero);
            PostMessageW(window,0,UIntPtr.Zero,IntPtr.Zero);
            if(Enum.IsDefined(typeof(TrayCommand),(int)command)) Command?.Invoke((TrayCommand)command);
        } finally { DestroyMenu(menu); }
    }
    public void Dispose() { if(disposed) return; disposed=true; registration.Dispose(); RemoveWindowSubclass(window,callback,new(0x5053)); icon?.Dispose(); icon=null; }
}

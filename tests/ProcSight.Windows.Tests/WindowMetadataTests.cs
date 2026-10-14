using System.Runtime.InteropServices;
using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class WindowMetadataTests
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateWindowExW(uint style,string className,string title,uint windowStyle,
        int x,int y,int width,int height,IntPtr owner,IntPtr menu,IntPtr instance,IntPtr parameter);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr window);
    [Fact] public void ReaderCapturesOnlyRequestedProcessWindowsAndRejectsChangedLifetime()
    {
        var window=CreateWindowExW(0,"STATIC","SYNTHETIC_WINDOW",0x90000000,-32000,-32000,10,10,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero,window);
        try {
            var key=new ProcessKey(Environment.ProcessId,null,Guid.NewGuid());
            var result=WindowMetadataReader.Read([key]);
            Assert.Contains(result[key.Pid],w=>w.Title=="SYNTHETIC_WINDOW"&&w.ClassName=="Static");
            Assert.Empty(WindowMetadataReader.Read([]));
            Assert.False(WindowMetadataReader.Read([new(Environment.ProcessId,DateTimeOffset.UtcNow.AddYears(-20),null)]).ContainsKey(key.Pid));
        } finally { DestroyWindow(window); }
    }
    [Fact] public async Task UnresponsiveOwnWindowDoesNotBlockCollection()
    {
        using var ready=new ManualResetEventSlim(); using var release=new ManualResetEventSlim();
        var window=IntPtr.Zero;
        var thread=new Thread(()=> {
            window=CreateWindowExW(0,"STATIC","SYNTHETIC_BLOCKED_WINDOW",0x90000000,-32000,-32000,10,10,
                IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
            ready.Set(); release.Wait(); if(window!=IntPtr.Zero) DestroyWindow(window);
        });
        thread.Start();
        try {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(5))); Assert.NotEqual(IntPtr.Zero,window);
            var result=await Task.Run(()=>WindowMetadataReader.Read([new(Environment.ProcessId,null,Guid.NewGuid())]))
                .WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Contains(result[Environment.ProcessId],w=>w.ClassName=="Static");
        } finally { release.Set(); Assert.True(thread.Join(TimeSpan.FromSeconds(5))); }
    }
}

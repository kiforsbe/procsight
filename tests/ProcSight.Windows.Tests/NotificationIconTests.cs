using ProcSight.Windows.Tray;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class NotificationIconTests
{
    [Fact] public void FailedModifyAndReaddSignalWindowRecovery()
    {
        var success=true; var lost=0; using var state=new NotificationRegistration(_=>success);
        state.Unavailable+=()=>lost++; state.Show(); success=false; state.Update(); state.Update();
        Assert.Equal(2,lost); Assert.False(state.Registered);
    }
    [Fact] public void DynamicIconsOwnAndReleaseTheirNativeHandles()
    {
        var renderer=new TrayIconRenderer();
        foreach(var size in new[]{16,20,24,32,48,64}) {
            using var icon=renderer.Render(new(TrayMetric.Cpu,50,"%",TraySampleState.Available),size);
            Assert.False(icon.IsInvalid); Assert.Equal(size*size*4,renderer.Pixels(new(TrayMetric.Network,null,"B/s",TraySampleState.Unavailable),size).Length);
        }
    }
    [Fact] public void ExplorerRestartReaddsOneIconAndDisposeDeletesOnce()
    {
        var calls=new List<uint>();
        var registration=new NotificationRegistration(action=> { calls.Add(action); return true; });
        registration.Show(); registration.Show(); registration.ExplorerRestarted(); registration.Update(); registration.Dispose(); registration.Dispose();
        Assert.Equal(new uint[]{0,4,0,4,1,2},calls);
    }
    [Fact] public void FailedAddRemainsRecoverableAndHiddenUpdatesDoNotReregister()
    {
        var success=false; var calls=new List<uint>();
        using var registration=new NotificationRegistration(action=> { calls.Add(action); return success; });
        Assert.False(registration.Show()); success=true; Assert.True(registration.Show());
        registration.Hide(); registration.Update(); Assert.False(registration.Registered);
        Assert.Equal(new uint[]{0,0,4,2},calls);
    }
}

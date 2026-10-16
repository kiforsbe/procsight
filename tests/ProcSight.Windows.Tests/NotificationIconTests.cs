using ProcSight.Windows.Tray;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class NotificationIconTests
{
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

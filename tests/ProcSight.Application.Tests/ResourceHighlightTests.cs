using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Application.Tests;
public class ResourceHighlightTests
{
    [Fact] public void RoofIsDirectOpacityAndBrighterByDefault() {
        var state=new ResourceHighlightState(1); state.Observe(10,DateTimeOffset.UnixEpoch,0);
        var settings=new ResourceShadingOptions();
        Assert.Equal(128,state.Opacity(ResourceShadingMode.RecentChanges,0,settings,100));
        Assert.Equal(204,state.Opacity(ResourceShadingMode.RecentChanges,0,settings with { MaximumOpacityPercent=80 },100));
        Assert.Equal(13,state.Opacity(ResourceShadingMode.RecentChanges,10,settings with { MaximumOpacityPercent=80 },100));
    }
    [Fact] public void ConfigurableFloorDefaultsBelowPreviousTintAndNeverShadesMissingOrOff() {
        var state=new ResourceHighlightState(1); var options=new ResourceShadingOptions();
        state.Observe(0,DateTimeOffset.UnixEpoch,0);
        Assert.Equal(13,state.Opacity(ResourceShadingMode.RecentChanges,10,options,100));
        Assert.Equal(0,state.Opacity(ResourceShadingMode.RecentChanges,10,options with { MinimumOpacityPercent=0 },100));
        Assert.Equal(18,state.Opacity(ResourceShadingMode.RecentChanges,10,options with { MinimumOpacityPercent=7 },100));
        Assert.Equal(0,state.Opacity(ResourceShadingMode.Off,10,options,100));
        Assert.Equal(0,state.Opacity(ResourceShadingMode.RecentChanges,10,options,100,true));
        state.Reset(); Assert.Equal(0,state.Opacity(ResourceShadingMode.RecentChanges,10,options,100));
    }
    [Fact] public void UnchangedPollsDoNotRestartRecencyAndDisplayedChangesDo() {
        var state=new ResourceHighlightState(1); var at=DateTimeOffset.UnixEpoch;
        state.Observe(10,at,0);
        Assert.Equal(1,state.Level(ResourceShadingMode.RecentChanges,0,5,100));
        state.Observe(10.01,at.AddSeconds(2),2);
        Assert.InRange(state.Level(ResourceShadingMode.RecentChanges,2,5,100),.59,.61);
        Assert.Equal(0,state.Level(ResourceShadingMode.RecentChanges,5,5,100));
        state.Observe(12,at.AddSeconds(6),6);
        Assert.Equal(1,state.Level(ResourceShadingMode.RecentChanges,6,5,100));
    }
    [Fact] public void MissingValuesAndRepeatedSnapshotsCannotManufactureActivity() {
        var state=new ResourceHighlightState(.1); var at=DateTimeOffset.UnixEpoch;
        state.Observe(2,at,0); state.Observe(3,at,1);
        Assert.Equal(.6,state.Level(ResourceShadingMode.RecentChanges,2,5,10),8);
        state.Observe(null,at.AddSeconds(3),3);
        foreach(var mode in Enum.GetValues<ResourceShadingMode>()) Assert.Equal(0,state.Level(mode,3,5,10));
    }
    [Fact] public void ActivitySpikesAndRelativeConsumersHaveDifferentMeanings() {
        var state=new ResourceHighlightState(1); var at=DateTimeOffset.UnixEpoch;
        state.Observe(10,at,0); state.Observe(10,at.AddSeconds(1),1);
        Assert.Equal(0,state.Level(ResourceShadingMode.RecentSpikes,1,5,100));
        state.Observe(40,at.AddSeconds(2),2);
        Assert.True(state.Level(ResourceShadingMode.RecentSpikes,2,5,100)>.5);
        Assert.Equal(.4,state.Level(ResourceShadingMode.TopConsumers,2,5,100),8);
        Assert.Equal(1,state.Level(ResourceShadingMode.CurrentActivity,2,5,100));
        Assert.Equal(0,state.Level(ResourceShadingMode.Off,2,5,100));
        state.Observe(0,at.AddSeconds(3),3);
        Assert.Equal(0,state.Level(ResourceShadingMode.CurrentActivity,3,5,100));
    }
}

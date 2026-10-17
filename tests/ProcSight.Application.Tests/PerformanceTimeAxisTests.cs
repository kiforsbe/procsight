using ProcSight.Application.Presentation;
using Xunit;
namespace ProcSight.Application.Tests;
public class PerformanceTimeAxisTests
{
    [Fact] public void LogarithmicTimeExpandsRecentHistoryWithoutChangingEndpoints() {
        Assert.Equal(1,PerformanceTimeAxis.Position(0,60,true));
        Assert.Equal(0,PerformanceTimeAxis.Position(60,60,true));
        Assert.True(1-PerformanceTimeAxis.Position(1,60,true)>1-PerformanceTimeAxis.Position(1,60,false));
        var positions=new[]{60d,30,10,3,1,0}.Select(age=>PerformanceTimeAxis.Position(age,60,true)).ToArray();
        Assert.True(positions.Zip(positions.Skip(1)).All(pair=>pair.First<pair.Second));
    }
    [Fact] public void InvalidTimesAreNotPlottedAndLinearScaleStaysUniform() {
        Assert.True(double.IsNaN(PerformanceTimeAxis.Position(double.NaN,60,true)));
        Assert.Equal(.5,PerformanceTimeAxis.Position(30,60,false));
        Assert.Equal(1,PerformanceTimeAxis.Position(-1,60,true));
        Assert.Equal(0,PerformanceTimeAxis.Position(100,60,true));
    }
}

using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Application.Tests;
public class ResourceRatePresentationTests
{
    [Fact] public void GpuGroupsUseBusiestMemberAndPreserveMissingCoverage() {
        Assert.Equal(45,ResourceRatePresentation.GpuMaximum([Observation<double>.Available(30),Observation<double>.Available(45)]));
        Assert.StartsWith("≥",ResourceRatePresentation.GpuText([Observation<double>.Available(30),Observation<double>.Missing("Denied")]));
        Assert.Equal("…",ResourceRatePresentation.GpuText([Observation<double>.Pending()]));
        Assert.Equal("—",ResourceRatePresentation.GpuText([Observation<double>.Missing("Denied")]));
        Assert.Contains(TableColumns.Available(false),c=>c.Id=="Gpu");
        Assert.DoesNotContain(TableColumns.Available(true),c=>c.Id=="Gpu");
    }
    [Fact] public void MissingAndWarmUpAreNotDisplayedAsZero() {
        Assert.Equal("—",ResourceRatePresentation.Text([Observation<double>.Missing("Denied")]));
        Assert.Equal("…",ResourceRatePresentation.Text([Observation<double>.Pending()]));
        Assert.Contains("MB/s",ResourceRatePresentation.Text([Observation<double>.Available(0)]));
        Assert.Equal("—",ResourceRatePresentation.Text([Observation<double>.Available(double.NaN)]));
    }
    [Fact] public void IncompleteGroupsHaveLowerBoundAndExplainMissingCoverage() {
        Observation<double>[] values=[Observation<double>.Available(1000000),Observation<double>.Missing("Permission required")];
        Assert.StartsWith("≥",ResourceRatePresentation.Text(values));
        Assert.Contains("Permission required",ResourceRatePresentation.Details(values));
        Assert.EndsWith("Mbps",ResourceRatePresentation.Text(values,true));
    }
    [Fact] public void SubsystemsSelectTheirOwnSystemMetric() {
        var point=new PerformanceSnapshot(DateTimeOffset.UtcNow,Observation<double>.Available(1),Observation<double>.Available(2),
            Observation<double>.Available(3),Observation<double>.Available(4),Observation<double>.Available(5),Observation<double>.Available(6),100,50,8);
        Assert.Equal(3,point.Metric(PerformanceSubsystem.Disks).Value);
        Assert.Equal(5,point.Metric(PerformanceSubsystem.Network).Value);
        Assert.Equal(6,point.Metric(PerformanceSubsystem.Gpu).Value);
    }
}

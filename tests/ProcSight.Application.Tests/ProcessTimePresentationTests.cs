using System.Globalization;
using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public class ProcessTimePresentationTests
{
    [Fact] public void StartSortingUsesInstantsAcrossOffsetsAndKeepsUnknownLast() {
        var first=Samples.Process(1) with { Key=new(1,DateTimeOffset.Parse("2026-10-17T10:00:00+02:00"),null) };
        var second=Samples.Process(2) with { Key=new(2,DateTimeOffset.Parse("2026-10-17T09:00:00Z"),null) };
        var unknown=Samples.Process(3) with { Key=new(3,null,Guid.NewGuid()) };
        var snapshot=Samples.Snapshot(first,second,unknown); var entities=new IdentityResolver().Resolve(snapshot,[]);
        Assert.Equal(new[]{1,2,3},ProcessListProjection.Build(snapshot,entities,[],new(Sort:ProcessSort.Started)).Rows.Select(r=>r.Entity.Process!.Key.Pid));
        Assert.Equal(new[]{2,1,3},ProcessListProjection.Build(snapshot,entities,[],new(Sort:ProcessSort.Started,Descending:true)).Rows.Select(r=>r.Entity.Process!.Key.Pid));
    }
    [Fact] public void FriendlyDisplayUsesLocalCalendarWhileExportRetainsFullTimestamp() {
        var zone=TimeZoneInfo.CreateCustomTimeZone("Synthetic +02",TimeSpan.FromHours(2),"Synthetic +02","Synthetic +02");
        var now=DateTimeOffset.Parse("2026-10-17T08:00:00Z"); var start=DateTimeOffset.Parse("2026-10-16T22:23:45.1234567Z");
        Assert.Equal("Today, 00:23",ProcessTimePresentation.Started(start,now,zone,CultureInfo.GetCultureInfo("sv-SE")));
        Assert.StartsWith("Yesterday, ",ProcessTimePresentation.Started(start.AddDays(-1),now,zone,CultureInfo.GetCultureInfo("sv-SE")));
        Assert.Equal("—",ProcessTimePresentation.Started(null,now,zone,CultureInfo.InvariantCulture));
        var process=Samples.Process(1) with { Key=new(1,start,null) };
        var entity=new IdentityResolver().Resolve(Samples.Snapshot(process),[]).Single();
        Assert.Equal(start.ToString("O"),TableValues.Read(entity)["Started"]);
        Assert.DoesNotContain("Today",TableValues.Read(entity)["Started"]!);
    }
}

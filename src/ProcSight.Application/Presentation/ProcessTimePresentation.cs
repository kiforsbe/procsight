using System.Globalization;
namespace ProcSight.Application.Presentation;
public static class ProcessTimePresentation
{
    public static string Started(DateTimeOffset? value,DateTimeOffset now,TimeZoneInfo? zone=null,CultureInfo? culture=null) {
        if(value is not { } started) return "—";
        zone??=TimeZoneInfo.Local; culture??=CultureInfo.CurrentCulture;
        var local=TimeZoneInfo.ConvertTime(started,zone); var today=TimeZoneInfo.ConvertTime(now,zone).Date;
        var time=local.ToString("t",culture);
        return local.Date==today?"Today, "+time:local.Date==today.AddDays(-1)?"Yesterday, "+time:
            local.ToString("d MMM yyyy",culture)+", "+time;
    }
}

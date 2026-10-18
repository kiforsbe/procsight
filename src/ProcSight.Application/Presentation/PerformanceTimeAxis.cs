namespace ProcSight.Application.Presentation;
public static class PerformanceTimeAxis
{
    public static double Position(double ageSeconds,double spanSeconds,bool logarithmic) {
        if(!double.IsFinite(ageSeconds)||!double.IsFinite(spanSeconds)||spanSeconds<=0) return double.NaN;
        var age=Math.Clamp(ageSeconds,0,spanSeconds);
        return 1-(logarithmic?Math.Log(1+age)/Math.Log(1+spanSeconds):age/spanSeconds);
    }
    public static double[] Ticks(double spanSeconds,bool logarithmic)=>logarithmic?
        [spanSeconds,spanSeconds/2,spanSeconds/6,spanSeconds/20,spanSeconds/60,0]:
        [spanSeconds,spanSeconds*.75,spanSeconds*.5,spanSeconds*.25,0];
}

using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
/// <summary>Transient, bounded cell history. All animation time is monotonic; sample identity is local only.</summary>
public sealed class ResourceHighlightState(double quantum)
{
    private double? value; private double rounded,baseline,peak,previousAt;
    private double changed=double.NegativeInfinity,spiked=double.NegativeInfinity,spike;
    private DateTimeOffset? sampled;
    public void Observe(double? next,DateTimeOffset sampleAt,double now) {
        if(next is null||next<0||!double.IsFinite(next.Value)) { Reset(); return; }
        if(sampled==sampleAt) return;
        if(sampled>sampleAt) Reset();
        var amount=next.Value; var display=Math.Round(amount/quantum);
        if(value is null) { baseline=peak=amount; changed=now; }
        else {
            if(display!=rounded) changed=now;
            if(amount>value&&amount-baseline>=quantum&&amount>Math.Max(quantum,baseline*1.5)) {
                spiked=now; spike=Math.Clamp((amount-baseline)/Math.Max(quantum,baseline),0,1);
            }
            var elapsed=Math.Max(0,now-previousAt);
            baseline+=(amount-baseline)*(1-Math.Exp(-elapsed/10));
            peak=Math.Max(amount,peak*Math.Exp(-elapsed/30));
        }
        value=amount; rounded=display; previousAt=now; sampled=sampleAt;
    }
    public void Reset() { value=null; sampled=null; changed=spiked=double.NegativeInfinity; baseline=peak=spike=0; }
    public byte Opacity(ResourceShadingMode mode,double now,ResourceShadingOptions settings,double visibleMaximum,bool highContrast=false) {
        if(value is null||mode==ResourceShadingMode.Off||highContrast) return 0;
        var floor=255*settings.MinimumOpacityPercent/100d;
        var ceiling=255*settings.MaximumOpacityPercent/100d;
        return (byte)Math.Clamp(Math.Round(floor+(ceiling-floor)*Level(mode,now,settings.FadeSeconds,visibleMaximum)),0,255);
    }
    public double Level(ResourceShadingMode mode,double now,double fadeSeconds,double visibleMaximum) {
        if(value is not { } current||!double.IsFinite(now)) return 0;
        double Fade(double at)=>Math.Clamp(1-Math.Max(0,now-at)/Math.Max(1,fadeSeconds),0,1);
        return mode switch {
            ResourceShadingMode.RecentChanges=>Fade(changed),
            ResourceShadingMode.CurrentActivity=>peak>0?Math.Clamp(current/peak,0,1):0,
            ResourceShadingMode.RecentSpikes=>spike*Fade(spiked),
            ResourceShadingMode.TopConsumers=>visibleMaximum>0?Math.Clamp(current/visibleMaximum,0,1):0,
            _=>0 };
    }
}

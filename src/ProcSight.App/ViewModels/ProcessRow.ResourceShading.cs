using Microsoft.UI.Xaml.Media;
using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
namespace ProcSight.App.ViewModels;
public sealed partial class ProcessRow
{
    private readonly ResourceHighlightState diskHighlight=new(100000),networkHighlight=new(12500),gpuHighlight=new(.1);
    private ProcessKey[] highlightScope=[];
    private static readonly Brush ClearHighlight=new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    public Brush DiskBrush { get; private set; }=ClearHighlight;
    public Brush NetworkBrush { get; private set; }=ClearHighlight;
    public Brush GpuBrush { get; private set; }=ClearHighlight;
    public (double Disk,double Network,double Gpu) HighlightValues { get; private set; }
    public void ObserveHighlights(double now) {
        var members=Projection.Members.Where(e=>e.Process is not null).Select(e=>e.Process!).ToArray();
        var scope=members.Select(p=>p.Key).ToArray();
        if(!highlightScope.SequenceEqual(scope)) { diskHighlight.Reset(); networkHighlight.Reset(); gpuHighlight.Reset(); highlightScope=scope; }
        double? Sum(IEnumerable<Observation<double>> source) {
            var valid=source.Where(v=>v.State==ObservationState.Available&&v.Value is >=0&&double.IsFinite(v.Value.Value)).ToArray();
            return valid.Length==0?null:valid.Sum(v=>v.Value!.Value);
        }
        var disk=Sum(members.Select(p=>p.Resources.DiskBytesPerSecond));
        var network=Sum(members.Select(p=>p.Resources.NetworkBytesPerSecond));
        var gpu=ResourceRatePresentation.GpuMaximum(members.Select(p=>p.Resources.GpuPercent??Observation<double>.Missing("Unavailable")));
        var at=members.Select(p=>p.Resources.SampledAt).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
        diskHighlight.Observe(disk,at,now); networkHighlight.Observe(network,at,now); gpuHighlight.Observe(gpu,at,now);
        HighlightValues=(disk??0,network??0,gpu??0);
    }
    public void RefreshHighlights(ResourceShadingOptions settings,double now,(double Disk,double Network,double Gpu) maximum,
        Func<byte,Brush> palette,bool highContrast) {
        ConfigureUsageShading(settings.MinimumOpacityPercent,highContrast);
        Brush Pick(ResourceHighlightState state,ResourceShadingMode mode,double max)=>palette(state.Opacity(mode,now,settings,max,highContrast));
        var disk=Pick(diskHighlight,settings.Disk,maximum.Disk); var network=Pick(networkHighlight,settings.Network,maximum.Network);
        var gpu=Pick(gpuHighlight,settings.Gpu,maximum.Gpu);
        if(!ReferenceEquals(DiskBrush,disk)) { DiskBrush=disk; Changed(nameof(DiskBrush)); }
        if(!ReferenceEquals(NetworkBrush,network)) { NetworkBrush=network; Changed(nameof(NetworkBrush)); }
        if(!ReferenceEquals(GpuBrush,gpu)) { GpuBrush=gpu; Changed(nameof(GpuBrush)); }
    }
}

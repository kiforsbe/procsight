using ProcSight.Core.Models;
using ProcSight.Windows.Storage;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class TrayPreferencesTests
{
    [Fact] public void ShadingAndAutomaticSortingRoundTripWithCompatibleDefaults() {
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
        try {
            var store=new PreferencesStore(Path.Combine(folder,"settings.json"));
            Assert.False(new PresentationPreferences().AutomaticProcessSorting);
            Assert.Equal(ResourceShadingMode.RecentChanges,new ResourceShadingOptions().Disk);
            var options=new ResourceShadingOptions(ResourceShadingMode.TopConsumers,ResourceShadingMode.Off,ResourceShadingMode.RecentSpikes,10,75);
            store.Save(new(),new(ResourceShading:options,AutomaticProcessSorting:true,PerformanceLogTimeAxis:true));
            var restored=store.Load().Presentation;
            Assert.Equal(options,restored.ResourceShading); Assert.True(restored.AutomaticProcessSorting);
            Assert.True(restored.PerformanceLogTimeAxis);
            Assert.Equal(5,restored.ResourceShading!.MinimumOpacityPercent);
            Assert.Throws<ArgumentException>(()=>store.Save(new(),new(ResourceShading:options with { MinimumOpacityPercent=16 })));
            Assert.Throws<ArgumentException>(()=>store.Save(new(),new(ResourceShading:options with { FadeSeconds=0 })));
            Assert.Throws<ArgumentException>(()=>store.Save(new(),new(ResourceShading:options with { Gpu=(ResourceShadingMode)999 })));
            File.WriteAllText(Path.Combine(folder,"settings.json"),"""{"Policy":{},"Presentation":{"Theme":"Dark"}}""");
            restored=store.Load().Presentation;
            Assert.Null(restored.ResourceShading); Assert.False(restored.AutomaticProcessSorting);
            Assert.False(restored.PerformanceLogTimeAxis);
        } finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
    [Fact] public void DefaultsAreExplicitAndMetricRoundTrips()
    {
        var defaults=new PresentationPreferences(); Assert.False(defaults.ShowTrayIcon); Assert.False(defaults.MinimizeToTray); Assert.Equal(TrayMetric.Cpu,defaults.TrayMetric);
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
        try {
            var store=new PreferencesStore(Path.Combine(folder,"settings.json"));
            store.Save(new(),new(ShowTrayIcon:true,TrayMetric:TrayMetric.Gpu));
            Assert.Equal(TrayMetric.Gpu,store.Load().Presentation.TrayMetric);
            Assert.Throws<ArgumentException>(()=>store.Save(new(),new(TrayMetric:(TrayMetric)999)));
        } finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
}

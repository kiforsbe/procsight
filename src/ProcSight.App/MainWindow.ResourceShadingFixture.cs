#if DEBUG
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private async Task CheckResourceShadingAsync(string mode) {
        if(mode=="live-sort") {
            sort=ProcessSort.Disk; sortDescending=false; RefreshRows(reorder:true);
            var target=rows.Single(r=>r.Entity.Process?.Key.Pid==1001); var index=rows.IndexOf(target);
            table!.SelectedItems.Add(target);
            void Change(double value) {
                lastSnapshot=lastSnapshot with { Processes=lastSnapshot.Processes.Select(p=>p.Key.Pid==1001?p with {
                    Resources=p.Resources with { DiskBytesPerSecond=Observation<double>.Available(value),SampledAt=p.Resources.SampledAt.AddSeconds(1) } }:p).ToArray() };
                entities=new IdentityResolver().Resolve(lastSnapshot,[]); RefreshRows();
            }
            Change(999000000);
            if(rows.IndexOf(target)!=index) throw new InvalidOperationException("Manual sorting moved a changed process.");
            var resets=0;
            var nativeResets=0;
            void NativeChanged(global::Windows.Foundation.Collections.IObservableVector<object> sender,global::Windows.Foundation.Collections.IVectorChangedEventArgs args) {
                if(args.CollectionChange==global::Windows.Foundation.Collections.CollectionChange.Reset) nativeResets++;
            }
            table.Items.VectorChanged+=NativeChanged;
            System.Collections.Specialized.NotifyCollectionChangedEventHandler checkReset=(_,e)=> { if(e.Action==System.Collections.Specialized.NotifyCollectionChangedAction.Reset) resets++; };
            rows.CollectionChanged+=checkReset;
            presentation=presentation with { AutomaticProcessSorting=true }; RefreshRows();
            for(var iteration=0;iteration<6;iteration++) {
                Change(iteration%2==0?0:999000000);
                WindowRoot.UpdateLayout(); await Task.Delay(16);
                if(!table.SelectedItems.Contains(target)) throw new InvalidOperationException("Automatic native relocation lost process selection.");
            }
            table.Items.VectorChanged-=NativeChanged;
            rows.CollectionChanged-=checkReset;
            if(resets>0) throw new InvalidOperationException("Automatic sorting reset the large live list instead of retaining its containers.");
            if(nativeResets>0) throw new InvalidOperationException($"Automatic sorting produced {nativeResets} native vector resets from managed moves.");
            if(rows.IndexOf(target)<=index||!table.SelectedItems.Contains(target)) throw new InvalidOperationException("Automatic sorting did not move the measured row or lost selection.");
            var automaticIndex=rows.IndexOf(target);
            presentation=presentation with { AutomaticProcessSorting=false }; Change(0);
            if(rows.IndexOf(target)!=automaticIndex) throw new InvalidOperationException("Disabling automatic sorting did not freeze row order.");
            RefreshRows(reorder:true);
            if(rows.IndexOf(target)!=0) throw new InvalidOperationException("Explicit sorting did not apply the current value.");
            return;
        }
        await Task.Delay(100); RefreshResourceShading(all:true); WindowRoot.UpdateLayout();
        var measured=rows.Single(r=>r.Entity.Process?.Key.Pid==1001);
        if(new[]{measured.DiskBrush,measured.NetworkBrush,measured.GpuBrush}.OfType<SolidColorBrush>().Any(b=>b.Color.A==0))
            throw new InvalidOperationException("Recent resource changes have no shading.");
        foreach(var id in new[]{"Disk","Network","Gpu"}) {
            BeginColumnMotion(id); RestoreDraggedColumn();
            foreach(var grid in RealizedColumnRows()) {
                var cell=grid.Children.OfType<Border>().Single(c=>c.Tag as string==id);
                if(cell.GetBindingExpression(Border.BackgroundProperty)?.ParentBinding.Path.Path!=id+"Brush")
                    throw new InvalidOperationException("Column dragging lost a resource shading binding.");
            }
        }
        presentation=presentation with { ResourceShading=new(ResourceShadingMode.Off,ResourceShadingMode.Off,ResourceShadingMode.Off) };
        RefreshResourceShading(all:true);
        if(new[]{measured.DiskBrush,measured.NetworkBrush,measured.GpuBrush}.OfType<SolidColorBrush>().Any(b=>b.Color.A!=0))
            throw new InvalidOperationException("Off mode left a resource cell shaded.");
        presentation=presentation with { ResourceShading=new() }; RefreshResourceShading(all:true);
        if(mode=="resource-shading-settings") {
            resourceShadeTimer?.Stop(); PageContent.Children.Clear();
            var settings=(Expander)BuildResourceShadingSettings(); settings.IsExpanded=true;
            PageContent.Children.Add(new ScrollViewer { Content=settings,Margin=new Microsoft.UI.Xaml.Thickness(24) });
            await Task.Delay(100); settings.IsExpanded=true; await Task.Delay(300);
            if(!settings.IsExpanded||FixtureControls<ComboBox>(settings).Count()!=5)
                throw new InvalidOperationException("Resource shading controls were not shown in the expanded native settings section.");
        }
    }
}
#endif

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProcSight.App.ViewModels;
using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using System.Text;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private bool exporting;
    private async Task ExportInventoryAsync()
    {
        if(exporting||table is null) return; exporting=true;
        try {
            // Immutable collector records and analysis snapshots are captured before showing either dialog.
            var services=page=="Services"; var inventory=entities.Where(e=>services?e.Service is not null:e.Process is not null).ToArray();
            var findings=analysis.ToDictionary(p=>p.Key,p=>p.Value);
            var selected=InventoryExport.ExpandSelection(table.SelectedItems.OfType<ProcessRow>().Select(r=>r.Projection));
            var filtered=InventoryExport.ExpandSelection(ProcessListProjection.Build(lastSnapshot,entities,findings.Values.ToArray(),new(search,false,services,sort,
                ClassificationFilter:classificationFilter,Descending:sortDescending)).Rows);
            var visibleColumns=tableColumnLayout.Select(c=>c.Id).ToArray();
            var scope=new ComboBox { Header="Entries",ItemsSource=new[]{"Selected entries","All filtered entries","Full inventory"},SelectedIndex=selected.Length>0?0:1,HorizontalAlignment=HorizontalAlignment.Stretch };
            var fields=new ComboBox { Header="Fields",ItemsSource=new[]{"Visible columns","All available metadata columns"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
            var format=new ComboBox { Header="Format",ItemsSource=new[]{"CSV","JSON","TSV"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
            var commands=new CheckBox { Content="Include command lines and command-derived names" };
            var identifying=new CheckBox { Content="Include paths and ownership" };
            var windows=new CheckBox { Content="Include window titles and names derived from them" };
            var count=RuleText("","CaptionTextBlockStyle",true);
            void Count()=>count.Text=$"{(scope.SelectedIndex==0?selected.Length:scope.SelectedIndex==1?filtered.Length:inventory.Length)} individual {(services?"services":"processes")} in this snapshot. Groups export their members once.";
            scope.SelectionChanged+=(_,_)=>Count(); Count();
            var panel=new StackPanel { Spacing=12,MinWidth=300 };
            foreach(var control in new UIElement[]{scope,count,fields,format,commands,identifying,windows,RuleText("Exports are local files and can contain private information. These choices are independent of AI sharing settings. CSV/TSV text is protected against spreadsheet formulas. Memory is in bytes; CPU is percent; disk/network are bytes per second.","CaptionTextBlockStyle",true)}) panel.Children.Add(control);
            var dialog=new ContentDialog { Title="Export inventory",RequestedTheme=WindowRoot.ActualTheme,Content=new ScrollViewer { Content=panel,MaxHeight=540,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled },PrimaryButtonText="Choose file…",CloseButtonText="Cancel",XamlRoot=PageContent.XamlRoot };
            tableConfigurationDialog=dialog;
            ContentDialogResult result;
            try { result=await dialog.ShowAsync(); } finally { tableConfigurationDialog=null; }
            if(result!=ContentDialogResult.Primary) return;
            var chosen=scope.SelectedIndex==0?selected:scope.SelectedIndex==1?filtered:inventory;
            if(chosen.Length==0) { await MessageAsync("Nothing to export","Select entries or choose a nonempty inventory scope."); return; }
            var allowed=TableColumns.ExportColumns(services,commands.IsChecked==true,identifying.IsChecked==true,windows.IsChecked==true).Select(c=>c.Id).ToHashSet();
            var columns=(fields.SelectedIndex==0?visibleColumns:TableColumns.Available(services).Select(c=>c.Id)).Where(allowed.Contains).ToArray();
            var records=chosen.Select(e=>TableValues.Read(e,findings.GetValueOrDefault(e.Identity),commands.IsChecked==true,identifying.IsChecked==true,windows.IsChecked==true)).ToArray();
            var extension=format.SelectedIndex==1?".json":format.SelectedIndex==2?".tsv":".csv";
            var picker=new Microsoft.Windows.Storage.Pickers.FileSavePicker(AppWindow.Id) { SuggestedFileName="ProcSight-"+(services?"services":"processes")+extension,DefaultFileExtension=extension };
            picker.FileTypeChoices.Add(format.SelectedItem.ToString()!,new[]{extension});
            var file=await picker.PickSaveFileAsync(); if(file is null) return;
            var json=format.SelectedIndex==1; var separator=format.SelectedIndex==2?'\t':',';
            await Task.Run(async()=> {
                var content=json?InventoryExport.Json(columns,records):InventoryExport.Delimited(columns,records,separator);
                var temporary=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file.Path)!,".procsight-export-"+Guid.NewGuid().ToString("N")+".tmp");
                try {
                    await System.IO.File.WriteAllTextAsync(temporary,content,new UTF8Encoding(!json),lifetime.Token);
                    lifetime.Token.ThrowIfCancellationRequested();
                    System.IO.File.Move(temporary,file.Path,true);
                } finally { if(System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
            },lifetime.Token);
            if(status is not null) status.Text=$"Exported {chosen.Length} {(services?"services":"processes")} · {format.SelectedItem}";
        } catch(OperationCanceledException) { }
        catch { await MessageAsync("Export failed","The export file could not be written. Check the destination and available permissions, then try again."); }
        finally { exporting=false; }
    }
}

#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ProcSight.Application.Presentation;
using ProcSight.App.ViewModels;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private static IEnumerable<T> FixtureControls<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T match) yield return match;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in FixtureControls<T>(VisualTreeHelper.GetChild(root,i))) yield return child;
    }
    private static void FixtureInvoke(FrameworkElement control)
    {
        var peer=Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(control);
        ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
    }
    private async Task CheckTableCustomizationAsync(string mode,string output)
    {
        if(mode=="columns-services") { page="Services"; RenderPage(); await Task.Delay(200); }
        table!.SelectedItem=rows.First(r=>!r.IsGroup); var primary=(ProcessRow)table.SelectedItem;
        if(mode is "columns" or "columns-services") {
            var resizeHandle=FixtureControls<ColumnResizeThumb>(PageContent).First();
            if(FixtureControls<Border>(resizeHandle).Any()||FixtureControls<Microsoft.UI.Xaml.Shapes.Shape>(resizeHandle).Any())
                throw new InvalidOperationException("Resize hit area renders extra grip chrome.");
            if(resizeHandle.Width<20||resizeHandle.Margin.Right!=-resizeHandle.Width/2||Canvas.GetZIndex(resizeHandle)<1)
                throw new InvalidOperationException("Resize target does not span both sides of the divider.");
            var movingHeader=tableHeader!.Children.OfType<Border>().First(b=>b.Tag as string!="Name");
            var movingId=(string)movingHeader.Tag;
            PositionDraggedHeader(movingHeader,movingId,380,10);
            var firstOffset=((TranslateTransform)movingHeader.RenderTransform).X;
            PositionDraggedHeader(movingHeader,movingId,387,10);
            if(Math.Abs(((TranslateTransform)movingHeader.RenderTransform).X-firstOffset-7)>0.01)
                throw new InvalidOperationException("Dragged header snaps instead of following pointer continuously.");
            foreach(var row in rows) if(table.ContainerFromItem(row) is ListViewItem { ContentTemplateRoot:Grid grid }) {
                var cell=grid.Children.OfType<FrameworkElement>().Single(c=>c.Tag as string==movingId);
                if(cell.RenderTransform is not TranslateTransform translation||Math.Abs(translation.X-((TranslateTransform)movingHeader.RenderTransform).X)>.01)
                    throw new InvalidOperationException("Dragged column body does not follow the header.");
            }
            RestoreDraggedColumn();
            if(tableHeader.Children.OfType<Border>().FirstOrDefault(b=>b.Tag as string=="Cpu") is { } resourceHeader) {
                PositionDraggedHeader(resourceHeader,"Cpu",600,10); RestoreDraggedColumn();
                foreach(var row in rows) if(table.ContainerFromItem(row) is ListViewItem { ContentTemplateRoot:Grid grid }) {
                    var cell=grid.Children.OfType<Border>().Single(c=>c.Tag as string=="Cpu");
                    if(cell.GetBindingExpression(Border.BackgroundProperty)?.ParentBinding.Path.Path!="CpuBrush")
                        throw new InvalidOperationException("Drag cleanup lost the resource heat binding.");
                }
            }
            if(mode=="columns") {
                var template=table.ItemTemplate; var headerButton=sortHeaders[ProcessSort.Disk].Button;
                MoveColumn("Disk","Network");
                if(!ReferenceEquals(template,table.ItemTemplate)||!ReferenceEquals(headerButton,sortHeaders[ProcessSort.Disk].Button)) throw new InvalidOperationException("Reorder rebuilt row templates or header controls instead of moving cells.");
            }
            var bindingErrors=new List<string>();
            var exceptionKinds=new System.Collections.Concurrent.ConcurrentDictionary<string,int>();
            void FirstChance(object? sender,System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)=>exceptionKinds.AddOrUpdate(e.Exception.GetType().FullName!,1,(_,count)=>count+1);
            AppDomain.CurrentDomain.FirstChanceException+=FirstChance;
            Microsoft.UI.Xaml.Application.Current.DebugSettings.IsBindingTracingEnabled=true;
            void BindingFailed(object sender,BindingFailedEventArgs e) { if(bindingErrors.Count<20) bindingErrors.Add(e.Message); }
            Microsoft.UI.Xaml.Application.Current.DebugSettings.BindingFailed+=BindingFailed;
            var selected=rows.Where(r=>!r.IsGroup).Take(2).ToArray(); foreach(var row in selected) if(!table.SelectedItems.Contains(row)) table.SelectedItems.Add(row);
            ApplyColumns(tableColumnLayout.Concat([new("NativeName",180),new("CommandLine",400),new("Path",280)])); await Task.Delay(200);
            var old=tableColumnLayout.Select(c=>c.Id).ToArray(); MoveColumn("Name","CommandLine"); MoveColumn("CommandLine","Name");
            if(!old.SequenceEqual(tableColumnLayout.Select(c=>c.Id))) throw new InvalidOperationException("Name was reorderable.");
            MoveColumn("CommandLine","Assessment"); await Task.Delay(100);
            if(tableColumnLayout[1].Id!="CommandLine") throw new InvalidOperationException("Column reorder failed.");
            ResizeColumn(0,340,true); ResizeColumn(1,460,true); await Task.Delay(150);
            RefreshRows(); await Task.Delay(150);
            if(selected.Any(r=>!table.SelectedItems.Contains(r))) throw new InvalidOperationException("Refresh lost multiselection.");
            void CheckGeometry() {
                var container=(ListViewItem)table.ContainerFromIndex(0);
                var grid=FixtureControls<Grid>(container).First(g=>g.Height==28&&g.ColumnDefinitions.Count==tableColumnLayout.Length+1);
                for(var i=0;i<tableColumnLayout.Length;i++) {
                    if(Math.Abs(grid.ColumnDefinitions[i].ActualWidth-tableHeader!.ColumnDefinitions[i].ActualWidth)>1) throw new InvalidOperationException("Resizing misaligned headers and rows.");
                }
            }
            CheckGeometry(); var scrolling=FindTableScrollViewer(table)!; scrolling.ChangeView(scrolling.ScrollableWidth,null,null,true); await Task.Delay(120); CheckGeometry();
            ApplyColumns(tableColumnLayout.Where(c=>c.Id!="Path")); await Task.Delay(100);
            if(tableColumnLayout.Any(c=>c.Id=="Path")) throw new InvalidOperationException("Column hide failed.");
            var saved=tableColumnLayout.ToArray(); RenderPage(); await Task.Delay(200);
            if(!saved.SequenceEqual(tableColumnLayout)) throw new InvalidOperationException("Page recreation lost saved layout.");
            table!.SelectedItem=rows.First(r=>!r.IsGroup); SelectSort(ProcessSort.NativeName); RefreshRows(reorder:true); CheckGeometry();
            if(mode=="columns") {
                for(var repeat=0;repeat<4;repeat++) { MoveColumn("Disk","Network"); await Task.Delay(50); MoveColumn("Network","Disk"); await Task.Delay(50); }
            }
            Microsoft.UI.Xaml.Application.Current.DebugSettings.BindingFailed-=BindingFailed;
            AppDomain.CurrentDomain.FirstChanceException-=FirstChance;
            if(exceptionKinds.Count>0) File.WriteAllLines(output+".exception-kinds",exceptionKinds.Select(p=>p.Key+": "+p.Value));
            if(bindingErrors.Count>0) { File.WriteAllLines(output+".bindings",bindingErrors); throw new InvalidOperationException("Column customization produced binding failures."); }
            File.WriteAllText(output+".checks","Name fixed first; add/hide/reorder/resize columns; row/header geometry; horizontal scroll; multiselection refresh; saved layout and optional sorting pass.");
        } else if(mode=="column-drag") {
            var original=tableColumnLayout.ToArray();
            var header=tableHeader!.Children.OfType<Border>().Single(b=>b.Tag as string=="Pid");
            draggedColumnId="Pid";
            PositionDraggedHeader(header,"Pid",390,30);
            PreviewDraggedColumns(original,"Pid",390);
            PositionDraggedHeader(header,"Pid",390,30);
            if(!columnMotions.Values.Any(m=>m.Animation is not null)) throw new InvalidOperationException("Neighboring columns did not start sliding.");
            await Task.Delay(60);
            var x=header.TransformToVisual(WindowRoot).TransformPoint(new(0,0)).X;
            foreach(var row in rows) if(table.ContainerFromItem(row) is ListViewItem { ContentTemplateRoot:Grid grid }) {
                var cell=grid.Children.OfType<FrameworkElement>().Single(c=>c.Tag as string=="Pid");
                if(Math.Abs(cell.TransformToVisual(WindowRoot).TransformPoint(new(0,0)).X-x)>1)
                    throw new InvalidOperationException("Dragged column geometry differs between header and body.");
            }
            File.WriteAllText(output+".checks","Mid-drag full-column rendering and header/body geometry pass; no pointer injection.");
            var holdDeadline=DateTime.UtcNow.AddSeconds(45);
            while(File.Exists(output+".hold")&&DateTime.UtcNow<holdDeadline) await Task.Delay(100);
        } else if(mode=="columns-pointer") {
            Title="ProcSight - synthetic column drag test"; AppTitleBar.Title=Title;
            var deadline=DateTime.UtcNow.AddSeconds(90);
            while(!File.Exists(output+".finish")&&DateTime.UtcNow<deadline) await Task.Delay(100);
            File.WriteAllText(output+".layout",string.Join(",",tableColumnLayout.Select(c=>c.Id)));
            if(tableColumnLayout[0].Id!="Name"||tableColumnLayout[1].Id!="Pid") throw new InvalidOperationException("Real pointer drag did not reorder PID before Assessment.");
            File.WriteAllText(output+".checks","Real pointer drag moved PID before Assessment and kept Name fixed.");
        } else if(mode=="row-menu") {
            var menu=RowMenu(primary); menu.ShowAt(table); await Task.Delay(100);
            var details=menu.Items.OfType<MenuFlyoutItem>().Single(i=>i.Text=="Show details"); FixtureInvoke(details); menu.Hide(); await Task.Delay(100);
            if(!detailsOpen||detailSelectionKey is null) throw new InvalidOperationException("Context details action failed.");
            detailsOpen=false; UpdateDetailsLayout();
            var second=rows.First(r=>!r.IsGroup&&r!=primary); table.SelectedItems.Add(second);
            var multi=RowMenu(primary);
            if(multi.Items.OfType<MenuFlyoutItem>().Single(i=>i.Text=="Analyze").IsEnabled||!multi.Items.OfType<MenuFlyoutItem>().Any(i=>i.Text=="Copy selected names")) throw new InvalidOperationException("Context menu ignored multiselection.");
            var captured=CaptureRowTarget(second); var oldProjection=second.Projection;
            second.Update(oldProjection with { Entity=second.Entity with { Process=second.Entity.Process! with { Key=new(second.Entity.Process!.Key.Pid,DateTimeOffset.UtcNow.AddHours(1),null) } } });
            if(IsRowTargetCurrent(captured)) throw new InvalidOperationException("Context action accepted a replacement lifetime.");
            second.Update(oldProjection);
            // Invoke the actual templated native chevron, rather than calling the expansion method.
            presentation=presentation with { Grouped=true }; RefreshRows(); await Task.Delay(200);
            var group=rows.First(r=>r.IsGroup); table.SelectedItem=group; table.ScrollIntoView(group); await Task.Delay(100);
            var container=(ListViewItem)table.ContainerFromItem(group);
            var chevron=FixtureControls<Button>(container).First(b=>b.Tag as string==group.Key);
            var expanded=group.Projection.Expanded; FixtureInvoke(chevron); await Task.Delay(150);
            if(group.Projection.Expanded==expanded) throw new InvalidOperationException("Dynamic template chevron was not wired.");
            var groupMenu=RowMenu(group);
            if(groupMenu.Items.OfType<MenuFlyoutItem>().Any(i=>i.Text=="Analyze")) throw new InvalidOperationException("Group exposes individual analysis.");
            groupMenu.ShowAt(table); await Task.Delay(150);
            File.WriteAllText(output+".checks","Individual/multi/group context actions, native menu invocation and dynamic chevron pass.");
        } else if(mode=="column-chooser") {
            _=ChooseColumnsAsync(); await Task.Delay(250);
            var dialog=tableConfigurationDialog??throw new InvalidOperationException("Missing column dialog.");
            if(dialog.ActualTheme!=WindowRoot.ActualTheme) throw new InvalidOperationException("Column chooser theme differs from app.");
            var content=(ScrollViewer)dialog.Content; var check=FixtureControls<CheckBox>((DependencyObject)content.Content).Single(c=>c.Content as string=="Command line");
            var peer=Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(check);
            ((Microsoft.UI.Xaml.Automation.Provider.IToggleProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Toggle)).Toggle(); await Task.Delay(150);
            if(tableColumnLayout.All(c=>c.Id!="CommandLine")) throw new InvalidOperationException("Column chooser did not apply immediately.");
            File.WriteAllText(output+".checks","Native column chooser applies checkbox changes immediately.");
        } else {
            _=ExportInventoryAsync(); await Task.Delay(200);
            if(tableConfigurationDialog?.Title as string!="Export inventory") throw new InvalidOperationException("Missing export dialog.");
            if(tableConfigurationDialog.ActualTheme!=WindowRoot.ActualTheme) throw new InvalidOperationException("Export dialog theme differs from app.");
            var controls=FixtureControls<CheckBox>((DependencyObject)((ScrollViewer)tableConfigurationDialog.Content).Content).ToArray();
            if(controls.Length!=3||controls.Any(c=>c.IsChecked==true)) throw new InvalidOperationException("Private export fields were enabled implicitly.");
            if(mode=="export-picker") {
                Title="ProcSight - synthetic export test"; AppTitleBar.Title=Title;
                var deadline=DateTime.UtcNow.AddSeconds(90);
                while(!File.Exists(output+".csv")&&DateTime.UtcNow<deadline) await Task.Delay(100);
                if(!File.Exists(output+".csv")) throw new InvalidOperationException("Real Save picker did not produce a file.");
                var text=File.ReadAllText(output+".csv");
                if(!text.StartsWith("\"Name\",\"Assessment\",\"Pid\"")||text.Contains("CommandLine")||text.Contains("C:\\Program Files")) throw new InvalidOperationException("Export fields were incorrect.");
                File.WriteAllText(output+".checks","Real Windows App SDK Save picker wrote the selected synthetic CSV with private fields excluded.");
                return;
            }
            File.WriteAllText(output+".checks","Native export dialog has selected/filtered/full scopes, formats and private fields opt-in; Save picker is not launched by fixtures.");
        }
    }
}
#endif

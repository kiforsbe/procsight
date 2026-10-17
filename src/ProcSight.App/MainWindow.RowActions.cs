using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ProcSight.App.ViewModels;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private sealed record RowActionTarget(ProcessRow Row,ResolvedEntity Entity,bool Group,string Name,string Pid);
    private static RowActionTarget CaptureRowTarget(ProcessRow row)=>new(row,row.Entity,row.IsGroup,row.Name,row.Pid);
    private bool IsEntityCurrent(ResolvedEntity entity)=>entities.Any(e=>e.Identity==entity.Identity&&e.Process?.Key==entity.Process?.Key&&e.Service?.Name==entity.Service?.Name);
    private bool IsRowTargetCurrent(RowActionTarget target)=>rows.Contains(target.Row)&&target.Row.IsGroup==target.Group&&
        target.Row.Entity.Identity==target.Entity.Identity&&target.Row.Entity.Process?.Key==target.Entity.Process?.Key&&target.Row.Entity.Service?.Name==target.Entity.Service?.Name;
    private void ShowRowContextMenu(UIElement sender,ContextRequestedEventArgs args)
    {
        if(table is null) return;
        var pointer=args.TryGetPosition(table,out var point);
        DependencyObject? source=args.OriginalSource as DependencyObject;
        while(source is not null&&source is not ListViewItem) source=VisualTreeHelper.GetParent(source);
        var row=(source as ListViewItem)?.Content as ProcessRow;
        if(row is null&&!pointer) row=table.SelectedItem as ProcessRow;
        if(row is null||!rows.Contains(row)) return;
        if(!table.SelectedItems.Contains(row)) table.SelectedItem=row;
        var menu=RowMenu(row); args.Handled=true;
        if(pointer) menu.ShowAt(table,new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position=point });
        else menu.ShowAt((FrameworkElement?)source??table);
    }
    private MenuFlyout RowMenu(ProcessRow row)
    {
        var menu=new MenuFlyout(); var key=row.Entity.Process?.Key; var service=row.Entity.Service?.Name; var group=row.IsGroup;
        var selection=table!.SelectedItems.OfType<ProcessRow>().Select(CaptureRowTarget).ToArray();
        bool Current()=>rows.Contains(row)&&row.IsGroup==group&&row.Entity.Process?.Key==key&&row.Entity.Service?.Name==service;
        void Add(string title,Func<Task> action,bool enabled=true,Symbol? icon=null) {
            var item=new MenuFlyoutItem { Text=title,IsEnabled=enabled }; if(icon is { } symbol) item.Icon=new SymbolIcon(symbol);
            item.Click+=async(_,_)=> { if(!Current()) return; try { await action(); } catch(OperationCanceledException) { } catch { await MessageAsync("Action unavailable","The selected item or its metadata is no longer available. Refresh the list and try again."); } }; menu.Items.Add(item);
        }
        var single=selection.Length==1;
        if(row.Projection.HasChildren) Add(row.Projection.Expanded?"Collapse":"Expand",()=> { ToggleExpansion(new Button { Tag=row.Key },new RoutedEventArgs()); return Task.CompletedTask; },single);
        Add("Show details",()=> { detailsOpen=true; UpdateDetailsLayout(); ShowDetail(row); return Task.CompletedTask; },single,Symbol.Document);
        if(!group) {
            Add("Analyze",async()=>await coordinator.RequestReanalysisAsync(row.Entity.Identity,lifetime.Token),single&&ProcSight.Application.Analysis.ConnectionRouter.Resolve(EffectivePolicy).Count>0,Symbol.Find);
            Add("View rules",()=>InspectProcessRulesAsync(row.Entity),single,Symbol.Library);
            Add("Correct classification",CorrectSelectedAsync,single,Symbol.Edit);
            Add("Preview AI payload",PreviewAsync,single);
        }
        menu.Items.Add(new MenuFlyoutSeparator());
        Add(selection.Length>1?"Copy selected names":"Copy name",()=> { CopyLocal(string.Join(Environment.NewLine,selection.Where(IsRowTargetCurrent).Select(r=>r.Name))); return Task.CompletedTask; },icon:Symbol.Copy);
        Add(selection.Length>1?"Copy selected PIDs":"Copy PID",()=> { CopyLocal(string.Join(Environment.NewLine,selection.Where(r=>IsRowTargetCurrent(r)&&!r.Group).Select(r=>r.Pid))); return Task.CompletedTask; },selection.Any(r=>!r.Group&&r.Pid!="—"));
        Add("Export…",()=>ExportInventoryAsync(),icon:Symbol.Save);
        return menu;
    }
    private static void CopyLocal(string text)
    {
        var data=new global::Windows.ApplicationModel.DataTransfer.DataPackage(); data.SetText(text);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
    }
}

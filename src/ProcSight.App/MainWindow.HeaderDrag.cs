using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private bool headerGestureConsumed;
    private string? draggedColumnId;
    private sealed class ColumnMotion
    {
        public TranslateTransform Transform { get; }=new();
        public Storyboard? Animation { get; set; }
        public double Target { get; set; }
    }
    private readonly Dictionary<string,ColumnMotion> columnMotions=[];
    private readonly Dictionary<FrameworkElement,(Transform Transform,int Z,Brush? Background,Microsoft.UI.Xaml.Data.Binding? BackgroundBinding)> draggedCells=[];
    private Brush? draggedColumnBackground;
    private TableColumnPreference[] dragOriginalLayout=[],dragPreviewLayout=[];

    private void BeginColumnMotion(string id)
    {
        draggedColumnId=id; dragOriginalLayout=tableColumnLayout.ToArray(); dragPreviewLayout=dragOriginalLayout;
        foreach(var column in dragOriginalLayout) if(column.Id!="Name") columnMotions[column.Id]=new();
        var surface=(Border)XamlReader.Load("""<Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Background="{ThemeResource SolidBackgroundFillColorBaseBrush}"/>""");
        surface.RequestedTheme=WindowRoot.ActualTheme; draggedColumnBackground=surface.Background;
        if(tableHeader is not null) foreach(var cell in tableHeader.Children.OfType<FrameworkElement>()) AttachColumnMotion(cell);
        foreach(var grid in RealizedColumnRows()) PositionDraggedRow(grid,id);
    }
    private void AttachColumnMotion(FrameworkElement cell)
    {
        if(cell.Tag is not string id||!columnMotions.TryGetValue(id,out var motion)||draggedCells.ContainsKey(cell)) return;
        draggedCells[cell]=(cell.RenderTransform,Canvas.GetZIndex(cell),(cell as Border)?.Background,
            (cell as Border)?.GetBindingExpression(Border.BackgroundProperty)?.ParentBinding);
        cell.RenderTransform=motion.Transform;
        if(id==draggedColumnId) {
            Canvas.SetZIndex(cell,10);
            if(cell is Border border) border.Background=draggedColumnBackground;
        }
    }
    private void PreviewDraggedColumns(IReadOnlyList<TableColumnPreference> original,string id,double pointerX)
    {
        var preview=ProcSight.Application.Presentation.TableColumns.ReorderPreview(original,id,pointerX);
        if(preview.Select(c=>c.Id).SequenceEqual(dragPreviewLayout.Select(c=>c.Id))) return;
        double left=0;
        var originalPositions=dragOriginalLayout.ToDictionary(c=>c.Id,c=> { var x=left; left+=c.Width; return x; });
        left=0;
        foreach(var column in preview) {
            var target=left-originalPositions[column.Id]; left+=column.Width;
            if(column.Id==id||!columnMotions.TryGetValue(column.Id,out var motion)||Math.Abs(motion.Target-target)<.1) continue;
            var from=motion.Transform.X;
            motion.Animation?.Stop(); motion.Transform.X=target; motion.Target=target;
            var animation=new DoubleAnimation { From=from,To=target,Duration=new(TimeSpan.FromMilliseconds(180)),
                EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } };
            Storyboard.SetTarget(animation,motion.Transform); Storyboard.SetTargetProperty(animation,"X");
            var story=new Storyboard(); story.Children.Add(animation); motion.Animation=story;
            story.Completed+=(_,_)=> { if(ReferenceEquals(motion.Animation,story)) { motion.Animation=null; story.Stop(); } };
            story.Begin();
        }
        dragPreviewLayout=preview;
    }
    private void PositionDraggedHeader(Border header,string id,double pointerX,double grabOffset)
    {
        if(columnMotions.Count==0) BeginColumnMotion(id);
        var index=Array.FindIndex(dragOriginalLayout,c=>c.Id==id);
        if(index<1) return;
        var left=dragOriginalLayout.Take(index).Sum(c=>c.Width);
        var minimum=dragOriginalLayout[0].Width;
        var maximum=Math.Max(minimum,dragOriginalLayout.Sum(c=>c.Width)-dragOriginalLayout[index].Width);
        // One native property write moves the entire column, including newly realized rows.
        columnMotions[id].Transform.X=Math.Clamp(pointerX-grabOffset,minimum,maximum)-left;
    }
    private void PositionDraggedRow(Grid grid,string id)
    {
        foreach(var cell in grid.Children.OfType<FrameworkElement>()) AttachColumnMotion(cell);
    }
    private void RestoreDraggedColumn()
    {
        foreach(var motion in columnMotions.Values) motion.Animation?.Stop();
        foreach(var (cell,state) in draggedCells) {
            cell.RenderTransform=state.Transform; Canvas.SetZIndex(cell,state.Z);
            if(cell is Border border&&cell.Tag as string==draggedColumnId) {
                if(state.BackgroundBinding is { } binding) border.SetBinding(Border.BackgroundProperty,binding);
                else border.Background=state.Background;
            }
        }
        draggedCells.Clear(); columnMotions.Clear(); draggedColumnId=null; draggedColumnBackground=null;
        dragOriginalLayout=[]; dragPreviewLayout=[];
    }
    private void AttachHeaderDrag(Button button,string id)
    {
        uint? pointer=null; double start=0,grabOffset=0; var dragging=false;
        TableColumnPreference[] original=[];
        Border? movingHeader=null;
        int Target(double x) {
            double edge=0;
            for(var index=0;index<original.Length;index++) { edge+=original[index].Width; if(x<edge) return index; }
            return original.Length-1;
        }
        void Cancel() {
            pointer=null; RestoreDraggedColumn(); dragging=false;
            DispatcherQueue.TryEnqueue(()=>headerGestureConsumed=false);
        }
        button.AddHandler(UIElement.PointerPressedEvent,new PointerEventHandler((_,e)=> {
            if(tableHeader is null||!e.GetCurrentPoint(button).Properties.IsLeftButtonPressed) return;
            headerGestureConsumed=false;
            if(id=="Name") return;
            pointer=e.Pointer.PointerId; start=e.GetCurrentPoint(tableHeader).Position.X;
            original=tableColumnLayout.ToArray(); dragging=false;
            grabOffset=start-original.TakeWhile(c=>c.Id!=id).Sum(c=>c.Width);
            movingHeader=tableHeader.Children.OfType<Border>().Single(c=>c.Tag as string==id);
            button.CapturePointer(e.Pointer);
        }),true);
        button.AddHandler(UIElement.PointerMovedEvent,new PointerEventHandler((_,e)=> {
            if(pointer!=e.Pointer.PointerId||tableHeader is null||movingHeader is null||!e.GetCurrentPoint(button).Properties.IsLeftButtonPressed) return;
            var x=e.GetCurrentPoint(tableHeader).Position.X;
            if(!dragging&&Math.Abs(x-start)<6) return;
            headerGestureConsumed=true; dragging=true;
            PositionDraggedHeader(movingHeader,id,x,grabOffset);
            PreviewDraggedColumns(original,id,x);
        }),true);
        button.AddHandler(UIElement.PointerReleasedEvent,new PointerEventHandler((_,e)=> {
            if(pointer!=e.Pointer.PointerId||tableHeader is null) return;
            var position=e.GetCurrentPoint(tableHeader).Position;
            var index=Target(position.X); pointer=null; RestoreDraggedColumn();
            if(!dragging) return;
            dragging=false; headerGestureConsumed=true;
            if(index>0&&position.Y>=0&&position.Y<=tableHeader.ActualHeight&&position.X<=tableHeader.ActualWidth)
                ApplyColumns(ProcSight.Application.Presentation.TableColumns.ReorderPreview(original,id,position.X),true);
            DispatcherQueue.TryEnqueue(()=>headerGestureConsumed=false);
        }),true);
        button.PointerCanceled+=(_,_)=>Cancel();
        // Native Button releases capture before our routed release handler can commit.
        button.PointerCaptureLost+=(_,_)=>DispatcherQueue.TryEnqueue(()=> { if(pointer is not null) Cancel(); });
        button.Unloaded+=(_,_)=> { if(pointer is null&&!dragging) return; pointer=null; dragging=false; RestoreDraggedColumn(); };
        button.KeyDown+=(_,e)=> {
            if(e.Key is global::Windows.System.VirtualKey.Enter or global::Windows.System.VirtualKey.Space) headerGestureConsumed=false;
            if(e.Key==global::Windows.System.VirtualKey.Escape) { Cancel(); button.ReleasePointerCaptures(); e.Handled=true; }
        };
    }
}

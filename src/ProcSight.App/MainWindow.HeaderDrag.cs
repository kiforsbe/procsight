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
    private double draggedColumnOffset;
    private readonly Dictionary<FrameworkElement,(Transform Transform,TransitionCollection Transitions,int Z,Brush? Background,Microsoft.UI.Xaml.Data.Binding? BackgroundBinding)> draggedCells=[];
    private Brush? draggedColumnBackground;
    private readonly Dictionary<FrameworkElement,(Storyboard Story,Transform Original)> columnSlides=[];

    private void SlideColumnCells(Grid grid,IReadOnlyDictionary<string,double> previous)
    {
        double left=0;
        var current=tableColumnLayout.ToDictionary(c=>c.Id,c=> { var x=left; left+=c.Width; return x; });
        foreach(var cell in grid.Children.OfType<Border>()) {
            if(cell.Tag is not string id||id==draggedColumnId||!previous.TryGetValue(id,out var oldLeft)) continue;
            var offset=oldLeft-current[id];
            if(columnSlides.ContainsKey(cell)&&cell.RenderTransform is TranslateTransform running) offset+=running.X;
            StopColumnSlide(cell);
            if(Math.Abs(offset)<.1) continue;
            var original=cell.RenderTransform;
            var translation=new TranslateTransform(); cell.RenderTransform=translation;
            var animation=new DoubleAnimation { From=offset,To=0,Duration=new(TimeSpan.FromMilliseconds(180)),
                EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } };
            Storyboard.SetTarget(animation,translation); Storyboard.SetTargetProperty(animation,"X");
            var story=new Storyboard(); story.Children.Add(animation); columnSlides[cell]=(story,original);
            story.Completed+=(_,_)=> { if(columnSlides.TryGetValue(cell,out var value)&&ReferenceEquals(value.Story,story)) StopColumnSlide(cell); };
            story.Begin();
        }
    }
    private void StopColumnSlide(FrameworkElement cell)
    {
        if(columnSlides.Remove(cell,out var active)) { active.Story.Stop(); cell.RenderTransform=active.Original; }
    }
    private void StopColumnSlides()
    {
        foreach(var cell in columnSlides.Keys.ToArray()) StopColumnSlide(cell);
    }

    private void PositionDraggedHeader(Border header,string id,double pointerX,double grabOffset)
    {
        var index=Array.FindIndex(tableColumnLayout,c=>c.Id==id);
        if(index<1) return;
        var left=tableColumnLayout.Take(index).Sum(c=>c.Width);
        var minimum=tableColumnLayout[0].Width;
        var maximum=Math.Max(minimum,TableMinimumWidth-tableColumnLayout[index].Width);
        draggedColumnOffset=Math.Clamp(pointerX-grabOffset,minimum,maximum)-left;
        PositionDraggedCell(header);
        foreach(var grid in RealizedColumnRows()) PositionDraggedRow(grid,id);
    }
    private void PositionDraggedRow(Grid grid,string id)
    {
        if(grid.Children.OfType<FrameworkElement>().FirstOrDefault(c=>c.Tag as string==id) is { } cell) PositionDraggedCell(cell);
    }
    private void PositionDraggedCell(FrameworkElement cell)
    {
        if(!draggedCells.ContainsKey(cell)) {
            StopColumnSlide(cell);
            draggedCells[cell]=(cell.RenderTransform,cell.Transitions,Canvas.GetZIndex(cell),(cell as Border)?.Background,
                (cell as Border)?.GetBindingExpression(Border.BackgroundProperty)?.ParentBinding);
            cell.Transitions=new TransitionCollection();
            cell.RenderTransform=new TranslateTransform();
            Canvas.SetZIndex(cell,10);
            // Full-height opaque cells move together rather than a floating label covering other text.
            if(cell is Border border) {
                if(draggedColumnBackground is null) {
                    var surface=(Border)XamlReader.Load("""<Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Background="{ThemeResource SolidBackgroundFillColorBaseBrush}"/>""");
                    surface.RequestedTheme=WindowRoot.ActualTheme;
                    draggedColumnBackground=surface.Background;
                }
                border.Background=draggedColumnBackground;
            }
        }
        ((TranslateTransform)cell.RenderTransform).X=draggedColumnOffset;
    }
    private void RestoreDraggedColumn()
    {
        foreach(var (cell,state) in draggedCells) {
            cell.RenderTransform=state.Transform; cell.Transitions=state.Transitions; Canvas.SetZIndex(cell,state.Z);
            if(cell is Border border) {
                if(state.BackgroundBinding is { } binding) border.SetBinding(Border.BackgroundProperty,binding);
                else border.Background=state.Background;
            }
        }
        draggedCells.Clear(); draggedColumnId=null; draggedColumnOffset=0; draggedColumnBackground=null;
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
            pointer=null; RestoreDraggedColumn();
            if(dragging) { dragging=false; ApplyColumns(original,false); }
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
            headerGestureConsumed=true; draggedColumnId=id;
            if(!dragging) {
                dragging=true;
                // Disable easing on dragged cells before rearranging their layout slots.
                PositionDraggedHeader(movingHeader,id,x,grabOffset);
            }
            var preview=ProcSight.Application.Presentation.TableColumns.ReorderPreview(original,id,x);
            if(!preview.Select(c=>c.Id).SequenceEqual(tableColumnLayout.Select(c=>c.Id))) ApplyColumns(preview,false,true);
            PositionDraggedHeader(movingHeader,id,x,grabOffset);
        }),true);
        button.AddHandler(UIElement.PointerReleasedEvent,new PointerEventHandler((_,e)=> {
            if(pointer!=e.Pointer.PointerId||tableHeader is null) return;
            var position=e.GetCurrentPoint(tableHeader).Position;
            var index=Target(position.X); pointer=null; RestoreDraggedColumn();
            if(!dragging) return;
            dragging=false; headerGestureConsumed=true;
            if(index>0&&position.Y>=0&&position.Y<=tableHeader.ActualHeight&&position.X<=tableHeader.ActualWidth) {
                ApplyColumns(ProcSight.Application.Presentation.TableColumns.ReorderPreview(original,id,position.X),false);
                ApplyColumns(tableColumnLayout,true);
            } else ApplyColumns(original,false);
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

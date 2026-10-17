using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using ProcSight.Core.Models;
namespace ProcSight.App;
public sealed partial class MainWindow
{
    private bool headerGestureConsumed;
    private void AttachHeaderDrag(Button button,string id)
    {
        uint? pointer=null; double start=0; var dragging=false;
        TableColumnPreference[] original=[];
        Popup? ghost=null;
        int Target(double x) {
            double edge=0;
            for(var index=0;index<original.Length;index++) { edge+=original[index].Width; if(x<edge) return index; }
            return original.Length-1;
        }
        void CloseGhost() { if(ghost is not null) { ghost.IsOpen=false; ghost.Child=null; ghost=null; } }
        void Cancel() {
            pointer=null; CloseGhost();
            if(dragging) { dragging=false; ApplyColumns(original,false); }
            DispatcherQueue.TryEnqueue(()=>headerGestureConsumed=false);
        }
        button.AddHandler(UIElement.PointerPressedEvent,new PointerEventHandler((_,e)=> {
            if(tableHeader is null||!e.GetCurrentPoint(button).Properties.IsLeftButtonPressed) return;
            headerGestureConsumed=false;
            if(id=="Name") return;
            pointer=e.Pointer.PointerId; start=e.GetCurrentPoint(tableHeader).Position.X;
            original=tableColumnLayout.ToArray(); dragging=false;
            button.CapturePointer(e.Pointer);
        }),true);
        button.AddHandler(UIElement.PointerMovedEvent,new PointerEventHandler((_,e)=> {
            if(pointer!=e.Pointer.PointerId||tableHeader is null||!e.GetCurrentPoint(button).Properties.IsLeftButtonPressed) return;
            var x=e.GetCurrentPoint(tableHeader).Position.X;
            if(!dragging&&Math.Abs(x-start)<6) return;
            dragging=true; headerGestureConsumed=true;
            if(ghost is null) {
                var title=ProcSight.Application.Presentation.TableColumns.All.Single(c=>c.Id==id).Label;
                ghost=new Popup { XamlRoot=button.XamlRoot,IsHitTestVisible=false,Child=new Border { RequestedTheme=WindowRoot.ActualTheme,
                    Background=(Brush)Microsoft.UI.Xaml.Application.Current.Resources["LayerFillColorDefaultBrush"],
                    BorderBrush=(Brush)Microsoft.UI.Xaml.Application.Current.Resources["AccentFillColorDefaultBrush"],
                    BorderThickness=new(1),CornerRadius=new(4),Padding=new(10,6,10,6),Opacity=.9,
                    Child=new TextBlock { Text=title } } };
                ghost.IsOpen=true;
            }
            var location=e.GetCurrentPoint(WindowRoot).Position;
            ghost.HorizontalOffset=location.X+12; ghost.VerticalOffset=location.Y+8;
            var preview=ProcSight.Application.Presentation.TableColumns.ReorderPreview(original,id,x);
            if(!preview.Select(c=>c.Id).SequenceEqual(tableColumnLayout.Select(c=>c.Id))) ApplyColumns(preview,false);
        }),true);
        button.AddHandler(UIElement.PointerReleasedEvent,new PointerEventHandler((_,e)=> {
            if(pointer!=e.Pointer.PointerId||tableHeader is null) return;
            var position=e.GetCurrentPoint(tableHeader).Position;
            var index=Target(position.X); pointer=null; CloseGhost();
            if(!dragging) return;
            dragging=false; headerGestureConsumed=true;
            if(index>0&&position.Y>=0&&position.Y<=tableHeader.ActualHeight&&position.X<=tableHeader.ActualWidth) {
                ApplyColumns(ProcSight.Application.Presentation.TableColumns.ReorderPreview(original,id,position.X),false);
                ApplyColumns(tableColumnLayout,true);
            } else ApplyColumns(original,false);
            DispatcherQueue.TryEnqueue(()=>headerGestureConsumed=false);
        }),true);
        button.PointerCanceled+=(_,_)=>Cancel();
        // Button releases capture during its own release handler; defer loss handling until
        // our PointerReleased handler has had a chance to commit and clear the pointer.
        button.PointerCaptureLost+=(_,_)=>DispatcherQueue.TryEnqueue(()=> { if(pointer is not null) Cancel(); });
        button.Unloaded+=(_,_)=> { pointer=null; dragging=false; CloseGhost(); };
        button.KeyDown+=(_,e)=> {
            if(e.Key is global::Windows.System.VirtualKey.Enter or global::Windows.System.VirtualKey.Space) headerGestureConsumed=false;
            if(e.Key==global::Windows.System.VirtualKey.Escape) { Cancel(); button.ReleasePointerCaptures(); e.Handled=true; }
        };
    }
}

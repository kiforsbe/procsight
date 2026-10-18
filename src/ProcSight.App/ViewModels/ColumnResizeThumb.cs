using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
namespace ProcSight.App.ViewModels;
// WinUI Thumb is sealed; the native thumb inherits this wrapper's resize cursor.
public sealed class ColumnResizeThumb : UserControl
{
    private readonly Thumb thumb=new() { Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),BorderThickness=new(0) };
    public ColumnResizeThumb() {
        ProtectedCursor=InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
        // Retain native input/automation but replace the stock grip and all of its state chrome.
        thumb.Template=(Microsoft.UI.Xaml.Controls.ControlTemplate)XamlReader.Load("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Thumb">
              <Grid Background="Transparent"/>
            </ControlTemplate>
            """);
        Content=thumb;
    }
    public event DragDeltaEventHandler DragDelta { add=>thumb.DragDelta+=value; remove=>thumb.DragDelta-=value; }
    public event DragCompletedEventHandler DragCompleted { add=>thumb.DragCompleted+=value; remove=>thumb.DragCompleted-=value; }
}

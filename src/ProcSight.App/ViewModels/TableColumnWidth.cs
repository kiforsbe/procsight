using System.ComponentModel;
using Microsoft.UI.Xaml;
namespace ProcSight.App.ViewModels;
public sealed class TableColumnWidth(double width) : INotifyPropertyChanged
{
    private GridLength width=new(width);
    private static readonly PropertyChangedEventArgs WidthChanged=new(nameof(Width));
    public GridLength Width { get=>width; set { if(width==value) return; width=value; PropertyChanged?.Invoke(this,WidthChanged); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

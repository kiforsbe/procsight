using System.ComponentModel;
using Microsoft.UI.Xaml;
namespace ProcSight.App.ViewModels;
public sealed class TableColumnWidth(double width) : INotifyPropertyChanged
{
    private GridLength width=new(width);
    public GridLength Width { get=>width; set { if(width==value) return; width=value; PropertyChanged?.Invoke(this,new(nameof(Width))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

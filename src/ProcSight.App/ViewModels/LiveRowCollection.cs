using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
namespace ProcSight.App.ViewModels;
public sealed class LiveRowCollection : ObservableCollection<ProcessRow>
{
    protected override void MoveItem(int oldIndex,int newIndex)
    {
        if(oldIndex==newIndex) return;
        // The WinRT vector adapter translates NotifyCollectionChanged.Move into Reset.
        // Native remove/insert notifications keep the rest of the list realized.
        var row=this[oldIndex]; RemoveItem(oldIndex); InsertItem(newIndex,row);
    }
    // Explicit sorting/filtering can permute hundreds of rows. Notify the native list once;
    // keep the same view models and restore lifetime-aware selection in RefreshRows.
    public void ReplaceOrder(IReadOnlyList<ProcessRow> ordered)
    {
        CheckReentrancy();
        Items.Clear();
        foreach(var row in ordered) Items.Add(row);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

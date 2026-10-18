using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class ExecutableIconTests
{
    [Fact] public void ShellReturnsExecutableIconsInsteadOfOneSharedDocumentSymbol()
    {
        var system=Environment.GetFolderPath(Environment.SpecialFolder.System);
        var task=ExecutableIconReader.Read(Path.Combine(system,"Taskmgr.exe"));
        var note=ExecutableIconReader.Read(Path.Combine(system,"notepad.exe"));
        Assert.NotNull(task); Assert.NotNull(note);
        Assert.Equal(32*32*4,task.Pixels.Length);
        Assert.False(task.Pixels.SequenceEqual(note.Pixels));
    }
    [Fact] public void MissingAndNetworkPathsDoNotTriggerIconLookup()
    {
        Assert.Null(ExecutableIconReader.Read(@"C:\does-not-exist\task.exe"));
        Assert.Null(ExecutableIconReader.Read(@"\\server\share\task.exe"));
    }
}

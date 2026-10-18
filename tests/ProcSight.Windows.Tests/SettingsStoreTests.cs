using ProcSight.Core.Models;
using ProcSight.Windows.Storage;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class SettingsStoreTests
{
    [Fact] public void AtomicPreferencesPreserveLocalMode()
    {
        var folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString()); var path=Path.Combine(folder,"settings.json");
        try
        {
            var store=new PreferencesStore(path); store.Save(new(CommandLineMode:CommandLineMode.LocalOnly),new());
            Assert.Equal(CommandLineMode.LocalOnly,new PreferencesStore(path).Load().Policy.CommandLineMode);
            Assert.Empty(Directory.GetFiles(folder,"*.tmp"));
            File.WriteAllText(path,"invalid");
            Assert.Throws<InvalidDataException>(()=>store.Load());
        }
        finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }
    [Fact] public void CredentialRoundTripAndRemove()
    {
        var id="test-"+Guid.NewGuid(); var store=new CredentialStore();
        try { store.Set(id,"synthetic-secret"); Assert.Equal("synthetic-secret",store.Get(id)); }
        finally { store.Remove(id); }
        Assert.Null(store.Get(id));
    }
}

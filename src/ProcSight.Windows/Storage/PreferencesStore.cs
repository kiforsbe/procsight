using ProcSight.Application.Settings;
using ProcSight.Core.Models;
using System.Text.Json;
namespace ProcSight.Windows.Storage;
public sealed class PreferencesStore(string path) : IPreferencesStore
{
    private sealed record Values(AnalysisPolicy Policy,PresentationPreferences Presentation);
    public (AnalysisPolicy Policy,PresentationPreferences Presentation) Load()
    {
        if(!File.Exists(path)) return (new(),new());
        try
        {
            using var file=File.OpenRead(path);
            if(file.Length>65536) throw new InvalidDataException("Settings exceed the size limit.");
            var value=JsonSerializer.Deserialize<Values>(file) ?? throw new InvalidDataException("Settings are empty.");
            if(value.Policy is null || value.Presentation is null || !Enum.IsDefined(value.Policy.CommandLineMode) ||
                value.Policy.Revision is <0 or long.MaxValue) throw new InvalidDataException("Invalid analysis policy.");
            return(value.Policy,value.Presentation);
        }
        catch(JsonException e) { throw new InvalidDataException("Settings invalid; online analysis stays disabled until repaired.",e); }
    }
    public void Save(AnalysisPolicy policy,PresentationPreferences presentation)
    {
        var target=Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary=target+"."+Guid.NewGuid()+".tmp";
        try
        {
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            { JsonSerializer.Serialize(file,new Values(policy,presentation)); file.Flush(true); }
            File.Move(temporary,target,true);
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}

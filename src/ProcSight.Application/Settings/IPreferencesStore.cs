using ProcSight.Core.Models;
namespace ProcSight.Application.Settings;
public interface IPreferencesStore
{
    (AnalysisPolicy Policy,PresentationPreferences Presentation) Load();
    void Save(AnalysisPolicy policy,PresentationPreferences presentation);
}

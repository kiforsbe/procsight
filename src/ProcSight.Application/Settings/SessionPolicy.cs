using ProcSight.Core.Models;
namespace ProcSight.Application.Settings;
public sealed record SessionConsent(bool ExternalAnalysis=false,bool CommandLine=false,bool IdentifyingMetadata=false);
public static class SessionPolicy
{
    public static AnalysisPolicy Effective(AnalysisPolicy saved,bool elevated,SessionConsent consent)
    {
        if(!elevated) return saved;
        return saved with {
            ExternalAnalysisAllowed=consent.ExternalAnalysis,
            ResearchEnabled=saved.ResearchEnabled&&consent.ExternalAnalysis,
            CommandLineMode=saved.CommandLineMode==CommandLineMode.OnlineFull&&!consent.CommandLine?CommandLineMode.Off:saved.CommandLineMode,
            ShareIdentifyingMetadata=saved.ShareIdentifyingMetadata&&consent.IdentifyingMetadata&&consent.ExternalAnalysis
        };
    }
}

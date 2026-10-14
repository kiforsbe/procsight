using ProcSight.Core.Models;
namespace ProcSight.Core.Privacy;
public static class OriginPolicy
{
    public static bool CanShare(IReadOnlySet<DataOrigin> origins,AnalysisPolicy policy) =>
        !origins.Contains(DataOrigin.LocalCommandLine) &&
        (!origins.Contains(DataOrigin.OnlineCommandLine) || policy.CommandLineMode==CommandLineMode.OnlineFull);
}

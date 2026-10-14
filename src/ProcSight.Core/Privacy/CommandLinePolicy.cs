using ProcSight.Core.Models;
namespace ProcSight.Core.Privacy;
public static class CommandLinePolicy
{
    public static bool CanDispatch(AnalysisPayload payload,AnalysisPolicy policy) =>
        payload.PolicyRevision==policy.Revision && (payload.CommandLine is null ||
            policy.CommandLineMode != CommandLineMode.Off &&
            (payload.Destination != AnalysisDestination.CloudModel || policy.CommandLineMode==CommandLineMode.OnlineFull));
}

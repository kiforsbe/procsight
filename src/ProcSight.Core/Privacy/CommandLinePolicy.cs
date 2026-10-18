using ProcSight.Core.Models;
namespace ProcSight.Core.Privacy;
public static class CommandLinePolicy
{
    public static bool CanDispatch(AnalysisPayload payload,AnalysisPolicy policy) =>
        payload.PolicyRevision==policy.Revision && (!payload.ResearchEnabled||policy.ResearchEnabled&&AnalysisProjection.ResearchSafe(payload)) && (payload.Destination!=AnalysisDestination.CloudModel||policy.ExternalAnalysisAllowed) && (payload.CommandLine is null ||
            payload.Destination != AnalysisDestination.CloudModel || policy.CommandLineMode==CommandLineMode.OnlineFull) &&
        (payload.Destination!=AnalysisDestination.CloudModel || policy.ShareIdentifyingMetadata ||
            !payload.Metadata.Keys.Concat(payload.Evidence.SelectMany(e=>e.Facts.Keys)).Any(AnalysisProjection.IdentifyingField));
}

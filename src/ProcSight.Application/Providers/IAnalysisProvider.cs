using ProcSight.Core.Models;
namespace ProcSight.Application.Providers;
public interface IAnalysisProvider
{
    Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken ct);
    Task<ProviderResult> AnalyzeAsync(AnalysisPayload payload,CancellationToken ct);
}

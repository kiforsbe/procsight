using ProcSight.Core.Models;
namespace ProcSight.Application.Research;
public interface IResearchProvider { Task<ResearchResult> ResearchAsync(ResearchRequest request,CancellationToken ct); }

using ProcSight.Core.Models;
namespace ProcSight.Application.Budget;
public interface IUsageLedger
{
    Task<bool> TryReserveAsync(UsageKind kind,DateOnly localDate,UsageLimits limits,CancellationToken ct);
    Task<(int Jobs,int Calls)> GetUsageAsync(DateOnly localDate,CancellationToken ct);
}

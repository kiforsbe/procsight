using ProcSight.Core.Models;
namespace ProcSight.Core.Identity;
public static class ParentLinkValidator
{
    public static bool IsVerified(ProcessSnapshot child, ProcessSnapshot parent) =>
        child.ParentPid == parent.Key.Pid && child.Key.StartedAt is { } childTime &&
        parent.Key.StartedAt is { } parentTime && parentTime <= childTime;
}

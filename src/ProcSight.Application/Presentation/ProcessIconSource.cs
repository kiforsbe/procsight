using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
public static class ProcessIconSource
{
    // Local cosmetic source only: selecting configured metadata does not establish loaded-module or trust evidence.
    public static ExecutableMetadata? Select(ResolvedEntity entity)
    {
        var executable=entity.Service is { } service?
            !string.IsNullOrWhiteSpace(service.Implementation?.FullPath)?service.Implementation:service.HostExecutable:
            entity.Process?.Executable;
        return string.IsNullOrWhiteSpace(executable?.FullPath)?null:executable;
    }
    public static string? Key(ResolvedEntity entity)=>Select(entity) is { FullPath: { } path } executable && !string.IsNullOrWhiteSpace(path)?
        path+"|"+executable.FileIdentity:null;
}

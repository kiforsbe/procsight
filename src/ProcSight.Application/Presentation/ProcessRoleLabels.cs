using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
public sealed record RoleLabel(string Label,string Explanation,bool UsesCommandContext=false,bool Specific=false);
public static class ProcessRoleLabels
{
    public static RoleLabel Describe(ResolvedEntity entity)
    {
        var owner=entity.Owner ?? entity.Process?.Executable.Product ?? entity.DisplayName;
        if(entity.Process is not { } process) return new(entity.DisplayName,"Windows service metadata.");
        var command=process.CommandLine?.Raw;
        // Hints remain local display context; only the bounded category enters command evidence.
        if(ProcSight.Core.Rules.LocalCommandRules.Match(process.Executable.Name,command) is { } role)
            return new($"{owner} — {role.Label}","Provisional invocation role from local rule "+role.RuleId+"; identity and safety remain separate.",true,true);
        if(process.ServiceNames.Count>0)
            return new("Service host — "+string.Join(", ",process.ServiceNames),"Services associated with this PID by Windows; no security verdict.",Specific:true);
        if(ProcSight.Core.Rules.KnownTaskCatalog.Match(entity) is { } known)
            return new(known.Label,"Bundled common-task purpose matched verified metadata; exact invocation role and safety may be unknown.");
        return new(entity.DisplayName,"Executable metadata; exact invocation role may be unknown.");
    }
    public static string DisplayName(ResolvedEntity entity,string? assessedLabel=null)
    {
        var local=Describe(entity);
        var generic=assessedLabel is null || string.Equals(assessedLabel,entity.Owner,StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assessedLabel,entity.DisplayName,StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assessedLabel,ProcSight.Core.Rules.KnownTaskCatalog.Match(entity)?.Label,StringComparison.OrdinalIgnoreCase);
        return local.Specific&&generic?local.Label:assessedLabel ?? local.Label;
    }
}

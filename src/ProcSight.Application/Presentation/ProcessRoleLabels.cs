using ProcSight.Core.Models;
namespace ProcSight.Application.Presentation;
public sealed record RoleLabel(string Label,string Explanation,bool UsesCommandContext=false,bool Specific=false);
public static class ProcessRoleLabels
{
    public static RoleLabel Describe(ResolvedEntity entity)
    {
        var owner=entity.Owner ?? entity.Process?.Executable.Product ?? entity.DisplayName;
        if(entity.Process is not { } process) return new(entity.DisplayName,"Windows service metadata.");
        var host=process.Executable.Name.ToLowerInvariant() switch {
            "python.exe" or "pythonw.exe" or "py.exe"=>"Python","node.exe" or "node_repl.exe"=>"Node.js",
            "cmd.exe"=>"Command Prompt","pwsh.exe" or "powershell.exe"=>"PowerShell","dotnet.exe"=>".NET",
            "wscript.exe" or "cscript.exe"=>"Windows Script Host","plexscripthost.exe"=>"Plex Script Host",_=>owner };
        var command=process.CommandLine?.Raw;
        // Hints remain local display context; only the bounded category enters command evidence.
        if(ProcSight.Core.Rules.LocalCommandRules.Match(process.Executable.Name,command) is { } role)
            return new($"{host} — {role.Workload ?? role.Label}","Provisional invocation role from local rule "+role.RuleId+"; identity and safety remain separate.",true,true);
        if(process.ServiceNames.Count>0)
            return new("Service host — "+string.Join(", ",process.ServiceNames),"Services associated with this PID by Windows; no security verdict.",Specific:true);
        if(ProcSight.Core.Rules.KnownTaskCatalog.Match(entity) is { } known) {
            if(process.Executable.Product?.Equals("PowerToys",StringComparison.OrdinalIgnoreCase)==true &&
                process.Executable.Name.StartsWith("PowerToys.",StringComparison.OrdinalIgnoreCase) && process.Executable.Name.Length>14) {
                var component=process.Executable.Name[10..^4];
                component=component switch { "ColorPickerUI"=>"Color Picker","QuickAccess"=>"Quick Access",_=>
                    System.Text.RegularExpressions.Regex.Replace(component,"([a-z])([A-Z])","$1 $2") };
                return new($"{owner} — {component}","Known signed PowerToys component identified by executable/original-file metadata.",Specific:true);
            }
            return new(known.Label,"Bundled common-task purpose matched verified metadata; exact invocation role and safety may be unknown.");
        }
        return new(entity.DisplayName,"Executable metadata; exact invocation role may be unknown.");
    }
    public static string DisplayName(ResolvedEntity entity,string? assessedLabel=null)
    {
        var local=Describe(entity);
        var generic=assessedLabel is null || string.Equals(assessedLabel,entity.Owner,StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assessedLabel,entity.DisplayName,StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assessedLabel,ProcSight.Core.Rules.KnownTaskCatalog.Match(entity)?.Label,StringComparison.OrdinalIgnoreCase);
        var label=local.Specific&&generic?local.Label:assessedLabel ?? local.Label;
        var windows=entity.Process?.Windows;
        var title=windows?.Where(w=>!string.IsNullOrWhiteSpace(w.Title)).OrderByDescending(w=>w.Foreground)
            .ThenBy(w=>w.Minimized).ThenBy(w=>w.Title,StringComparer.Ordinal).FirstOrDefault()?.Title;
        if(title is null) return label;
        title=new string(title.Take(512).Select(c=>char.IsControl(c)?' ':c)
            .Where(c=>System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.Format).ToArray()).Trim();
        var aliases=new[]{entity.Owner,entity.DisplayName,entity.Process?.Executable.Product,entity.Process?.Executable.Name.Replace(".exe","",StringComparison.OrdinalIgnoreCase),label};
        foreach(var alias in aliases.Where(a=>!string.IsNullOrWhiteSpace(a))) {
            if(title.Equals(alias,StringComparison.OrdinalIgnoreCase)) return label;
            foreach(var separator in new[]{" - "," — "," – "})
                if(title.EndsWith(separator+alias,StringComparison.OrdinalIgnoreCase)) title=title[..^(separator.Length+alias!.Length)].Trim();
        }
        return title.Length==0||label.EndsWith(" — "+title,StringComparison.OrdinalIgnoreCase)?label:label+" — "+title;
    }
}

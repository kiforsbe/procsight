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
        // Display-only derived context. Never copy these labels into facts, persistence, or requests.
        if(command is { Length:<=32768 }) {
            var tokens=System.Text.RegularExpressions.Regex.Matches(command,"\"[^\"]*\"|\\S+",System.Text.RegularExpressions.RegexOptions.None,TimeSpan.FromMilliseconds(100))
                .Select(m=>m.Value.Trim('"')).Skip(1).ToArray();
            var roles=new (string Flag,string Role)[]{
                ("--utility-sub-type=network.mojom.NetworkService","Network service"),("--utility-sub-type=audio.mojom.AudioService","Audio service"),
                ("--type=renderer","Page renderer"),("--type=gpu-process","GPU rendering"),
                ("--type=extensionHost","Extension host"),("extensionHostProcess.js","Extension host"),("tsserver.js","TypeScript language server"),
                ("eslintServer.js","JavaScript linting service"),("--utility-sub-type=node.mojom.NodeService","Node.js utility"),("--type=utility","Utility process"),
                ("--type=crashpad-handler","Crash reporting"),("--worker","Background worker")};
            foreach(var (flag,role) in roles) if(tokens.Any(t=>flag.StartsWith("--",StringComparison.Ordinal)?t.Equals(flag,StringComparison.OrdinalIgnoreCase):
                !t.StartsWith("--",StringComparison.Ordinal)&&(t.Equals(flag,StringComparison.OrdinalIgnoreCase)||t.EndsWith("\\"+flag,StringComparison.OrdinalIgnoreCase)||t.EndsWith("/"+flag,StringComparison.OrdinalIgnoreCase))))
                return new($"{owner} — {role}","Provisional role inferred locally from permitted command-line context; not an AI assessment.",true,true);
            var isNode=process.Executable.Name.Equals("node.exe",StringComparison.OrdinalIgnoreCase);
            var isDotnet=process.Executable.Name.Equals("dotnet.exe",StringComparison.OrdinalIgnoreCase);
            if((isNode||isDotnet) && !tokens.Any(t=>t is "-e" or "--eval" or "-p" or "--print")) {
                var script=tokens.FirstOrDefault(t=>!t.StartsWith('-')&&!t.Contains('=') &&
                    (isNode?t.EndsWith(".js",StringComparison.OrdinalIgnoreCase)||t.EndsWith(".mjs",StringComparison.OrdinalIgnoreCase)||t.EndsWith(".cjs",StringComparison.OrdinalIgnoreCase):
                        t.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)));
                if(script is not null) {
                    var name=new string(script.Replace('\\','/').Split('/').Last().Where(c=>!char.IsControl(c)).ToArray());
                    if(name.Length>80) name=name[..80]+"…";
                    return new($"{owner} — runs {name}","Provisional local script/application argument; its precise behavior is not established.",true,true);
                }
            }
        }
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

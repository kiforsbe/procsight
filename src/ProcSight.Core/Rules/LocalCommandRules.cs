using System.Text;
namespace ProcSight.Core.Rules;
public sealed record LocalCommandRole(string Label,string Category,string RuleId);
/// <summary>Bounded declarative hints. They describe invocation, never identity or safety.</summary>
public static class LocalCommandRules
{
    private sealed record SwitchRule(string Token,string Label,string Category);
    private static readonly SwitchRule[] Switches=[
        new("--utility-sub-type=network.mojom.NetworkService","Network service","Utility"),
        new("--utility-sub-type=audio.mojom.AudioService","Audio service","Utility"),
        new("--type=renderer","Page renderer","Renderer"),new("--type=gpu-process","GPU rendering","Utility"),
        new("--type=extensionHost","Extension host","Worker"),
        new("--utility-sub-type=node.mojom.NodeService","Node.js utility","Utility"),
        new("--type=utility","Utility process","Utility"),new("--type=crashpad-handler","Crash reporting","Utility"),
        new("--worker","Background worker","Worker")];
    private static readonly IReadOnlyDictionary<string,string> EntryPoints=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
        ["extensionHostProcess.js"]="Extension host",["tsserver.js"]="TypeScript language server",
        ["typingsInstaller.js"]="TypeScript typings installer",["eslintServer.js"]="JavaScript linting service",
        ["pylance.js"]="Python language server",["Microsoft.CodeAnalysis.LanguageServer.dll"]="C# language server",
        ["VBCSCompiler.dll"]="C# and Visual Basic compiler server",["MSBuild.dll"]="MSBuild project build engine" };
    public static LocalCommandRole? Match(string executable,string? line)
    {
        var tokens=Tokenize(line); if(tokens is null || tokens.Count<2) return null;
        var args=tokens.Skip(1).ToArray(); var exe=executable.ToLowerInvariant();
        var power=exe is "pwsh.exe" or "powershell.exe";
        var node=exe is "node.exe" or "node_repl.exe";
        var python=exe is "python.exe" or "pythonw.exe" or "py.exe";
        // Interpreter payloads and script arguments are opaque: never search them for switches.
        if(power) {
            foreach(var arg in args) {
                if(arg.Equals("-EncodedCommand",StringComparison.OrdinalIgnoreCase)||arg.Equals("-enc",StringComparison.OrdinalIgnoreCase))
                    return new("Encoded PowerShell invocation","CLI","powershell-encoded");
                if(arg.Equals("-Command",StringComparison.OrdinalIgnoreCase)||arg.Equals("-c",StringComparison.OrdinalIgnoreCase))
                    return new("PowerShell command invocation","CLI","powershell-command");
                if(arg.Equals("-File",StringComparison.OrdinalIgnoreCase)||arg.Equals("-f",StringComparison.OrdinalIgnoreCase))
                    return new("PowerShell script invocation","CLI","powershell-file");
            }
            return null;
        }
        if(python) {
            for(var i=0;i<args.Length;i++) {
                if(args[i]=="-c") return new("Python inline invocation","CLI","python-inline");
                if(args[i]=="-m" && i+1<args.Length) return args[i+1] switch {
                    "http.server"=>new("Python HTTP server","Worker","python-http"),
                    "pip"=>new("Python package management","CLI","python-pip"),
                    "pytest"=>new("Python test runner","CLI","python-test"),
                    "ipykernel_launcher"=>new("Jupyter Python kernel","Worker","python-kernel"),
                    _=>new("Python module invocation","CLI","python-module") };
                if(!args[i].StartsWith('-')) break;
            }
        }
        if(exe=="dotnet.exe" && args[0] is "build" or "test" or "run" or "restore" or "publish" or "watch" or "msbuild")
            return new(args[0] switch { "build"=>".NET project build","test"=>".NET test runner","run"=>".NET application launch",
                "restore"=>".NET dependency restore","publish"=>".NET publishing","watch"=>".NET file watcher",_=>"MSBuild project build engine" },"CLI","dotnet-"+args[0]);
        // Runtimes: only an entry point can name a script, not later credential/prompt values.
        if(node || python || exe=="dotnet.exe") {
            var offset=exe=="dotnet.exe" && args[0]=="exec"?1:0;
            for(var i=offset;i<args.Length;i++) {
                var arg=args[i];
                if(arg=="--") { if(++i>=args.Length) return null; arg=args[i]; }
                else if(node && (arg is "-e" or "--eval" or "-p" or "--print" || arg.StartsWith("--eval=",StringComparison.Ordinal)))
                    return new("JavaScript inline invocation","CLI","node-inline");
                else if(arg.StartsWith('-')) {
                    if(node && Switches.FirstOrDefault(r=>r.Token.Equals(arg,StringComparison.OrdinalIgnoreCase)) is { } known)
                        return new(known.Label,known.Category,known.Token);
                    if(arg is "-r" or "--require" or "--import" or "--loader" or "--experimental-loader" or "--title" or "--conditions" or "-C" or
                        "--runtimeconfig" or "--depsfile" or "--additionalprobingpath" or "--fx-version" or "--roll-forward" or "-W" or "-X") { i++; continue; }
                    if(arg.Contains('=') || arg is "--inspect" or "--inspect-brk" or "--no-warnings" or "-u" or "-B" or "-O" or "-OO" ||
                        python && arg.StartsWith("-3",StringComparison.Ordinal)) continue;
                    return null; // Unknown option arity cannot identify an entry point safely.
                }
                var basename=arg.Replace('\\','/').Split('/').Last();
                if(EntryPoints.TryGetValue(basename,out var role)) return new(role,"Worker","entry-"+basename);
                var extension=node?new[]{".js",".mjs",".cjs"}:python?[".py"]:new[]{".dll"};
                if(extension.Any(e=>basename.EndsWith(e,StringComparison.OrdinalIgnoreCase)) && !basename.Contains('=') &&
                    basename.Length is >0 and <=80 && !basename.Any(char.IsControl))
                    return new("runs "+basename,"CLI","runtime-entry");
                break;
            }
            return null;
        }
        var boundary=Array.IndexOf(args,"--"); var switches=boundary<0?args:args[..boundary];
        foreach(var rule in Switches)
            if(switches.Contains(rule.Token,StringComparer.OrdinalIgnoreCase)) return new(rule.Label,rule.Category,rule.Token);
        return null;
    }
    // Windows backslash/quote tokenization; malformed input produces no hint.
    public static IReadOnlyList<string>? Tokenize(string? line)
    {
        if(line is null || line.Length>32768 || line.Contains('\0')) return null;
        var result=new List<string>(); var token=new StringBuilder(); var quoted=false; var active=false;
        for(var i=0;i<line.Length;i++) {
            var c=line[i];
            if(c=='\\') {
                var start=i; while(i<line.Length && line[i]=='\\') i++;
                var count=i-start;
                if(i<line.Length && line[i]=='"') {
                    token.Append('\\',count/2); if(count%2==1) token.Append('"'); else quoted=!quoted;
                } else { token.Append('\\',count); i--; }
                active=true; continue;
            }
            if(c=='"') { quoted=!quoted; active=true; continue; }
            if(char.IsWhiteSpace(c) && !quoted) {
                if(active) { result.Add(token.ToString()); token.Clear(); active=false; }
            } else { token.Append(c); active=true; }
            if(result.Count>1024) return null;
        }
        if(quoted) return null;
        if(active) result.Add(token.ToString());
        return result;
    }
}

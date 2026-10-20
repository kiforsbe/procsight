using System.Text;
namespace ProcSight.Core.Rules;
public sealed record LocalCommandRole(string Label,string Category,string RuleId,string? Workload=null);
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
        var python=exe is "python.exe" or "pythonw.exe" or "py.exe" or "plexscripthost.exe";
        if(exe is "wscript.exe" or "cscript.exe") {
            var target=args.SkipWhile(a=>a is "//nologo" or "//b" or "//i" || a.StartsWith("//T:",StringComparison.OrdinalIgnoreCase)||a.StartsWith("//E:",StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            return target is null?null:Script(target,[".vbs",".js",".wsf"]);
        }
        if(exe=="cmd.exe") return CommandPrompt(line!);
        if(exe=="code.exe") {
            var prefix=args.TakeWhile(arg=>arg.StartsWith('-')&&arg!="--").ToArray();
            foreach(var rule in Switches)
                if(prefix.Contains(rule.Token,StringComparer.OrdinalIgnoreCase)) return new(rule.Label,rule.Category,rule.Token);
            if(!args[0].StartsWith('-')) return Script(args[0],[".js",".mjs",".cjs"]);
            return null;
        }
        // Interpreter payloads and script arguments are opaque: never search them for switches.
        if(power) {
            for(var i=0;i<args.Length;i++) {
                var arg=args[i];
                if(arg.Equals("-EncodedCommand",StringComparison.OrdinalIgnoreCase)||arg.Equals("-enc",StringComparison.OrdinalIgnoreCase))
                    return new("Encoded PowerShell invocation","CLI","powershell-encoded");
                if(arg.Equals("-Command",StringComparison.OrdinalIgnoreCase)||arg.Equals("-c",StringComparison.OrdinalIgnoreCase))
                    return new("PowerShell command invocation","CLI","powershell-command");
                if(arg.Equals("-File",StringComparison.OrdinalIgnoreCase)||arg.Equals("-f",StringComparison.OrdinalIgnoreCase))
                    return i+1<args.Length?Script(args[i+1],[".ps1"]) ?? new("PowerShell script invocation","CLI","powershell-file"):
                        new("PowerShell script invocation","CLI","powershell-file");
            }
            return null;
        }
        if(python) {
            for(var i=0;i<args.Length;i++) {
                if(args[i] is "-W" or "-X") { i++; continue; }
                if(args[i]=="-c") return new("Python inline invocation","CLI","python-inline");
                if(args[i]=="-m" && i+1<args.Length) return args[i+1] switch {
                    "http.server"=>new("Python HTTP server","Worker","python-http"),
                    "pip"=>new("Python package management","CLI","python-pip"),
                    "pytest"=>new("Python test runner","CLI","python-test"),
                    "ipykernel_launcher"=>new("Jupyter Python kernel","Worker","python-kernel"),
                    _=>new("Python module invocation","CLI","python-module",
                        args[i+1].Length<=80 && args[i+1].All(c=>char.IsAsciiLetterOrDigit(c)||c is '_' or '.')?args[i+1]+" (module)":null) };
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
                    if(arg.Contains('=') || arg is "--inspect" or "--inspect-brk" or "--no-warnings" or "--experimental-vm-modules" or "--permission" or "-u" or "-B" or "-O" or "-OO" ||
                        python && arg.StartsWith("-3",StringComparison.Ordinal)) continue;
                    return null; // Unknown option arity cannot identify an entry point safely.
                }
                var extension=node?new[]{".js",".mjs",".cjs"}:python?[".py"]:new[]{".dll"};
                // Only generic runners can delegate to the immediate positional script.
                var entry=Script(arg,extension);
                return entry?.RuleId=="runtime-entry" && IsRunner(arg) && i+1<args.Length?
                    HostedScript(args[i+1],extension) ?? entry:entry;
            }
            return null;
        }
        var boundary=Array.IndexOf(args,"--"); var switches=boundary<0?args:args[..boundary];
        foreach(var rule in Switches)
            if(switches.Contains(rule.Token,StringComparer.OrdinalIgnoreCase)) return new(rule.Label,rule.Category,rule.Token);
        return null;
    }
    private static bool IsRunner(string path)
    {
        var basename=path.Replace('\\','/').Split('/').Last();
        var dot=basename.LastIndexOf('.');
        return dot>0 && basename[..dot].ToLowerInvariant() is
            "runner" or "launcher" or "bootstrap" or "plugin_runner" or "plugin-runner" or
            "script_runner" or "script-runner" or "run_script" or "run-script";
    }
    private static bool GenericEntry(string name)=>IsRunner(name) || name.ToLowerInvariant() is
        "plugin.py" or "main.py" or "__main__.py" or "index.js" or "index.mjs" or "index.cjs" or
        "main.js" or "main.mjs" or "main.cjs" or "plugin.js" or "plugin.mjs" or "plugin.cjs";
    private static LocalCommandRole? HostedScript(string target,string[] extensions)
    {
        // No options, expansions, traversal, URLs, UNC paths, or recursive argument searching.
        var path=target.Replace('\\','/');
        if(path.StartsWith('-') || path.StartsWith('/') || path.Any(c=>char.IsControl(c)||c is
            '=' or '"' or '%' or '!' or '&' or '|' or '<' or '>' or '?' or '*')) return null;
        var parts=path.Split('/');
        if(parts.Any(p=>p.Length==0 || p is "." or ".." || p.EndsWith('.') || p.EndsWith(' ')) ||
            path.Contains(':') && !(path.Length>3 && char.IsAsciiLetter(path[0]) && path[1]==':' &&
                path[2]=='/' && !path[2..].Contains(':'))) return null;
        var role=Script(target,extensions);
        if(role is null || role.RuleId!="runtime-entry") return role;
        var basename=parts[^1];
        if(IsRunner(basename)) return null; // Never follow nested runner chains.
        if(!GenericEntry(basename)) return role;
        if(parts.Length<2) return null;
        var folder=parts[^2];
        // Strip only an exact UUID suffix, never arbitrary instance text or command values.
        if(folder.Length>37 && folder[^37] is '_' or '-' && Guid.TryParseExact(folder[^36..],"D",out _))
            folder=folder[..^37];
        if(folder.Length is 0 or >80 || !folder.Any(char.IsAsciiLetter) ||
            !folder.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_' or '.' or ' ') ||
            Guid.TryParse(folder,out _) || folder.ToLowerInvariant() is
                "src" or "dist" or "lib" or "bin" or "scripts" or "plugins" or "installed" or "app" or "python") return null;
        return new("Hosted script","Worker","runtime-hosted-entry",folder+" — "+basename);
    }
    private static LocalCommandRole? Script(string path,string[] extensions)
    {
        var parts=path.Replace('\\','/').Split('/'); var basename=parts.Last();
        if(!extensions.Any(e=>basename.EndsWith(e,StringComparison.OrdinalIgnoreCase)) || basename.Contains('=') ||
            basename.Length is 0 or >80 || basename.Any(c=>char.IsControl(c)||c is '"' or '%' or '!' or '&' or '|')) return null;
        // Only known extension directory identifiers contribute human-readable context.
        var extension=parts.Zip(parts.Skip(1)).FirstOrDefault(pair=>pair.First.Equals("extensions",StringComparison.OrdinalIgnoreCase)).Second;
        if(extension?.StartsWith("ms-python.vscode-pylance-",StringComparison.OrdinalIgnoreCase)==true)
            return new("Pylance language server","Worker","extension-pylance");
        if(extension?.StartsWith("github.vscode-github-actions-",StringComparison.OrdinalIgnoreCase)==true)
            return new("GitHub Actions language server","Worker","extension-github-actions");
        if(extension is { Length:<=100 } && extension.Contains('.') && extension.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_' or '.')) {
            var version=System.Text.RegularExpressions.Regex.Match(extension,"-[0-9]",System.Text.RegularExpressions.RegexOptions.None,TimeSpan.FromMilliseconds(50));
            var identifier=version.Success?extension[..version.Index]:extension;
            return new("Extension script","Worker","extension-entry",identifier+" — "+basename);
        }
        if(EntryPoints.TryGetValue(basename,out var role)) return new(role,"Worker","entry-"+basename);
        return new("runs "+basename,"CLI","runtime-entry",basename);
    }
    private static LocalCommandRole? CommandPrompt(string line)
    {
        var match=System.Text.RegularExpressions.Regex.Match(line,
            "^\\s*(?:\"[^\"]*\"|\\S+)\\s+(?:(?:/[dsqau]|/[evf]:(?:on|off))\\s+)*(?:/[ck])\\s+(?<payload>.+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(50));
        if(!match.Success) return null;
        var payload=match.Groups["payload"].Value.Trim();
        // Shell operators/expansions make invocation ambiguous; do not evaluate them.
        if(payload.Any(c=>c is '&' or '|' or '<' or '>' or '^' or '%' or '!' or '\r' or '\n')) return null;
        if(payload.StartsWith("\"\"",StringComparison.Ordinal) && payload.EndsWith('"')) payload=payload[1..^1];
        var tokens=Tokenize(payload);
        // /s /c commonly adds an outer quote pair around the whole invocation.
        if(tokens?.Count==1 && payload.StartsWith('"') && payload.EndsWith('"') &&
            !tokens[0].EndsWith(".cmd",StringComparison.OrdinalIgnoreCase) && !tokens[0].EndsWith(".bat",StringComparison.OrdinalIgnoreCase))
            tokens=Tokenize(payload[1..^1]);
        if(tokens is null || tokens.Count==0) return null;
        var offset=tokens[0].Equals("call",StringComparison.OrdinalIgnoreCase)?1:0;
        if(offset>=tokens.Count) return null;
        var target=tokens[offset]; var name=target.Replace('\\','/').Split('/').Last();
        if(name.Equals("npx",StringComparison.OrdinalIgnoreCase)||name.Equals("npx.cmd",StringComparison.OrdinalIgnoreCase)) {
            offset++;
            while(offset<tokens.Count && tokens[offset] is "-y" or "--yes" or "--no" or "--quiet") offset++;
            if(offset>=tokens.Count || tokens[offset].StartsWith('-')) return null;
            var package=tokens[offset]; var version=package.LastIndexOf('@');
            if(version>0) package=package[..version];
            return package switch {
                "@playwright/mcp"=>new("Playwright MCP","Worker","cmd-playwright-mcp"),
                "chrome-devtools-mcp"=>new("Chrome DevTools MCP","Worker","cmd-chrome-devtools-mcp"),
                _=>package.Length is >0 and <=80 && package.All(c=>char.IsAsciiLetterOrDigit(c)||c is '@' or '/' or '-' or '_' or '.')?
                    new("npx package invocation","CLI","cmd-npx-package",package+" (npx)"):null };
        }
        if(name is "playwright-mcp" or "chrome-devtools-mcp")
            return new(name=="playwright-mcp"?"Playwright MCP":"Chrome DevTools MCP","Worker","cmd-mcp-tool");
        return Script(target,[".cmd",".bat"]);
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

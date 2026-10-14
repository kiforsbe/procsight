using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class LocalCommandRulesTests
{
    private static RoleLabel Label(string name,string command)
    {
        var p=Samples.Process(name:name) with { CommandLine=new(command,Samples.Now) };
        return ProcessRoleLabels.Describe(Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(p),[])));
    }
    [Theory]
    [InlineData("python.exe","python.exe -m http.server 8080","Python HTTP server")]
    [InlineData("python.exe","python.exe -m pip install example","Python package management")]
    [InlineData("pwsh.exe","pwsh.exe -EncodedCommand PRIVATE_PAYLOAD","Encoded PowerShell invocation")]
    [InlineData("dotnet.exe","dotnet.exe build example.csproj",".NET project build")]
    public void KnownInvocationsGetLocalRoles(string exe,string command,string expected)=>Assert.Contains(expected,Label(exe,command).Label);
    [Theory]
    [InlineData("pwsh.exe","pwsh.exe -Command \"--type=renderer\"")]
    [InlineData("node.exe","node.exe -e \"--type=renderer\"")]
    [InlineData("python.exe","python.exe -c \"--type=renderer\"")]
    public void ExecutableCodeIsNotScannedAsProcessSwitches(string exe,string command)=>Assert.DoesNotContain("Page renderer",Label(exe,command).Label);
    [Fact] public void ScriptArgumentsCannotTurnTheRuntimeIntoAnInlineInterpreter()=>
        Assert.Contains("Extension host",Label("node.exe","node.exe extensionHostProcess.js --eval PRIVATE_TEXT").Label);
    [Theory]
    [InlineData("node.exe","node.exe --title tsserver.js app.js","TypeScript language server")]
    [InlineData("dotnet.exe","dotnet.exe exec --runtimeconfig MSBuild.dll app.dll","MSBuild project build engine")]
    public void OptionValuesAreNotEntryPoints(string exe,string command,string incorrect)=>Assert.DoesNotContain(incorrect,Label(exe,command).Label);
    [Theory]
    [InlineData("python.exe","python.exe \"C:\\Example Tools\\serve_local.py\" --token SECRET","serve_local.py")]
    [InlineData("py.exe","py.exe -3.13 C:\\Example\\socks5_http_tunneler.py --proxy-file PRIVATE","socks5_http_tunneler.py")]
    [InlineData("cmd.exe","cmd.exe /d /s /c call ./scripts/launch_tools.cmd ./server.mjs","launch_tools.cmd")]
    [InlineData("cmd.exe","cmd.exe /d /s /c \"npx \"\"@playwright/mcp@latest\"\"\"","Playwright MCP")]
    [InlineData("cmd.exe","cmd.exe /d /s /c npx chrome-devtools-mcp@1.9.0","Chrome DevTools MCP")]
    [InlineData("Code.exe","Code.exe \"C:\\Example\\extensions\\ms-python.vscode-pylance-2026.1\\dist\\server.bundle.js\" --token SECRET","Pylance language server")]
    [InlineData("Code.exe","Code.exe \"C:\\Example\\extensions\\github.vscode-github-actions-1.0\\dist\\server-node.js\"","GitHub Actions language server")]
    [InlineData("Code.exe","Code.exe --type=utility --utility-sub-type=network.mojom.NetworkService","Network service")]
    [InlineData("python.exe","python.exe -m example_module --token SECRET","example_module")]
    [InlineData("pwsh.exe","pwsh.exe -NoProfile -File C:\\Example\\sync.ps1 --token SECRET","sync.ps1")]
    [InlineData("cmd.exe","cmd.exe /c \"C:\\Example Tools\\launch.cmd\"","launch.cmd")]
    [InlineData("cmd.exe","cmd.exe /c npx @example/helper@1.2 --token SECRET","@example/helper")]
    [InlineData("cmd.exe","cmd.exe /c npx -y @playwright/mcp@latest","Playwright MCP")]
    [InlineData("cmd.exe","cmd.exe /d /s /c \"\"C:\\Example Tools\\launch.cmd\" PRIVATE\"","launch.cmd")]
    [InlineData("Code.exe","Code.exe \"C:\\Example\\extensions\\ms-python.vscode-pylance-2026.1\\dist\\server.bundle.js\" -- --type=renderer","Pylance language server")]
    [InlineData("PlexScriptHost.exe","PlexScriptHost.exe C:\\Example\\helper.py PRIVATE","helper.py")]
    [InlineData("cscript.exe","cscript.exe //nologo C:\\Example\\helper.vbs PRIVATE","helper.vbs")]
    [InlineData("node.exe","node.exe --experimental-vm-modules C:\\Example\\worker.mjs PRIVATE","worker.mjs")]
    public void WorkloadIsProminentAndArgumentsRemainPrivate(string exe,string command,string workload)
    {
        var result=Label(exe,command);
        Assert.Contains(" — "+workload,result.Label); Assert.DoesNotContain("SECRET",result.Label);
        Assert.DoesNotContain("PRIVATE",result.Label); Assert.DoesNotContain("C:\\",result.Label);
    }
    [Theory]
    [InlineData("cmd.exe","cmd.exe /c echo npx @playwright/mcp@latest")]
    [InlineData("cmd.exe","cmd.exe /c set TOKEN=SECRET & npx @playwright/mcp@latest")]
    [InlineData("python.exe","python.exe -c \"serve_local.py\"")]
    public void TextMentioningAWorkloadIsNotThatWorkload(string exe,string command)
    {
        var result=Label(exe,command);
        Assert.DoesNotContain("Playwright MCP",result.Label); Assert.DoesNotContain("serve_local.py",result.Label);
        Assert.DoesNotContain("SECRET",result.Label);
    }
    [Fact] public void KnownSignedPowerToysComponentsGetHostedNames()
    {
        var process=Samples.Process(name:"PowerToys.FancyZones.exe",product:"PowerToys") with {
            Executable=new("PowerToys.FancyZones.exe",Product:"PowerToys",Signature:SignatureStatus.Verified,
                VerifiedSigner:"Microsoft Corporation",OriginalName:"PowerToys.FancyZones.exe") };
        var entity=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(process),[]));
        Assert.Equal("PowerToys — Fancy Zones",ProcessRoleLabels.DisplayName(entity));
    }
    [Fact] public void GenericHostsStayGroupedWhileChildrenNameTheirWorkloads()
    {
        var first=Samples.Process(1,name:"python.exe",product:"Python") with { CommandLine=new("python.exe first.py",Samples.Now) };
        var second=Samples.Process(2,name:"python.exe",product:"Python") with { CommandLine=new("python.exe second.py",Samples.Now) };
        var snapshot=Samples.Snapshot(first,second);
        var rows=ProcessListProjection.Build(snapshot,new IdentityResolver().Resolve(snapshot,[]),[],new(Grouped:true)).Rows;
        var group=Assert.Single(rows); Assert.Equal("Python",group.GroupName);
        Assert.Contains(group.Members,r=>ProcessRoleLabels.DisplayName(r).Contains(" — first.py"));
        Assert.Contains(group.Members,r=>ProcessRoleLabels.DisplayName(r).Contains(" — second.py"));
    }
}

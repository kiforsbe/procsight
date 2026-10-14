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
}

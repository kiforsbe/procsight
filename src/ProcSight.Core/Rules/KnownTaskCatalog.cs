using ProcSight.Core.Models;
using System.Security.Cryptography;
using System.Text;
namespace ProcSight.Core.Rules;
public sealed record KnownTaskDefinition(string Executable,string Label,string Purpose,IReadOnlyList<string> Signers,
    string SourceUrl,string? Product=null,bool WindowsComponent=false)
{
    public Guid Id=>new(SHA256.HashData(Encoding.UTF8.GetBytes("procsight-common-v1:"+Executable))[..16]);
}
public static class KnownTaskCatalog
{
    private const string Critical="https://learn.microsoft.com/en-us/windows/win32/rstmgr/critical-system-services";
    private static KnownTaskDefinition Windows(string exe,string label,string source=Critical)=>
        new(exe,label,"OSComponent",["Microsoft Windows","Microsoft Corporation","Microsoft Windows Publisher"],source,WindowsComponent:true);
    public static IReadOnlyList<KnownTaskDefinition> Definitions { get; }=[
        Windows("svchost.exe","Windows service host","https://learn.microsoft.com/en-us/windows/application-management/svchost-service-refactoring"),
        Windows("smss.exe","Windows session manager"),Windows("csrss.exe","Windows client/server runtime"),
        Windows("wininit.exe","Windows initialization"),Windows("logonui.exe","Windows sign-in interface"),
        Windows("lsass.exe","Windows authentication","https://learn.microsoft.com/en-us/windows-server/security/windows-authentication/credentials-processes-in-windows-authentication"),
        Windows("services.exe","Windows service manager"),Windows("winlogon.exe","Windows logon"),
        new("chrome.exe","Google Chrome","Application",["Google LLC","Google Inc"],"https://www.chromium.org/developers/design-documents/multi-process-architecture/","Google Chrome"),
        new("msedge.exe","Microsoft Edge","Application",["Microsoft Corporation"],"https://learn.microsoft.com/en-us/deployedge/microsoft-edge-browser-policies/","Microsoft Edge"),
        new("Code.exe","Visual Studio Code","DeveloperTool",["Microsoft Corporation"],"https://code.visualstudio.com/api/advanced-topics/extension-host","Visual Studio Code"),
        new("node.exe","Node.js JavaScript runtime","DeveloperTool",["OpenJS Foundation","Node.js Foundation"],"https://nodejs.org/en/about","Node.js"),
        new("pwsh.exe","PowerShell automation shell","DeveloperTool",["Microsoft Corporation"],"https://learn.microsoft.com/en-us/powershell/scripting/overview","PowerShell"),
        new("dotnet.exe",".NET application host","DeveloperTool",["Microsoft Corporation"],"https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet","Microsoft .NET"),
        new("MSBuild.exe","MSBuild project build engine","DeveloperTool",["Microsoft Corporation"],"https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild","Microsoft")
    ];
    public static KnownTaskDefinition? Match(ResolvedEntity entity)
    {
        if(entity.Process?.Executable is not { Signature:SignatureStatus.Verified,VerifiedSigner:{ } signer } exe) return null;
        return Definitions.FirstOrDefault(d=>d.Executable.Equals(exe.Name,StringComparison.OrdinalIgnoreCase) &&
            d.Signers.Contains(SignerLabel(signer) ?? "",StringComparer.OrdinalIgnoreCase) &&
            (d.WindowsComponent?(exe.OriginalName?.Equals(d.Executable,StringComparison.OrdinalIgnoreCase)==true ||
                exe.OriginalName?.Equals(d.Executable+".mui",StringComparison.OrdinalIgnoreCase)==true) &&
                (exe.FullPath?.Contains(@"\Windows\System32\",StringComparison.OrdinalIgnoreCase)==true||exe.FullPath?.Contains(@"\Windows\SysWOW64\",StringComparison.OrdinalIgnoreCase)==true):
                exe.Product?.Contains(d.Product!,StringComparison.OrdinalIgnoreCase)==true));
    }
    private static string? SignerLabel(string subject)
    {
        if(!subject.Contains('=')) return subject;
        try {
            var decoded=new System.Security.Cryptography.X509Certificates.X500DistinguishedName(subject)
                .Decode(System.Security.Cryptography.X509Certificates.X500DistinguishedNameFlags.UseNewLines);
            return decoded.Split('\n').Select(s=>s.Trim()).FirstOrDefault(s=>s.StartsWith("CN=",StringComparison.OrdinalIgnoreCase))?[3..].Trim().Trim('"');
        } catch(Exception e) when(e is System.Security.Cryptography.CryptographicException or ArgumentException) { return null; }
    }
    public static LocalAnalysis? Evaluate(ResolvedEntity entity,DateTimeOffset now)
    {
        var definition=Match(entity); if(definition is null) return null;
        var output=new Assessment(AssessmentAxis.Purpose,definition.Purpose,
            "Bundled common-task reference matched verified signer and executable metadata. Invocation role and safety remain separate.",
            Confidence.Medium,entity.Evidence.Where(e=>e.Origin==DataOrigin.ShareableMetadata).Select(e=>e.Id).ToList(),now.AddDays(30),[DataOrigin.ShareableMetadata]);
        return new([output],entity.Owner,[definition.Id],[],new(){AssessmentAxis.Security,AssessmentAxis.Optionality},definition.Label,"Known executable purpose");
    }
    public static LocalAnalysis Supplement(ResolvedEntity entity,LocalAnalysis local,KnowledgeEntry knowledge,DateTimeOffset now)
    {
        var common=Evaluate(entity,now); if(common is null) return local;
        var purposeAllowed=!local.Assessments.Any(a=>a.Axis==AssessmentAxis.Purpose) &&
            !local.Issues.Contains("Conflicting Purpose assessments") &&
            !knowledge.Corrections.Any(c=>c.Assessment.Axis==AssessmentAxis.Purpose&&c.Assessment.ExpiresAt>now);
        var axes=local.UnresolvedAxes.ToHashSet(); if(purposeAllowed) axes.Remove(AssessmentAxis.Purpose);
        return local with { Assessments=purposeAllowed?[..local.Assessments,..common.Assessments]:local.Assessments,
            MatchingRuleIds=purposeAllowed?[..local.MatchingRuleIds,..common.MatchingRuleIds]:local.MatchingRuleIds,UnresolvedAxes=axes,
            IdentityLabel=local.IdentityLabel ?? common.IdentityLabel,Role=local.Role ?? common.Role };
    }
}

using ProcSight.Core.Models;
using System.Security.Cryptography;
using System.Text;
namespace ProcSight.Core.Rules;
public sealed record KnownTaskDefinition(string Executable,string Label,string Purpose,IReadOnlyList<string> Signers,
    string SourceUrl,string? Product=null,bool WindowsComponent=false,string? ServiceName=null,
    string? OriginalName=null,bool PerUserService=false,bool PublisherOnly=false,IReadOnlyList<string>? PathScopes=null)
{
    public Guid Id=>new(SHA256.HashData(Encoding.UTF8.GetBytes("procsight-common-v2:"+Executable.ToLowerInvariant()+"|"+Product+"|"+ServiceName))[..16]);
}
public static class KnownTaskCatalog
{
    private const string Critical="https://learn.microsoft.com/en-us/windows/win32/rstmgr/critical-system-services";
    private static KnownTaskDefinition Windows(string exe,string label,string source=Critical)=>
        new(exe,label,"OSComponent",["Microsoft Windows","Microsoft Corporation","Microsoft Windows Publisher"],source,WindowsComponent:true);
    private static readonly IReadOnlyList<KnownTaskDefinition> Baseline=[
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
        new("dotnet.exe",".NET application host","DeveloperTool",["Microsoft Corporation",".NET"],"https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet",".NET"),
        new("MSBuild.exe","MSBuild project build engine","DeveloperTool",["Microsoft Corporation"],"https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild","Microsoft")
    ];
    public static IReadOnlyList<KnownTaskDefinition> Definitions { get; }=Load();
    private static IReadOnlyList<KnownTaskDefinition> Load()
    {
        using var stream=typeof(KnownTaskCatalog).Assembly.GetManifestResourceStream("ProcSight.Core.Rules.common-tasks.json")!;
        var extra=System.Text.Json.JsonSerializer.Deserialize<List<KnownTaskDefinition>>(stream)!;
        var all=Baseline.Concat(extra).ToArray();
        if(all.Select(d=>d.Id).Distinct().Count()!=all.Length || all.Any(d=>d.Signers.Count==0 ||
            !Uri.TryCreate(d.SourceUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https"))
            throw new InvalidOperationException("Invalid bundled catalog.");
        return Array.AsReadOnly(all);
    }
    public static KnownTaskDefinition? Match(ResolvedEntity entity)
    {
        if((entity.Process?.Executable ?? entity.Service?.Implementation) is not { Signature:SignatureStatus.Verified,VerifiedSigner:{ } signer } exe) return null;
        return Definitions.FirstOrDefault(d=>(entity.Service is { } service ? ServiceMatches(d,service.Name):d.ServiceName is null) &&
            d.Executable.Equals(exe.Name,StringComparison.OrdinalIgnoreCase) &&
            d.Signers.Contains(SignerLabel(signer) ?? "",StringComparer.OrdinalIgnoreCase) &&
            (d.OriginalName is null || OriginalMatches(exe.OriginalName,d.OriginalName)) &&
            (d.PublisherOnly ||
            (d.WindowsComponent?OriginalMatches(exe.OriginalName,d.OriginalName ?? d.Executable) &&
                WindowsPathMatches(d,exe.FullPath):
                d.Product is not null && exe.Product?.Equals(d.Product,StringComparison.OrdinalIgnoreCase)==true)));
    }
    private static bool WindowsPathMatches(KnownTaskDefinition d,string? path)
    {
        var scopes=d.PathScopes ?? new[]{"System32","SysWOW64","SystemApps"};
        return scopes.Any(scope=>path?.Contains(scope switch {
            "System32"=>@"\Windows\System32\","SysWOW64"=>@"\Windows\SysWOW64\",
            "SystemApps"=>@"\Windows\SystemApps\","Servicing"=>@"\Windows\servicing\",
            "DefenderPlatform"=>@"\Microsoft\Windows Defender\Platform\",_=>"\0" },StringComparison.OrdinalIgnoreCase)==true);
    }
    private static bool OriginalMatches(string? actual,string expected)
    {
        static string Base(string value)=>value.EndsWith(".mui",StringComparison.OrdinalIgnoreCase)?value[..^4]:value;
        return actual is not null && Base(actual).Equals(Base(expected),StringComparison.OrdinalIgnoreCase);
    }
    private static bool ServiceMatches(KnownTaskDefinition d,string name)=>d.ServiceName is { } expected &&
        (name.Equals(expected,StringComparison.OrdinalIgnoreCase) || d.PerUserService && name.StartsWith(expected+"_",StringComparison.OrdinalIgnoreCase) &&
        name[(expected.Length+1)..] is { Length:>0 and <=16 } suffix && suffix.All(Uri.IsHexDigit));
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
        if(definition.PublisherOnly) return new([],entity.Owner,[definition.Id],[],Enum.GetValues<AssessmentAxis>().ToHashSet(),
            definition.Label,"Authenticated publisher; exact purpose unknown");
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
        var axes=local.UnresolvedAxes.ToHashSet(); if(purposeAllowed && common.Assessments.Count>0) axes.Remove(AssessmentAxis.Purpose);
        return local with { Assessments=purposeAllowed?[..local.Assessments,..common.Assessments]:local.Assessments,
            MatchingRuleIds=purposeAllowed?[..local.MatchingRuleIds,..common.MatchingRuleIds]:local.MatchingRuleIds,UnresolvedAxes=axes,
            IdentityLabel=local.IdentityLabel ?? common.IdentityLabel,Role=local.Role ?? common.Role };
    }
}

using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
namespace ProcSight.Windows.Security;
public sealed record PackagedExecutionContext(bool HasPackageIdentity,bool InspectionSucceeded,bool AllowsElevation,string? FamilyName=null)
{
    public bool CanRestartElevated=>InspectionSucceeded&&(!HasPackageIdentity||AllowsElevation);
    public string? CredentialScope=>!InspectionSucceeded?null:!HasPackageIdentity?"":FamilyName is { Length:>0 }?FamilyName+":":null;
}
public static class PackageContext
{
    private static readonly PackagedExecutionContext Unavailable=new(true,false,false);
    private static readonly string RestrictedSession=Environment.ProcessId+"-"+Guid.NewGuid().ToString("N");
    private static readonly Lazy<PackagedExecutionContext> Current=new(()=> { try { return InspectNative(); } catch { return Unavailable; } });
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern int GetCurrentPackageFullName(ref uint length,StringBuilder? value);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern int GetPackagePathByFullName(string name,ref uint length,StringBuilder? value);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern int GetCurrentPackageFamilyName(ref uint length,StringBuilder? value);
    public static PackagedExecutionContext Inspect(Func<PackagedExecutionContext>? observation=null)
    {
        try { return observation?.Invoke()??Current.Value; } catch { return Unavailable; }
    }
    public static string DataFolder(string? localAppData=null,PackagedExecutionContext? context=null)
    {
        context??=Inspect(); localAppData??=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if(context.InspectionSucceeded&&!context.HasPackageIdentity) return Path.Combine(localAppData,"ProcSight");
        if(context.InspectionSucceeded&&context.FamilyName is { Length:>0 } family&&family.All(c=>char.IsAsciiLetterOrDigit(c)||c is '.' or '_' or '-'))
            return Path.Combine(localAppData,"Packages",family,"LocalState","ProcSight");
        // Unknown package identity must not fall back to somebody's existing app data.
        return Path.Combine(localAppData,"ProcSight.RestrictedSession",RestrictedSession);
    }
    private static PackagedExecutionContext InspectNative()
    {
        uint count=0; var result=GetCurrentPackageFullName(ref count,null);
        if(result==15700) return new(false,true,false); // APPMODEL_ERROR_NO_PACKAGE
        if(result!=122||count is 0 or >32768) return Unavailable;
        var name=new StringBuilder((int)count); if(GetCurrentPackageFullName(ref count,name)!=0) return Unavailable;
        count=0; if(GetPackagePathByFullName(name.ToString(),ref count,null)!=122||count is 0 or >32768) return Unavailable;
        var path=new StringBuilder((int)count); if(GetPackagePathByFullName(name.ToString(),ref count,path)!=0) return Unavailable;
        // Package path comes from Windows, not configuration or current working directory.
        var manifest=Path.Combine(path.ToString(),"AppxManifest.xml");
        using var file=new FileStream(manifest,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length>512*1024) return Unavailable;
        using var reader=new StreamReader(file); var context=ParseManifest(reader.ReadToEnd());
        count=0; if(GetCurrentPackageFamilyName(ref count,null)!=122||count is 0 or >32768) return Unavailable;
        var family=new StringBuilder((int)count); if(GetCurrentPackageFamilyName(ref count,family)!=0) return Unavailable;
        return context with { FamilyName=family.ToString() };
    }
    public static PackagedExecutionContext ParseManifest(string xml)
    {
        try {
            if(xml.Length>512*1024) return Unavailable;
            using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=512*1024 });
            var document=XDocument.Load(reader); XNamespace foundation="http://schemas.microsoft.com/appx/manifest/foundation/windows10";
            XNamespace restricted="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";
            if(document.Root?.Name!=foundation+"Package") return Unavailable;
            var allowed=document.Root.Element(foundation+"Capabilities")?.Elements(restricted+"Capability").Any(e=>(string?)e.Attribute("Name")=="allowElevation")==true;
            return new(true,true,allowed);
        } catch { return Unavailable; }
    }
}

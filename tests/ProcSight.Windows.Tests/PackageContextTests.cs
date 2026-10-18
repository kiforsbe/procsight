using ProcSight.Windows.Security;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class PackageContextTests
{
    [Fact] public void PackagedDataAndCredentialsDoNotImplicitlyReuseUnpackagedState()
    {
        var context=new PackagedExecutionContext(true,true,true,"ProcSight.Development_fixture");
        Assert.Equal("ProcSight.Development_fixture:",context.CredentialScope);
        Assert.Equal(Path.Combine("C:\\Users\\Fixture\\AppData\\Local","Packages","ProcSight.Development_fixture","LocalState","ProcSight"),
            PackageContext.DataFolder("C:\\Users\\Fixture\\AppData\\Local",context));
        Assert.Equal("",new PackagedExecutionContext(false,true,false).CredentialScope);
        Assert.Null(new PackagedExecutionContext(true,false,false).CredentialScope);
    }
    [Theory]
    [InlineData(false,true,false,true)] [InlineData(true,true,false,false)]
    [InlineData(true,true,true,true)] [InlineData(false,false,true,false)]
    public void ElevationRequiresKnownUnpackagedOrDeclaredCapability(bool packaged,bool inspected,bool allowed,bool expected)=>
        Assert.Equal(expected,new PackagedExecutionContext(packaged,inspected,allowed).CanRestartElevated);
    [Fact] public void InspectionFailuresAreRestrictive()=>Assert.False(PackageContext.Inspect(()=>throw new IOException()).CanRestartElevated);
    [Theory] [InlineData("runFullTrust",false)] [InlineData("allowElevation",true)]
    public void OnlyDeclaredRestrictedCapabilityAllowsElevation(string capability,bool expected)
    {
        var xml=$"""<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:r="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"><Capabilities><r:Capability Name="{capability}" /></Capabilities></Package>""";
        Assert.Equal(expected,PackageContext.ParseManifest(xml).CanRestartElevated);
        Assert.False(PackageContext.ParseManifest(xml.Replace("restrictedcapabilities","untrusted")).CanRestartElevated);
    }
    [Fact] public void ExternalEntityAndOversizedManifestAreRejected()
    {
        Assert.False(PackageContext.ParseManifest("<!DOCTYPE Package SYSTEM 'file:///private'><Package/>").InspectionSucceeded);
        Assert.False(PackageContext.ParseManifest(new string('x',600000)).InspectionSucceeded);
    }
}

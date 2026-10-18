using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Core.Tests;
public sealed class ServiceIdentityTests
{
    private static ExecutableMetadata Module(SignatureStatus status=SignatureStatus.Verified)=>new("dhcpcore.dll",@"C:\Windows\System32\dhcpcore.dll",
        "fixture-hash",Product:"Microsoft® Windows® Operating System",Signature:status,VerifiedSigner:"Microsoft Windows",OriginalName:"dhcpcore.dll");
    private static ResolvedEntity Entity(ExecutableMetadata? implementation,ExecutableMetadata? host=null)=>Assert.Single(new IdentityResolver().Resolve(
        new(Guid.NewGuid(),Samples.Now,[],[new("Dhcp","DHCP Client","Running","Auto",42,"svchost.exe -k example",ObservationState.Available,implementation,host)]),[]));
    [Fact] public void ServiceGetsItsOwnVerifiedModuleIdentityAndPurpose()
    {
        var e=Entity(Module()); Assert.Equal("Microsoft Windows",e.Facts.GetValueOrDefault("signer"));
        Assert.NotNull(KnownTaskCatalog.Match(e));
    }
    [Fact] public void SignedHostCannotAuthenticateUnsignedOrUnknownServiceModule()
    {
        var host=new ExecutableMetadata("svchost.exe",Signature:SignatureStatus.Verified,VerifiedSigner:"Microsoft Windows");
        Assert.Null(KnownTaskCatalog.Match(Entity(null,host)));
        Assert.Null(KnownTaskCatalog.Match(Entity(Module(SignatureStatus.Unsigned),host)));
        Assert.False(Entity(null,host).Facts.ContainsKey("signer"));
    }
    [Fact] public void ModuleReplacementInvalidatesServiceKnowledgeScope()
    {
        var a=Entity(Module()); var b=Entity(Module() with { ContentHash="replacement",Signature=SignatureStatus.Invalid });
        Assert.NotEqual(a.Identity,b.Identity); Assert.NotEqual(a.Evidence[0].Id,b.Evidence[0].Id);
    }
    [Fact] public void ShareableServiceIdentityDoesNotDigestPrivateArguments()
    {
        var a=Entity(Module());
        var snapshot=new MachineSnapshot(Guid.NewGuid(),Samples.Now,[],[a.Service! with { BinaryPath="svchost.exe --secret=different" }]);
        var b=Assert.Single(new IdentityResolver().Resolve(snapshot,[]));
        Assert.Equal(a.Identity,b.Identity);
        Assert.DoesNotContain("different",System.Text.Json.JsonSerializer.Serialize(b));
        Assert.DoesNotContain("different",b.Service!.ToString());
    }
    [Theory]
    [InlineData("TrustedInstaller","TrustedInstaller.exe",@"C:\Windows\servicing\TrustedInstaller.exe")]
    [InlineData("WinDefend","MsMpEng.exe",@"C:\ProgramData\Microsoft\Windows Defender\Platform\1.0\MsMpEng.exe")]
    public void WindowsServiceLocationsHaveScopedCoverage(string name,string file,string path)
    {
        var module=Module() with { Name=file,FullPath=path,OriginalName=file,
            VerifiedSigner=name=="WinDefend"?"Microsoft Windows Publisher":"Microsoft Windows" };
        var service=new ServiceSnapshot(name,name,"Running","Auto",null,null,ObservationState.Available,module);
        var entity=Assert.Single(new IdentityResolver().Resolve(new(Guid.NewGuid(),Samples.Now,[],[service]),[]));
        Assert.NotNull(KnownTaskCatalog.Match(entity));
        entity=entity with { Service=service with { Implementation=module with { FullPath=@"C:\Unrelated\"+file } } };
        Assert.Null(KnownTaskCatalog.Match(entity));
    }
    [Theory]
    [InlineData("BluetoothUserService",true)]
    [InlineData("BluetoothUserService_ab12",true)]
    [InlineData("BluetoothUserService_not-an-instance",false)]
    public void PerUserRulesUsePortableTemplateNamesAndConstrainedInstanceSuffixes(string name,bool matches)
    {
        var module=Module() with { Name="Microsoft.Bluetooth.UserService.dll",FullPath=@"C:\Windows\System32\Microsoft.Bluetooth.UserService.dll",
            OriginalName="Microsoft.Bluetooth.UserService.dll" };
        var service=new ServiceSnapshot(name,name,"Running","Auto",null,null,ObservationState.Available,module);
        var entity=Assert.Single(new IdentityResolver().Resolve(new(Guid.NewGuid(),Samples.Now,[],[service]),[]));
        Assert.Equal(matches,KnownTaskCatalog.Match(entity) is not null);
    }
}

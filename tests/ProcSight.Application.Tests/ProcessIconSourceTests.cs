using ProcSight.Application.Presentation;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class ProcessIconSourceTests
{
    private static ResolvedEntity Service(ExecutableMetadata? implementation=null,ExecutableMetadata? host=null)=>
        new(new("fixture-service"),"Fixture service",null,null,
            new("Fixture","Fixture service","Running","Auto",42,null,ObservationState.Available,implementation,host),[],[],[]);
    [Fact] public void ServiceUsesImplementationRatherThanSharedHost()
    {
        var entity=Service(new("fixture.dll",@"C:\Fixture\fixture.dll",FileIdentity:"module-v1"),
            new("host.exe",@"C:\Fixture\host.exe",FileIdentity:"host-v1"));
        Assert.Equal(@"C:\Fixture\fixture.dll",ProcessIconSource.Select(entity)?.FullPath);
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData(" ")]
    public void ServiceWithoutImplementationPathUsesConfiguredHost(string? path)
    {
        var entity=Service(new("fixture.dll",path),new("host.exe",@"C:\Fixture\host.exe"));
        Assert.Equal(@"C:\Fixture\host.exe",ProcessIconSource.Select(entity)?.FullPath);
    }
    [Fact] public void UnavailableServiceFilesKeepFallback()
    {
        Assert.Null(ProcessIconSource.Key(Service(new("fixture.dll"),new("host.exe"))));
    }
    [Fact] public void ProcessUsesItsOwnExecutable()
    {
        var entity=new ResolvedEntity(new("fixture-process"),"Fixture",null,
            Samples.Process() with { Executable=new("fixture.exe",@"C:\Fixture\fixture.exe",FileIdentity:"process-v1") },null,[],[],[]);
        Assert.Equal(@"C:\Fixture\fixture.exe|process-v1",ProcessIconSource.Key(entity));
    }
    [Fact] public void ServiceIconKeyChangesWhenSelectedFileChanges()
    {
        var host=new ExecutableMetadata("host.exe",@"C:\Fixture\host.exe",FileIdentity:"host-v1");
        var original=Service(null,host);
        var enriched=Service(new("fixture.dll",@"C:\Fixture\fixture.dll",FileIdentity:"module-v1"),host);
        var replaced=Service(new("fixture.dll",@"C:\Fixture\fixture.dll",FileIdentity:"module-v2"),host);
        Assert.Equal(@"C:\Fixture\host.exe|host-v1",ProcessIconSource.Key(original));
        Assert.Equal(@"C:\Fixture\fixture.dll|module-v1",ProcessIconSource.Key(enriched));
        Assert.Equal(@"C:\Fixture\fixture.dll|module-v2",ProcessIconSource.Key(replaced));
        Assert.Null(ProcessIconSource.Key(Service()));
    }
    [Fact] public void UnselectedHostChangesDoNotInvalidateImplementationIcon()
    {
        var implementation=new ExecutableMetadata("fixture.dll",@"C:\Fixture\fixture.dll",FileIdentity:"module-v1");
        var entity=Service(implementation,new("host.exe",@"C:\Fixture\host.exe",FileIdentity:"host-v2"));
        Assert.Equal(@"C:\Fixture\fixture.dll|module-v1",ProcessIconSource.Key(entity));
    }
}

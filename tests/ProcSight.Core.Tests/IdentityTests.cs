using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Core.Tests;
public sealed class IdentityTests
{
    [Fact] public void SameNodeDifferentParentsStaySeparate()
    {
        var entities = new IdentityResolver().Resolve(Samples.Snapshot(
            Samples.Process(1, name:"a.exe", product:"App A", started:Samples.Now.AddMinutes(-1)),
            Samples.Process(2, name:"b.exe", product:"App B", started:Samples.Now.AddMinutes(-1)),
            Samples.Process(3, 1), Samples.Process(4, 2)), []);
        Assert.NotEqual(entities[2].Identity, entities[3].Identity);
        Assert.Equal("App A", entities[2].Owner);
    }
    [Fact] public void ParentStartedAfterChildIsUnverified()
    {
        var entity = new IdentityResolver().Resolve(Samples.Snapshot(
            Samples.Process(1, product:"Wrong owner", started:Samples.Now.AddMinutes(1)), Samples.Process(2,1)), [])[1];
        Assert.Null(entity.Owner);
    }
    [Fact] public void CompanyNameDoesNotVerifySigner()
    {
        var process = Samples.Process() with { Executable = new("unknown.exe", Company:"Microsoft") };
        var entity = new IdentityResolver().Resolve(Samples.Snapshot(process), [])[0];
        Assert.False(entity.Facts.ContainsKey("signer"));
    }
    [Fact] public void AccessDeniedLeavesOwnershipUnknown() => Assert.Null(new IdentityResolver()
        .Resolve(Samples.Snapshot(Samples.Process() with { Executable = new("unknown.exe") }), [])[0].Owner);
    [Fact] public void ShellLauncherDoesNotOwnApplication()
    {
        var entity=new IdentityResolver().Resolve(Samples.Snapshot(
            Samples.Process(1,name:"explorer.exe",product:"Microsoft Windows",started:Samples.Now.AddMinutes(-1)),
            Samples.Process(2,parent:1,product:"App A")),[])[1];
        Assert.Equal("App A",entity.Owner);
    }
    [Fact] public void SameNamedOwnersAtDifferentPathsStaySeparate()
    {
        var first=Samples.Process(1,product:"Same app",started:Samples.Now.AddMinutes(-1));
        var second=Samples.Process(2,product:"Same app",started:Samples.Now.AddMinutes(-1)) with {
            Executable=first.Executable with { FullPath=@"C:\Other\node.exe" } };
        var entities=new IdentityResolver().Resolve(Samples.Snapshot(first,second,Samples.Process(3,1),Samples.Process(4,2)),[]);
        Assert.NotEqual(entities[2].Identity,entities[3].Identity);
    }
    [Fact] public void GenericWindowsProductUsesDescriptiveMetadata()
    {
        var process=Samples.Process(product:"Microsoft Windows Operating System") with {
            Executable=Samples.Process().Executable with { Product="Microsoft Windows Operating System",Description="Windows Search Indexer" } };
        var entity=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(process),[]));
        Assert.Equal("Windows Search Indexer",entity.DisplayName);
    }
}

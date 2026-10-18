using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class ExecutableEnrichmentTests
{
    [Fact] public async Task ActualWindowsHostMatchesBundledReference()
    {
        var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"svchost.exe");
        var metadata=await new ExecutableEnricher().EnrichAsync(new ProcessMetadataReader().Read("svchost.exe",path),default);
        var now=DateTimeOffset.UtcNow;
        var process=new ProcessSnapshot(new(1,now,null),null,metadata,new(Observation<double>.Pending(),Observation<long>.Pending(),
            Observation<double>.Pending(),Observation<double>.Pending(),now,null),[]);
        var entity=Assert.Single(new ProcSight.Core.Identity.IdentityResolver().Resolve(new(Guid.NewGuid(),now,[process],[]),[]));
        Assert.True(ProcSight.Core.Rules.KnownTaskCatalog.Evaluate(entity,now) is not null,$"Signature={metadata.Signature}; signer={metadata.VerifiedSigner}; original={metadata.OriginalName}; product={metadata.Product}");
    }
    [Fact] public async Task ChangedFileInvalidatesHash()
    {
        var path = Path.GetTempFileName();
        try
        {
            var enricher = new ExecutableEnricher();
            await File.WriteAllTextAsync(path,"first");
            var first = await enricher.EnrichAsync(new("fixture",path),default);
            await File.WriteAllTextAsync(path,"different content");
            var second = await enricher.EnrichAsync(new("fixture",path),default);
            Assert.NotNull(first.ContentHash); Assert.NotEqual(first.ContentHash,second.ContentHash);
        }
        finally { File.Delete(path); }
    }
    [Fact] public async Task MicrosoftBinaryVerifiesWithoutCompanyInference()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"kernel32.dll");
        var result = await new ExecutableEnricher().EnrichAsync(new("kernel32.dll",path),default);
        Assert.Equal(SignatureStatus.Verified,result.Signature);
        Assert.Contains("Microsoft",result.VerifiedSigner!);
    }
}

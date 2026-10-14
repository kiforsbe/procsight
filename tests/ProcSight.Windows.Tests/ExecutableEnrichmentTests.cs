using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class ExecutableEnrichmentTests
{
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

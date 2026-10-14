using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Core.Tests;
public sealed class CatalogExpansionTests
{
    [Fact] public void CatalogCoversInstalledFamiliesWithUniqueScopedIds()
    {
        Assert.True(KnownTaskCatalog.Definitions.Count>=120);
        Assert.Equal(KnownTaskCatalog.Definitions.Count,KnownTaskCatalog.Definitions.Select(d=>d.Id).Distinct().Count());
    }
    [Fact] public void DotnetCertificateAndProductAreRecognized()
    {
        var p=Samples.Process(name:"dotnet.exe") with { Executable=new("dotnet.exe",Product:".NET",
            Signature:SignatureStatus.Verified,VerifiedSigner:"CN=.NET, O=Microsoft Corporation, C=US",OriginalName:".NET Host") };
        var e=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(p),[]));
        Assert.Equal(".NET application host",KnownTaskCatalog.Match(e)?.Label);
    }
    [Fact] public void PublisherReferencePreservesUnknownPurposeAndAiFallback()
    {
        var p=Samples.Process(name:"codex.exe") with { Executable=new("codex.exe",Signature:SignatureStatus.Verified,VerifiedSigner:"OpenAI OpCo, LLC") };
        var e=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(p),[]));
        var result=KnownTaskCatalog.Evaluate(e,Samples.Now)!;
        Assert.Empty(result.Assessments); Assert.Contains(AssessmentAxis.Purpose,result.UnresolvedAxes);
        var local=new LocalAnalysis([],null,[],[],Enum.GetValues<AssessmentAxis>().ToHashSet());
        Assert.Contains(AssessmentAxis.Purpose,KnownTaskCatalog.Supplement(e,local,KnowledgeEntry.Empty(e.Identity),Samples.Now).UnresolvedAxes);
        Assert.Null(KnownTaskCatalog.Match(e with { Process=p with { Executable=p.Executable with { Signature=SignatureStatus.Unsigned } } }));
    }
}

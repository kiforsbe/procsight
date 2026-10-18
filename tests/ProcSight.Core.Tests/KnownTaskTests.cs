using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Core.Rules;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Core.Tests;
public sealed class KnownTaskTests
{
    private static ResolvedEntity Host(SignatureStatus signature=SignatureStatus.Verified,string original="svchost.exe")
    {
        var p=Samples.Process(name:"svchost.exe") with { Executable=new("svchost.exe",@"C:\Windows\System32\svchost.exe",
            Product:"Microsoft® Windows® Operating System",Signature:signature,VerifiedSigner:"Microsoft Windows",OriginalName:original) };
        return Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(p),[]));
    }
    [Fact] public void KnownWindowsTaskGetsPurposeButNoSafetyVerdict()
    {
        var result=KnownTaskCatalog.Evaluate(Host(),Samples.Now);
        Assert.NotNull(result); Assert.Equal("Windows service host",result.IdentityLabel);
        Assert.Single(result.Assessments); Assert.Equal(AssessmentAxis.Purpose,result.Assessments[0].Axis);
        Assert.Contains(AssessmentAxis.Security,result.UnresolvedAxes);
    }
    [Theory] [InlineData(SignatureStatus.Unsigned,"svchost.exe")] [InlineData(SignatureStatus.Verified,"different.exe")]
    public void FamiliarNameAloneCannotActivateSharedRule(SignatureStatus status,string original)=>
        Assert.Null(KnownTaskCatalog.Evaluate(Host(status,original),Samples.Now));
    [Fact] public void CatalogIsMachineIndependentAndAttributable()
    {
        Assert.True(KnownTaskCatalog.Definitions.Count>=15);
        Assert.All(KnownTaskCatalog.Definitions,d=>Assert.StartsWith("https://",d.SourceUrl));
        var e=Host() with { Process=Host().Process! with { Executable=Host().Process!.Executable with { FullPath=@"D:\Windows\System32\svchost.exe" } } };
        Assert.NotNull(KnownTaskCatalog.Evaluate(e,Samples.Now));
    }
    [Fact] public void VerifiedCertificateSubjectIsMatchedAsStructuredIssuerIdentity()
    {
        var e=Host(); e=e with { Process=e.Process! with { Executable=e.Process!.Executable with {
            VerifiedSigner="CN=Microsoft Windows, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" } } };
        Assert.NotNull(KnownTaskCatalog.Evaluate(e,Samples.Now));
    }
    [Fact] public void SharedCatalogCannotResolveConflictingLearnedPurpose()
    {
        var local=new LocalAnalysis([],null,[],["Conflicting Purpose assessments"],Enum.GetValues<AssessmentAxis>().ToHashSet());
        var result=KnownTaskCatalog.Supplement(Host(),local,KnowledgeEntry.Empty(Host().Identity),Samples.Now);
        Assert.Empty(result.Assessments); Assert.Contains(AssessmentAxis.Purpose,result.UnresolvedAxes);
    }
}

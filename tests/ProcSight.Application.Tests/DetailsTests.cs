using ProcSight.Application.Presentation;
using ProcSight.Core.Identity;
using ProcSight.Core.Models;
using ProcSight.Tests;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class DetailsTests
{
    [Theory] [InlineData(CommandLineMode.Off)] [InlineData(CommandLineMode.LocalOnly)] [InlineData(CommandLineMode.OnlineFull)]
    public void DetailsShowVerbatimCollectedCommandAndIdentityEvidence(CommandLineMode mode)
    {
        var process=Samples.Process() with { CommandLine=new("python.exe \"C:\\Example\\script.py\" --token SYNTHETIC_SECRET",Samples.Now),
            Executable=new("python.exe",@"C:\Example\python.exe","fixture-hash",Product:"Python",Signature:SignatureStatus.Verified,
                VerifiedSigner:"Fixture Publisher",OriginalName:"python.exe") };
        var entity=Assert.Single(new IdentityResolver().Resolve(Samples.Snapshot(process),[]));
        var details=ProcessDetails.FormatIdentity(entity,mode);
        Assert.Contains(process.CommandLine.Raw,details); Assert.Contains("Fixture Publisher",details);
        Assert.Contains("fixture-hash",details); Assert.Contains("Original filename",details);
    }
}

using System.ComponentModel;
using System.Text.Json;
using ProcSight.Core.Models;
using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class MetadataAccessTests
{
    [Fact] public void DeniedFileVersionReadKeepsExecutablePathAndSafeReason()
    {
        var reader=new ProcessMetadataReader(_=>throw new Win32Exception(5,"private provider detail"));
        var value=reader.Read("protected.exe",@"C:\synthetic\protected.exe");
        Assert.Equal("protected.exe",value.Name);
        Assert.Equal(@"C:\synthetic\protected.exe",value.FullPath);
        Assert.Null(value.Version);
        Assert.Equal(new MetadataIssue("FileMetadata",MetadataUnavailableReason.AccessDenied),Assert.Single(value.MetadataIssues!));
        Assert.DoesNotContain("private provider detail",value.ToString());
        Assert.DoesNotContain("MetadataIssues",JsonSerializer.Serialize(value));
    }
    [Fact] public void RestrictedServicePropertyKeepsSuccessfulFieldsAndLaterServiceRows()
    {
        object? Read(string field)=>field switch {
            "Name"=>"SyntheticService", "DisplayName"=>"Synthetic service", "State"=>"Running",
            "StartMode"=>"Manual", "ProcessId"=>42,
            "PathName"=>throw new UnauthorizedAccessException("private path and account"), _=>null };
        var row=ServiceEnumerator.ReadRow(Read)!;
        Assert.Equal("SyntheticService",row.Name);
        Assert.Equal(42,row.HostPid);
        Assert.Equal("Running",row.State);
        Assert.Null(row.BinaryPath);
        Assert.Equal(ObservationState.Unavailable,row.AccessState);
        Assert.Equal(new MetadataIssue("PathName",MetadataUnavailableReason.AccessDenied),Assert.Single(row.MetadataIssues!));
        Assert.DoesNotContain("MetadataIssues",JsonSerializer.Serialize(row));
        Assert.NotNull(ServiceEnumerator.ReadRow(field=>field=="Name"?"LaterService":null));
    }
    [Fact] public async Task MissingFileEnrichmentPreservesKnownMetadata()
    {
        var original=new ExecutableMetadata("synthetic.exe",Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".exe"),Version:"1.2",Product:"Synthetic");
        var result=await new ExecutableEnricher().EnrichAsync(original,default);
        Assert.Equal(original.Version,result.Version);
        Assert.Equal(original.Product,result.Product);
        Assert.Null(result.ContentHash);
        Assert.All(result.MetadataIssues!,issue=>Assert.Equal(MetadataUnavailableReason.NotFound,issue.Reason));
        Assert.Contains(result.MetadataIssues!,issue=>issue.Field=="ContentHash");
        Assert.Contains(result.MetadataIssues!,issue=>issue.Field=="Signature");
    }
    [Theory]
    [InlineData(5,MetadataUnavailableReason.AccessDenied)]
    [InlineData(87,MetadataUnavailableReason.ReadFailed)]
    [InlineData(2,MetadataUnavailableReason.NotFound)]
    public void NativeFailuresUseExactSafeReasonWithoutInferringOwnership(int error,MetadataUnavailableReason expected)
        =>Assert.Equal(expected,MetadataAccess.NativeReason(error));
}

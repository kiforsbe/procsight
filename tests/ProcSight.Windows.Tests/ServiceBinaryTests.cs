using ProcSight.Windows.Collection;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class ServiceBinaryTests
{
    [Theory]
    [InlineData("\"C:\\Example App\\service.exe\" --secret=value","C:\\Example App\\service.exe")]
    [InlineData("C:\\Windows\\System32\\svchost.exe -k netsvcs","C:\\Windows\\System32\\svchost.exe")]
    [InlineData("\\\\server\\share\\service.exe",null)]
    [InlineData("C:\\Example App\\service.exe -x",null)]
    [InlineData("\"C:\\unterminated.exe",null)]
    public void ConfiguredPathIsLocalUnambiguousAndContainsNoArguments(string line,string? expected)=>
        Assert.Equal(expected,ServiceBinaryReader.ExecutablePath(line));
}

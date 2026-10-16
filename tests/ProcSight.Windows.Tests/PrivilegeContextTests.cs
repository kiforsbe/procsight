using System.Security.Principal;
using ProcSight.Windows.Security;
using Xunit;
namespace ProcSight.Windows.Tests;
public sealed class PrivilegeContextTests
{
    [Fact] public void ActiveTokenMatchesWindowsPrincipalWithoutOriginalUserArguments()
    {
        using var identity=WindowsIdentity.GetCurrent();
        Assert.Equal(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator),PrivilegeContext.IsElevated);
    }
}

using ProcSight.Application.Presentation;
using Xunit;
namespace ProcSight.Application.Tests;
public sealed class NavigationTests
{
    [Fact] public void SelectingServicesChangesCurrentPage()
    {
        var navigation = new NavigationState();
        navigation.Select(PageId.Services);
        Assert.Equal(PageId.Services, navigation.Current);
    }
}

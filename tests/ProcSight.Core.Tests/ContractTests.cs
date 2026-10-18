using ProcSight.Core.Models;
using Xunit;
namespace ProcSight.Core.Tests;
public sealed class ContractTests
{
    [Fact] public void MissingStartTimeRequiresSnapshotScope() =>
        Assert.Throws<ArgumentException>(() => new ProcessKey(42, null, null));
    [Fact] public void MissingStartTimeAcceptsSnapshotScope() =>
        Assert.NotNull(new ProcessKey(42, null, Guid.NewGuid()).SnapshotScope);
}

using System.Text;
using Xunit;
using ProcSight.Application.Settings;
using ProcSight.Infrastructure.Authentication;

namespace ProcSight.Infrastructure.Tests;

public class ChatGptAuthTests
{
    [Fact]
    public void ProtectedRecordsRoundTripLargeUnicodeAndCleanOldChunks()
    {
        var credentials = new MemoryCredentials();
        var records = new ChatGptProtectedStore(credentials);
        var text = string.Concat(Enumerable.Repeat("å🙂", 3000));
        records.Set("test", text);
        Assert.Equal(text, records.Get("test"));
        Assert.All(credentials.Values.Values, v => Assert.True(Encoding.UTF8.GetByteCount(v) <= 2560));
        records.Set("test", "replacement");
        Assert.Equal("replacement", records.Get("test"));
        Assert.Equal(2, credentials.Values.Count);
        records.Remove("test");
        Assert.Empty(credentials.Values);
    }

    [Fact]
    public void InterruptedWritePreservesPreviousGeneration()
    {
        var credentials = new MemoryCredentials();
        var records = new ChatGptProtectedStore(credentials);
        records.Set("test", "original");
        credentials.FailAfter = 1;
        Assert.Throws<InvalidOperationException>(() => records.Set("test", new string('x', 6000)));
        Assert.Equal("original", records.Get("test"));
        Assert.Equal(2, credentials.Values.Count);
    }

    [Fact]
    public void HostIdentitySurvivesRestartAndUnknownAccountIsRejected()
    {
        var credentials = new MemoryCredentials();
        var first = new ChatGptSessionService(credentials);
        var second = new ChatGptSessionService(credentials);
        Assert.Equal(first.HostId, second.HostId);
        Assert.StartsWith("urn:uuid:", first.HostId);
        Assert.Empty(second.Accounts);
        Assert.Throws<ArgumentException>(() => second.SelectAccount("unknown"));
    }

    internal sealed class MemoryCredentials : ICredentialStore
    {
        public Dictionary<string, string> Values { get; } = [];
        public int? FailAfter { get; set; }
        public string? Get(string key) => Values.GetValueOrDefault(key);
        public void Set(string key, string value)
        {
            if (FailAfter is int count) { if (count == 0) { FailAfter = null; throw new InvalidOperationException("Synthetic storage failure"); } FailAfter = count - 1; }
            Values[key] = value;
        }
        public void Remove(string key) => Values.Remove(key);
    }
}

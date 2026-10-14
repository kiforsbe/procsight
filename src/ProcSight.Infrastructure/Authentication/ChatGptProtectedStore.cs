using System.Text;
using System.Text.Json;
using ProcSight.Application.Settings;

namespace ProcSight.Infrastructure.Authentication;

/// <summary>Generation-addressed records; the protected index is published only after all chunks exist.</summary>
public sealed class ChatGptProtectedStore(ICredentialStore credentials)
{
    private sealed record Index(string Generation, int Count);
    private const int ChunkCharacters = 2000;
    private static string Key(string key) => "chatgpt:" + key;
    private Index? ReadIndex(string key)
    {
        var value = credentials.Get(Key(key));
        if (value is null) return null;
        var index = JsonSerializer.Deserialize<Index>(value);
        if (index is null || !Guid.TryParseExact(index.Generation, "N", out _) || index.Count is < 1 or > 128)
            throw new InvalidOperationException("Protected ChatGPT record is invalid.");
        return index;
    }
    private static string Part(string key, Index index, int i) => Key(key) + ":" + index.Generation + ":" + i;
    public string? Get(string key) => ChatGptSessionLock.Run("ProcSight.ChatGpt.Credentials", () => GetCore(key));
    private string? GetCore(string key)
    {
        var index = ReadIndex(key);
        if (index is null) return null;
        var value = new StringBuilder();
        for (var i = 0; i < index.Count; i++) value.Append(credentials.Get(Part(key, index, i)) ?? throw new InvalidOperationException("Protected ChatGPT record is incomplete."));
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value.ToString())); }
        catch (FormatException) { throw new InvalidOperationException("Protected ChatGPT record is invalid."); }
    }
    public void Set(string key, string value) => ChatGptSessionLock.Run("ProcSight.ChatGpt.Credentials", () => { SetCore(key, value); return true; });
    private void SetCore(string key, string value)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        var index = new Index(Guid.NewGuid().ToString("N"), Math.Max(1, (encoded.Length + ChunkCharacters - 1) / ChunkCharacters));
        if (index.Count > 128) throw new InvalidOperationException("Protected ChatGPT record is too large.");
        var old = ReadIndex(key);
        var written = 0;
        try
        {
            for (var i = 0; i < index.Count; i++) { credentials.Set(Part(key, index, i), encoded.Substring(i * ChunkCharacters, Math.Min(ChunkCharacters, encoded.Length - i * ChunkCharacters))); written++; }
            credentials.Set(Key(key), JsonSerializer.Serialize(index));
        }
        catch { for (var i = 0; i < written; i++) credentials.Remove(Part(key, index, i)); throw; }
        if (old is not null) for (var i = 0; i < old.Count; i++) credentials.Remove(Part(key, old, i));
    }
    public void Remove(string key) => ChatGptSessionLock.Run("ProcSight.ChatGpt.Credentials", () => { RemoveCore(key); return true; });
    private void RemoveCore(string key)
    {
        var index = ReadIndex(key);
        credentials.Remove(Key(key));
        if (index is not null) for (var i = 0; i < index.Count; i++) credentials.Remove(Part(key, index, i));
    }
}

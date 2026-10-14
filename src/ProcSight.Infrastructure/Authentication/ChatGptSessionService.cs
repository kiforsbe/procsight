using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProcSight.Application.Settings;
using ProcSight.Core.Models;

namespace ProcSight.Infrastructure.Authentication;

public sealed record ChatGptAccount(string Id, string Label, string ClientId);
public sealed record ChatGptModel(string Slug, string DisplayName);
public sealed class ChatGptAuthenticationException(string message, ProviderErrorKind kind = ProviderErrorKind.Auth) : Exception(message)
{
    public ProviderErrorKind Kind { get; } = kind;
}

/// <summary>Public-client OAuth sessions with protected local registrations and fixed OpenAI endpoints.</summary>
public sealed class ChatGptSessionService
{
    private const string Issuer = "https://auth.openai.com";
    private const string Resource = "https://api.openai.com/v1";
    private const string Scope = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct";
    private readonly ChatGptProtectedStore store;
    private readonly HttpClient http;
    // Shared across service instances in this process. The desktop owns one session service.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private sealed record Catalog(string HostId, string? ActiveAccountId, List<ChatGptAccount> Accounts);
    private sealed record Session(string Issuer, string Subject, string ClientId, string? IdToken, string? AccessToken, string? RefreshToken, string Scopes, DateTimeOffset Expires);
    private Catalog catalog;
    public IReadOnlyList<ChatGptAccount> Accounts => catalog.Accounts.ToArray();
    public string? ActiveAccountId => catalog.ActiveAccountId;
    public string HostId => catalog.HostId;
    public ChatGptSessionService(ICredentialStore credentials, HttpClient? httpClient = null)
    {
        store = new(credentials);
        http = httpClient ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
        catalog = ChatGptSessionLock.Run("ProcSight.ChatGpt.Credentials", () =>
        {
            var saved = store.Get("catalog");
            if (saved is not null) return JsonSerializer.Deserialize<Catalog>(saved) ?? throw Error("Protected account catalog is invalid.");
            var created = new Catalog("urn:uuid:" + Guid.NewGuid(), null, []);
            store.Set("catalog", JsonSerializer.Serialize(created)); return created;
        });
    }
    private static ChatGptAuthenticationException Error(string text) => new(text);
    private void SaveCatalog() => store.Set("catalog", JsonSerializer.Serialize(catalog));
    private void ReloadCatalog() => catalog = JsonSerializer.Deserialize<Catalog>(store.Get("catalog") ?? throw Error("Protected account catalog is missing.")) ?? throw Error("Protected account catalog is invalid.");
    private Session? ReadSession(string id) => store.Get("account:" + id) is { } json ? JsonSerializer.Deserialize<Session>(json) : null;
    private void SaveSession(string id, Session session) => store.Set("account:" + id, JsonSerializer.Serialize(session));
    private ChatGptAccount Account(string id) => catalog.Accounts.FirstOrDefault(a => a.Id == id) ?? throw new ArgumentException("Unknown ChatGPT account.", nameof(id));
    public void SelectAccount(string accountId) => ChatGptSessionLock.Run("ProcSight.ChatGpt.Sessions", () => { ReloadCatalog(); Account(accountId); catalog = catalog with { ActiveAccountId = accountId }; SaveCatalog(); return true; });
    public Task<ChatGptAccount> SignInAsync(Func<Uri, Task> launchBrowser, CancellationToken cancellationToken = default) => ChatGptSessionLock.RunAsync(() => SignInCore(null, launchBrowser, cancellationToken), cancellationToken);
    public Task<ChatGptAccount> ReauthorizeAsync(string accountId, Func<Uri, Task> launchBrowser, CancellationToken cancellationToken = default) => ChatGptSessionLock.RunAsync(() => { ReloadCatalog(); return SignInCore(Account(accountId), launchBrowser, cancellationToken); }, cancellationToken);
    private async Task<ChatGptAccount> SignInCore(ChatGptAccount? returning, Func<Uri, Task> launch, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        await Gate.WaitAsync(timeout.Token);
        try
        {
            ReloadCatalog();
            var previous = returning is null ? null : ReadSession(returning.Id);
            var state = Random(); var nonce = Random(); var verifier = Random();
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(4);
            var redirect = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/auth/callback";
            var parameters = new Dictionary<string, string> { ["client_id"] = returning?.ClientId ?? "dynamic_agent_client", ["ext_agent_host_id"] = HostId, ["response_type"] = "code", ["redirect_uri"] = redirect, ["scope"] = Scope, ["resource"] = Resource, ["state"] = state, ["nonce"] = nonce, ["code_challenge_method"] = "S256", ["code_challenge"] = Base64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))) };
            if (returning is null) parameters["agent_name_hint"] = "ProcSight";
            else if (previous?.IdToken is { } hint) parameters["id_token_hint"] = hint;
            var callbackTask = ReceiveCallback(listener, timeout.Token);
            await launch(new Uri(Issuer + "/api/accounts/authorize?" + string.Join("&", parameters.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value))))).WaitAsync(timeout.Token);
            var callback = await callbackTask;
            if (!callback.TryGetValue("state", out var returnedState) || !FixedEqual(state, returnedState)) throw Error("ChatGPT callback state was invalid.");
            if (callback.ContainsKey("error")) throw Error("ChatGPT authorization was declined or unavailable.");
            if (!callback.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code)) throw Error("ChatGPT authorization code was missing.");
            var client = callback.GetValueOrDefault("client_id") ?? returning?.ClientId;
            if (string.IsNullOrWhiteSpace(client) || client == "dynamic_agent_client" || client.Length > 512 || returning is not null && client != returning.ClientId) throw Error("ChatGPT registration did not match the requested account.");
            using var token = await Post("/api/accounts/oauth/token", new() { ["grant_type"] = "authorization_code", ["client_id"] = client, ["code"] = code, ["code_verifier"] = verifier, ["redirect_uri"] = redirect, ["resource"] = Resource }, timeout.Token);
            var idToken = Required(token.RootElement, "id_token");
            var identity = await ValidateIdentity(idToken, client, nonce, timeout.Token);
            if (previous is not null && (previous.Issuer != Issuer || previous.Subject != identity.Subject)) throw Error("ChatGPT identity did not match the saved account.");
            var session = Tokens(token.RootElement, client, identity.Subject, idToken, null);
            var id = returning?.Id ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Issuer + "\n" + identity.Subject + "\n" + client))).ToLowerInvariant();
            var account = new ChatGptAccount(id, (identity.Email ?? "ChatGPT account") + " (" + id[..8] + ")", client);
            SaveSession(id, session);
            catalog.Accounts.RemoveAll(a => a.Id == id); catalog.Accounts.Add(account);
            catalog = catalog with { ActiveAccountId = id }; SaveCatalog();
            return account;
        }
        catch (OperationCanceledException) { throw; }
        catch (ChatGptAuthenticationException) { throw; }
        catch { throw Error("ChatGPT sign-in failed. Try signing in again."); }
        finally { Gate.Release(); }
    }
    public Task<string?> GetAccessTokenAsync(string accountId, CancellationToken cancellationToken = default) => ChatGptSessionLock.RunAsync(() => GetAccessTokenCore(accountId, cancellationToken), cancellationToken);
    private async Task<string?> GetAccessTokenCore(string accountId, CancellationToken cancellationToken)
    {
        ReloadCatalog();
        Account(accountId);
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var session = ReadSession(accountId);
            if (session?.AccessToken is null) return null;
            if (session.Expires > DateTimeOffset.UtcNow.AddMinutes(2)) return session.AccessToken;
            if (session.RefreshToken is null) throw Error("ChatGPT session expired. Sign in again.");
            using var result = await Post("/api/accounts/oauth/token", new() { ["grant_type"] = "refresh_token", ["client_id"] = session.ClientId, ["refresh_token"] = session.RefreshToken, ["resource"] = Resource }, cancellationToken);
            var next = Tokens(result.RootElement, session.ClientId, session.Subject, session.IdToken, session);
            SaveSession(accountId, next); return next.AccessToken;
        }
        catch (OperationCanceledException) { throw; }
        catch (ChatGptAuthenticationException) { throw; }
        catch { throw Error("ChatGPT session renewal failed. Sign in again."); }
        finally { Gate.Release(); }
    }
    public async Task<IReadOnlyList<ChatGptModel>> ListModelsAsync(string accountId, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(accountId, cancellationToken) ?? throw Error("Sign in to the selected ChatGPT account.");
        using var request = new HttpRequestMessage(HttpMethod.Get, Resource + "/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var json = await Send(request, cancellationToken, 2 * 1024 * 1024);
        if (!json.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array || models.GetArrayLength() > 512) throw Error("ChatGPT model catalog was invalid.");
        return models.EnumerateArray().Where(m => m.TryGetProperty("visibility", out var v) && v.GetString() == "list").Select(m => new ChatGptModel(Required(m, "slug"), Required(m, "display_name"))).ToArray();
    }
    public Task<bool> SignOutAsync(string accountId, CancellationToken cancellationToken = default) => ChatGptSessionLock.RunAsync(() => SignOutCore(accountId, cancellationToken), cancellationToken);
    private async Task<bool> SignOutCore(string accountId, CancellationToken cancellationToken)
    {
        ReloadCatalog();
        Account(accountId); await Gate.WaitAsync(cancellationToken);
        try
        {
            var session = ReadSession(accountId); var revoked = session?.RefreshToken is null;
            try
            {
                if (session?.RefreshToken is { } refresh)
                {
                    using var configRequest = new HttpRequestMessage(HttpMethod.Get, Issuer + "/.well-known/openid-configuration");
                    using var config = await Send(configRequest, cancellationToken);
                    var endpoint = AuthEndpoint(Required(config.RootElement, "revocation_endpoint"));
                    for (var attempt = 0; attempt < 3; attempt++)
                    {
                        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refresh, ["token_type_hint"] = "refresh_token", ["client_id"] = session.ClientId }) };
                        try
                        {
                            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                            if (response.StatusCode == HttpStatusCode.OK) { revoked = true; break; }
                            if ((int)response.StatusCode < 500) break;
                        }
                        catch (HttpRequestException) when (attempt < 2) { }
                        await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
                    }
                }
            }
            catch { revoked = false; }
            finally
            {
                if (session is not null) SaveSession(accountId, session with { AccessToken = null, RefreshToken = null, IdToken = null });
                if (catalog.ActiveAccountId == accountId) { catalog = catalog with { ActiveAccountId = null }; SaveCatalog(); }
            }
            return revoked;
        }
        finally { Gate.Release(); }
    }
    private static Session Tokens(JsonElement root, string client, string subject, string? idToken, Session? previous)
    {
        var scopes = root.TryGetProperty("scope", out var granted) ? granted.GetString() ?? "" : previous?.Scopes ?? "";
        var set = scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!set.Contains("chatgpt.tokens.use.direct") || !set.Contains("resource.invoke") || !set.Contains("offline_access")) throw Error("ChatGPT plan usage permission was not granted.");
        if (!string.Equals(Required(root, "token_type"), "Bearer", StringComparison.OrdinalIgnoreCase) || !root.TryGetProperty("expires_in", out var expires) || !expires.TryGetInt32(out var seconds) || seconds is <= 0 or > 604800) throw Error("ChatGPT token response was invalid.");
        return new(Issuer, subject, client, idToken, Required(root, "access_token"), root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : previous?.RefreshToken, scopes, DateTimeOffset.UtcNow.AddSeconds(seconds));
    }
    private async Task<(string Subject, string? Email)> ValidateIdentity(string jwt, string client, string nonce, CancellationToken ct)
    {
        var parts = jwt.Split('.'); if (parts.Length != 3) throw Error("ChatGPT identity token was invalid.");
        using var header = JsonDocument.Parse(Decode(parts[0])); using var payload = JsonDocument.Parse(Decode(parts[1]));
        if (Required(header.RootElement, "alg") != "RS256") throw Error("ChatGPT identity signature was unsupported.");
        using var configRequest = new HttpRequestMessage(HttpMethod.Get, Issuer + "/.well-known/openid-configuration");
        using var config = await Send(configRequest, ct);
        if (Required(config.RootElement, "issuer") != Issuer) throw Error("ChatGPT identity issuer was invalid.");
        using var keysRequest = new HttpRequestMessage(HttpMethod.Get, AuthEndpoint(Required(config.RootElement, "jwks_uri")));
        using var keys = await Send(keysRequest, ct);
        var kid = Required(header.RootElement, "kid");
        var matching = keys.RootElement.GetProperty("keys").EnumerateArray().Where(k => k.TryGetProperty("kid", out var keyId) && keyId.GetString() == kid).ToArray();
        if (matching.Length != 1 || Required(matching[0], "kty") != "RSA") throw Error("ChatGPT identity signing key was unavailable.");
        using var rsa = RSA.Create(); rsa.ImportParameters(new RSAParameters { Modulus = Decode(Required(matching[0], "n")), Exponent = Decode(Required(matching[0], "e")) });
        if (rsa.KeySize < 2048 || !rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw Error("ChatGPT identity signature was invalid.");
        var p = payload.RootElement;
        var aud = p.GetProperty("aud");
        var audienceMatches = aud.ValueKind == JsonValueKind.String ? aud.GetString() == client : aud.ValueKind == JsonValueKind.Array && aud.EnumerateArray().Any(a => a.ValueKind == JsonValueKind.String && a.GetString() == client);
        if (Required(p, "iss") != Issuer || !audienceMatches || p.GetProperty("exp").GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() || !FixedEqual(Required(p, "nonce"), nonce)) throw Error("ChatGPT identity claims were invalid.");
        if (p.TryGetProperty("nbf", out var nbf) && nbf.GetInt64() > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 30) throw Error("ChatGPT identity claims were invalid.");
        if (aud.ValueKind == JsonValueKind.Array && aud.GetArrayLength() > 1 && (!p.TryGetProperty("azp", out var azp) || azp.GetString() != client)) throw Error("ChatGPT identity audience was invalid.");
        return (Required(p, "sub"), p.TryGetProperty("email", out var email) ? email.GetString() : null);
    }
    private static Uri AuthEndpoint(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "auth.openai.com" || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0) throw Error("ChatGPT identity endpoint was invalid.");
        return uri;
    }
    private Task<JsonDocument> Post(string path, Dictionary<string, string> values, CancellationToken ct) => PostCore(new HttpRequestMessage(HttpMethod.Post, Issuer + path) { Content = new FormUrlEncodedContent(values) }, ct);
    private async Task<JsonDocument> PostCore(HttpRequestMessage request, CancellationToken ct) { using (request) return await Send(request, ct); }
    private async Task<JsonDocument> Send(HttpRequestMessage request, CancellationToken ct, int maximumBytes = 256 * 1024)
    {
        try { return await SendCore(request, ct, maximumBytes); }
        catch (HttpRequestException) { throw new ChatGptAuthenticationException("ChatGPT could not be reached. Try again later.", ProviderErrorKind.Transient); }
        catch (IOException) { throw new ChatGptAuthenticationException("ChatGPT connection was interrupted. Try again later.", ProviderErrorKind.Transient); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ChatGptAuthenticationException("ChatGPT request timed out. Try again later.", ProviderErrorKind.Transient); }
    }
    private async Task<JsonDocument> SendCore(HttpRequestMessage request, CancellationToken ct, int maximumBytes)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(45));
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token); }
        catch (HttpRequestException) { throw new ChatGptAuthenticationException("ChatGPT could not be reached. Try again later.", ProviderErrorKind.Transient); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ChatGptAuthenticationException("ChatGPT request timed out. Try again later.", ProviderErrorKind.Transient); }
        using var ownedResponse = response;
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests) throw new ChatGptAuthenticationException("ChatGPT usage limit reached. Check ChatGPT Settings → Usage.", ProviderErrorKind.RateLimited);
            if ((int)response.StatusCode >= 500) throw new ChatGptAuthenticationException("ChatGPT is temporarily unavailable. Try again later.", ProviderErrorKind.Transient);
            throw Error("ChatGPT session requires sign-in or plan authorization.");
        }
        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token); using var memory = new MemoryStream(); var buffer = new byte[8192];
        int count; while ((count = await stream.ReadAsync(buffer, timeout.Token)) != 0) { if (memory.Length + count > maximumBytes) throw Error("ChatGPT response was too large."); memory.Write(buffer, 0, count); }
        try { return JsonDocument.Parse(memory.ToArray()); } catch (JsonException) { throw Error("ChatGPT response was invalid."); }
    }
    private static async Task<Dictionary<string, string>> ReceiveCallback(TcpListener listener, CancellationToken ct)
    {
        using var socket = await listener.AcceptTcpClientAsync(ct); using var stream = socket.GetStream();
        using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct); readTimeout.CancelAfter(TimeSpan.FromSeconds(10));
        var bytes = new List<byte>(); var buffer = new byte[1];
        while (bytes.Count < 16384)
        {
            if (await stream.ReadAsync(buffer, readTimeout.Token) == 0) throw Error("ChatGPT callback was incomplete.");
            bytes.Add(buffer[0]); if (bytes.Count >= 4 && bytes[^4] == 13 && bytes[^3] == 10 && bytes[^2] == 13 && bytes[^1] == 10) break;
        }
        var line = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n")[0].Split(' ');
        const string body = "Return to ProcSight to view the authorization result.";
        var response = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nCache-Control: no-store\r\nConnection: close\r\nContent-Length: {body.Length}\r\n\r\n{body}");
        await stream.WriteAsync(response, ct);
        if (bytes.Count >= 16384 || line.Length != 3 || line[0] != "GET" || !line[1].StartsWith("/auth/callback?", StringComparison.Ordinal)) throw Error("ChatGPT callback was invalid.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in line[1][15..].Split('&'))
        {
            var pair = part.Split('=', 2); if (pair.Length != 2 || !result.TryAdd(Uri.UnescapeDataString(pair[0]), Uri.UnescapeDataString(pair[1].Replace('+', ' ')))) throw Error("ChatGPT callback parameters were invalid.");
        }
        return result;
    }
    private static string Required(JsonElement element, string property) => element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 and <= 65536 } text ? text : throw Error("ChatGPT response omitted required data.");
    private static string Random() => Base64(RandomNumberGenerator.GetBytes(32));
    private static bool FixedEqual(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    private static string Base64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
}

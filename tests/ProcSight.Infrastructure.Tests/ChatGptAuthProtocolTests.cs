using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProcSight.Infrastructure.Authentication;
using ProcSight.Core.Models;
using Xunit;

namespace ProcSight.Infrastructure.Tests;

public class ChatGptAuthProtocolTests
{
    [Fact]
    public async Task SignInValidatesSignedIdentityAndPersistsRotatingSession()
    {
        using var fixture = new Fixture();
        var store = new ChatGptAuthTests.MemoryCredentials();
        var service = new ChatGptSessionService(store, fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        Assert.StartsWith("person@example.invalid (", account.Label);
        Assert.Equal("oaiapp_synthetic", account.ClientId);
        Assert.Equal(account.Id, service.ActiveAccountId);
        Assert.Equal("access-synthetic", await service.GetAccessTokenAsync(account.Id));
        var models = await service.ListModelsAsync(account.Id);
        Assert.Single(models);
        Assert.Equal("synthetic-model", models[0].Slug);
        var restored = new ChatGptSessionService(store, fixture.Client);
        Assert.Equal(account.Id, Assert.Single(restored.Accounts).Id);
        Assert.True(await restored.SignOutAsync(account.Id));
        Assert.Null(await restored.GetAccessTokenAsync(account.Id));
        Assert.Single(restored.Accounts);
    }

    [Theory]
    [InlineData("state")]
    [InlineData("nonce")]
    [InlineData("signature")]
    [InlineData("scope")]
    [InlineData("audience")]
    [InlineData("expiry")]
    [InlineData("issuer")]
    public async Task InvalidAuthorizationNeverPublishesAccount(string invalid)
    {
        using var fixture = new Fixture { Invalid = invalid };
        var service = new ChatGptSessionService(new ChatGptAuthTests.MemoryCredentials(), fixture.Client);
        var error = await Assert.ThrowsAsync<ChatGptAuthenticationException>(() => service.SignInAsync(fixture.Launch));
        Assert.DoesNotContain("access-synthetic", error.ToString());
        Assert.Empty(service.Accounts);
    }

    [Fact]
    public async Task ReturningRegistrationRejectsDifferentIssuedClient()
    {
        using var fixture = new Fixture();
        var service = new ChatGptSessionService(new ChatGptAuthTests.MemoryCredentials(), fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        fixture.Invalid = "client";
        await Assert.ThrowsAsync<ChatGptAuthenticationException>(() => service.ReauthorizeAsync(account.Id, fixture.Launch));
        Assert.Equal("access-synthetic", await service.GetAccessTokenAsync(account.Id));
    }

    [Fact]
    public async Task ConcurrentRenewalsUseLatestRotatedSessionOnce()
    {
        using var fixture = new Fixture { ExpiresSeconds = 1 };
        var store = new ChatGptAuthTests.MemoryCredentials();
        var service = new ChatGptSessionService(store, fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        fixture.ExpiresSeconds = 3600;
        var second = new ChatGptSessionService(store, fixture.Client);
        var tokens = await Task.WhenAll(service.GetAccessTokenAsync(account.Id), second.GetAccessTokenAsync(account.Id));
        Assert.All(tokens, token => Assert.Equal("access-synthetic", token));
        Assert.Equal(1, fixture.RefreshCount);
    }

    [Fact]
    public async Task ReplacementRefreshTokenSurvivesRestartAndIsUsedForNextRenewal()
    {
        using var fixture = new Fixture { ExpiresSeconds = 1, RotateTokens = true };
        var store = new ChatGptAuthTests.MemoryCredentials();
        var service = new ChatGptSessionService(store, fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        Assert.Equal("access-rotated-1", await service.GetAccessTokenAsync(account.Id));
        var restarted = new ChatGptSessionService(store, fixture.Client);
        fixture.ExpiresSeconds = 3600;
        Assert.Equal("access-rotated-2", await restarted.GetAccessTokenAsync(account.Id));
        Assert.Equal(new[] { "refresh-synthetic", "refresh-rotated-1" }, fixture.RefreshTokensReceived);
        Assert.Equal("access-rotated-2", await service.GetAccessTokenAsync(account.Id));
        Assert.Equal(2, fixture.RefreshCount);
    }

    [Fact]
    public async Task ModelCatalogAcceptsLargeIgnoredInstructionsAndReturnsVisibleModels()
    {
        using var fixture = new Fixture { CatalogPadding = 600 * 1024 };
        var service = new ChatGptSessionService(new ChatGptAuthTests.MemoryCredentials(), fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        var model = Assert.Single(await service.ListModelsAsync(account.Id));
        Assert.Equal("synthetic-model", model.Slug);
        Assert.Equal("Synthetic", model.DisplayName);
    }

    [Fact]
    public async Task ModelCatalogRejectsResponseLargerThanTwoMiB()
    {
        using var fixture = new Fixture { CatalogPadding = 2 * 1024 * 1024 };
        var service = new ChatGptSessionService(new ChatGptAuthTests.MemoryCredentials(), fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        var error = await Assert.ThrowsAsync<ChatGptAuthenticationException>(() => service.ListModelsAsync(account.Id));
        Assert.Equal("ChatGPT response was too large.", error.Message);
    }

    [Fact]
    public async Task FailedRevocationStillClearsCredentialsAndRetainsRegistration()
    {
        using var fixture = new Fixture { RevokeStatus = HttpStatusCode.BadRequest };
        var service = new ChatGptSessionService(new ChatGptAuthTests.MemoryCredentials(), fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        Assert.False(await service.SignOutAsync(account.Id));
        Assert.Null(await service.GetAccessTokenAsync(account.Id));
        Assert.Null(service.ActiveAccountId);
        Assert.Single(service.Accounts);
    }

    [Fact]
    public async Task CancelledAuthorizationDoesNotPublishAccount()
    {
        using var fixture = new Fixture();
        var service = new ChatGptSessionService(new ChatGptAuthTests.MemoryCredentials(), fixture.Client);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SignInAsync(_ => { cancellation.Cancel(); return Task.CompletedTask; }, cancellation.Token));
        Assert.Empty(service.Accounts);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, ProviderErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ProviderErrorKind.Transient)]
    [InlineData(HttpStatusCode.BadRequest, ProviderErrorKind.Auth)]
    public async Task RenewalFailurePreservesCorrectRecoveryKind(HttpStatusCode status, ProviderErrorKind kind)
    {
        using var fixture = new Fixture { ExpiresSeconds = 1 };
        var service = new ChatGptSessionService(new ChatGptAuthTests.MemoryCredentials(), fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        fixture.TokenStatus = status;
        var error = await Assert.ThrowsAsync<ChatGptAuthenticationException>(() => service.GetAccessTokenAsync(account.Id));
        Assert.Equal(kind, error.Kind);
    }

    [Fact]
    public async Task SignedOutReturningAccountStillRejectsDifferentSubject()
    {
        using var fixture = new Fixture();
        var service = new ChatGptSessionService(new ChatGptAuthTests.MemoryCredentials(), fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        await service.SignOutAsync(account.Id);
        fixture.Invalid = "subject";
        await Assert.ThrowsAsync<ChatGptAuthenticationException>(() => service.ReauthorizeAsync(account.Id, fixture.Launch));
        Assert.Null(await service.GetAccessTokenAsync(account.Id));
    }

    [Fact]
    public async Task RevocationRetriesNetworkFailureWithAvailableRefreshToken()
    {
        using var fixture = new Fixture { RevokeNetworkFailures = 1 };
        var service = new ChatGptSessionService(new ChatGptAuthTests.MemoryCredentials(), fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        Assert.True(await service.SignOutAsync(account.Id));
        Assert.Equal(2, fixture.RevokeCount);
    }

    [Fact]
    public async Task DistinctRegistrationsSurviveStaleServiceAndHaveUniqueLabels()
    {
        using var firstFixture = new Fixture();
        using var secondFixture = new Fixture { ClientId = "oaiapp_second" };
        var store = new ChatGptAuthTests.MemoryCredentials();
        var first = new ChatGptSessionService(store, firstFixture.Client);
        var stale = new ChatGptSessionService(store, secondFixture.Client);
        var one = await first.SignInAsync(firstFixture.Launch);
        var two = await stale.SignInAsync(secondFixture.Launch);
        Assert.NotEqual(one.Label, two.Label);
        var restored = new ChatGptSessionService(store, firstFixture.Client);
        Assert.Equal(2, restored.Accounts.Count);
        first.SelectAccount(one.Id);
        Assert.Equal(2, first.Accounts.Count);
    }

    [Fact]
    public async Task InterruptedRefreshBodyIsTransientAndRedacted()
    {
        using var fixture = new Fixture { ExpiresSeconds = 1 };
        var service = new ChatGptSessionService(new ChatGptAuthTests.MemoryCredentials(), fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        fixture.FaultTokenBody = true;
        var error = await Assert.ThrowsAsync<ChatGptAuthenticationException>(() => service.GetAccessTokenAsync(account.Id));
        Assert.Equal(ProviderErrorKind.Transient, error.Kind);
        Assert.DoesNotContain("sensitive", error.ToString());
    }

    [Fact]
    public async Task CancellationWhileNamedSessionLockIsHeldIsBounded()
    {
        using var fixture = new Fixture();
        var service = new ChatGptSessionService(new ChatGptAuthTests.MemoryCredentials(), fixture.Client);
        var account = await service.SignInAsync(fixture.Launch);
        using var acquired = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var thread = new Thread(() => { using var mutex = new Mutex(false, "ProcSight.ChatGpt.Sessions"); mutex.WaitOne(); acquired.Set(); release.Wait(); mutex.ReleaseMutex(); });
        thread.Start();
        try
        {
            Assert.True(acquired.Wait(TimeSpan.FromSeconds(5)));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAccessTokenAsync(account.Id, cancellation.Token));
        }
        finally { release.Set(); thread.Join(); }
    }

    internal sealed class Fixture : HttpMessageHandler
    {
        private readonly RSA rsa = RSA.Create(2048);
        public HttpClient Client { get; }
        public string? Invalid { get; set; }
        public string ClientId { get; set; } = "oaiapp_synthetic";
        public int ExpiresSeconds { get; set; } = 3600;
        public int RefreshCount { get; private set; }
        public bool RotateTokens { get; set; }
        public int CatalogPadding { get; set; }
        public List<string> RefreshTokensReceived { get; } = [];
        public HttpStatusCode RevokeStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode TokenStatus { get; set; } = HttpStatusCode.OK;
        public int RevokeNetworkFailures { get; set; }
        public int RevokeCount { get; private set; }
        public bool FaultTokenBody { get; set; }
        private string nonce = "";
        public Fixture() => Client = new HttpClient(this);
        public async Task Launch(Uri uri)
        {
            var query = Query(uri.Query);
            nonce = query["nonce"];
            Assert.Equal("S256", query["code_challenge_method"]);
            Assert.Equal("https://api.openai.com/v1", query["resource"]);
            using var browser = new HttpClient();
            await browser.GetAsync(query["redirect_uri"] + "?state=" + (Invalid == "state" ? "wrong" : query["state"]) + "&code=synthetic&client_id=" + (Invalid == "client" ? "oaiapp_different" : ClientId));
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            object body;
            if (path == "/.well-known/openid-configuration") body = new { issuer = "https://auth.openai.com", jwks_uri = "https://auth.openai.com/.well-known/jwks.json", revocation_endpoint = "https://auth.openai.com/api/accounts/oauth/revoke" };
            else if (path == "/.well-known/jwks.json") { var p = rsa.ExportParameters(false); body = new { keys = new[] { new { kid = "test", kty = "RSA", alg = "RS256", n = B64(p.Modulus!), e = B64(p.Exponent!) } } }; }
            else if (path.EndsWith("/token"))
            {
                if (FaultTokenBody) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new FaultingStream()) };
                if (TokenStatus != HttpStatusCode.OK) return new HttpResponseMessage(TokenStatus) { Content = new StringContent("secret diagnostic must be redacted") };
                var form = Query(await request.Content!.ReadAsStringAsync(cancellationToken));
                Assert.Equal(ClientId, form["client_id"]);
                if (form["grant_type"] == "authorization_code") Assert.True(form["code_verifier"].Length >= 43);
                else
                {
                    var expected = RotateTokens && RefreshCount > 0 ? "refresh-rotated-" + RefreshCount : "refresh-synthetic";
                    Assert.Equal(expected, form["refresh_token"]);
                    RefreshTokensReceived.Add(form["refresh_token"]);
                    RefreshCount++;
                }
                var payload = B64(JsonSerializer.SerializeToUtf8Bytes(new { iss = Invalid == "issuer" ? "https://example.invalid" : "https://auth.openai.com", sub = Invalid == "subject" ? "different-subject" : "synthetic-subject", aud = Invalid == "audience" ? "wrong" : ClientId, exp = DateTimeOffset.UtcNow.AddHours(Invalid == "expiry" ? -1 : 1).ToUnixTimeSeconds(), nonce = Invalid == "nonce" ? "wrong" : nonce, email = "person@example.invalid" }));
                var signed = B64(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"kid\":\"test\"}")) + "." + payload;
                var signature = rsa.SignData(Encoding.ASCII.GetBytes(signed), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (Invalid == "signature") signature[0] ^= 1;
                body = new { access_token = RotateTokens && RefreshCount > 0 ? "access-rotated-" + RefreshCount : "access-synthetic", refresh_token = RotateTokens && RefreshCount > 0 ? "refresh-rotated-" + RefreshCount : "refresh-synthetic", id_token = signed + "." + B64(signature), scope = Invalid == "scope" ? "openid" : "openid offline_access resource.invoke chatgpt.tokens.use.direct", token_type = "Bearer", expires_in = ExpiresSeconds };
            }
            else if (path.EndsWith("/revoke")) { RevokeCount++; if (RevokeNetworkFailures-- > 0) throw new HttpRequestException("synthetic sensitive detail"); return new HttpResponseMessage(RevokeStatus); }
            else if (path == "/v1/models") { Assert.Equal("access-synthetic", request.Headers.Authorization!.Parameter); body = new { models = new[] { new { slug = "synthetic-model", display_name = "Synthetic", visibility = "list" }, new { slug = "hidden", display_name = "Hidden", visibility = "hidden" } }, base_instructions = new string('x', CatalogPadding) }; }
            else throw new InvalidOperationException("Unexpected synthetic endpoint");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body)) };
        }
        public static Dictionary<string,string> Query(string value) => value.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
        private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        protected override void Dispose(bool disposing) { rsa.Dispose(); base.Dispose(disposing); }
    }

    private sealed class FaultingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("sensitive synthetic body error");
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

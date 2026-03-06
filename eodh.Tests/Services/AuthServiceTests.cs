using System.IO;
using System.Net.Http;
using Xunit;
using eodh.Services;

namespace eodh.Tests.Services;

/// <summary>
/// Req 1: Authentication — validates credential management, environment selection,
/// Bearer token application, and HTTP client creation for EODH API access.
/// </summary>
public class AuthServiceTests
{
    private static AuthService CreateAuth() =>
        new(new CredentialStore(Path.Combine(Path.GetTempPath(), "eodh-test-" + Guid.NewGuid().ToString("N"))));

    [Fact]
    public void IsAuthenticated_FalseByDefault()
    {
        var auth = CreateAuth();
        Assert.False(auth.IsAuthenticated);
    }

    [Fact]
    public void SetCredentials_SetsIsAuthenticatedTrue()
    {
        var auth = CreateAuth();
        auth.SetCredentials("testuser", "test-api-token");

        Assert.True(auth.IsAuthenticated);
    }

    [Fact]
    public void SetCredentials_StoresUsername()
    {
        var auth = CreateAuth();
        auth.SetCredentials("testuser", "test-api-token");

        Assert.Equal("testuser", auth.Username);
    }

    [Fact]
    public void ClearCredentials_SetsIsAuthenticatedFalse()
    {
        var auth = CreateAuth();
        auth.SetCredentials("testuser", "test-api-token");
        auth.ClearCredentials();

        Assert.False(auth.IsAuthenticated);
    }

    [Fact]
    public void ClearCredentials_ClearsUsername()
    {
        var auth = CreateAuth();
        auth.SetCredentials("testuser", "test-api-token");
        auth.ClearCredentials();

        Assert.Null(auth.Username);
    }

    [Theory]
    [InlineData("production", "https://eodatahub.org.uk")]
    [InlineData("staging", "https://staging.eodatahub.org.uk")]
    [InlineData("test", "https://test.eodatahub.org.uk")]
    public void BaseUrl_MatchesSelectedEnvironment(string env, string expectedUrl)
    {
        var auth = CreateAuth();
        auth.SetCredentials("user", "token", env);

        Assert.Equal(expectedUrl, auth.BaseUrl);
    }

    [Fact]
    public void BaseUrl_DefaultsToProduction()
    {
        var auth = CreateAuth();
        auth.SetCredentials("user", "token");

        Assert.Equal("https://eodatahub.org.uk", auth.BaseUrl);
    }

    [Fact]
    public void ApplyAuth_SetsBearerTokenOnClient()
    {
        var auth = CreateAuth();
        auth.SetCredentials("user", "my-secret-token");

        using var client = new HttpClient();
        auth.ApplyAuth(client);

        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("my-secret-token", client.DefaultRequestHeaders.Authorization?.Parameter);
    }

    [Fact]
    public void ApplyAuth_DoesNothing_WhenNotAuthenticated()
    {
        var auth = CreateAuth();

        using var client = new HttpClient();
        auth.ApplyAuth(client);

        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public void CreateHttpClient_SetsBaseAddress()
    {
        var auth = CreateAuth();
        auth.SetCredentials("user", "token", "staging");

        using var client = auth.CreateHttpClient();

        Assert.Equal(new Uri("https://staging.eodatahub.org.uk"), client.BaseAddress);
    }

    [Fact]
    public void CreateHttpClient_SetsAuthHeader_WhenAuthenticated()
    {
        var auth = CreateAuth();
        auth.SetCredentials("user", "token");

        using var client = auth.CreateHttpClient();

        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization!.Scheme);
    }

    [Fact]
    public void CreateHttpClient_AcceptsJson()
    {
        var auth = CreateAuth();

        using var client = auth.CreateHttpClient();

        Assert.Contains(client.DefaultRequestHeaders.Accept,
            h => h.MediaType == "application/json");
    }

    [Fact]
    public void CreateHttpClient_HasTimeout()
    {
        var auth = CreateAuth();

        using var client = auth.CreateHttpClient();

        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
    }

    [Fact]
    public void SetCredentials_PersistsToStore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "eodh-auth-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CredentialStore(tempDir);
            var auth = new AuthService(store);
            auth.SetCredentials("bob", "my-token", "staging");

            var saved = store.Load();

            Assert.NotNull(saved);
            Assert.Equal("bob", saved.Value.Username);
            Assert.Equal("my-token", saved.Value.ApiToken);
            Assert.Equal("staging", saved.Value.Environment);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void TryLoadSavedCredentials_RestoresFromStore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "eodh-auth-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CredentialStore(tempDir);
            store.Save("alice", "saved-token", "test");

            var auth = new AuthService(store);
            var loaded = auth.TryLoadSavedCredentials();

            Assert.True(loaded);
            Assert.True(auth.IsAuthenticated);
            Assert.Equal("alice", auth.Username);
            Assert.Equal("https://test.eodatahub.org.uk", auth.BaseUrl);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ClearCredentials_DeletesFromStore()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "eodh-auth-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CredentialStore(tempDir);
            var auth = new AuthService(store);
            auth.SetCredentials("bob", "my-token");
            auth.ClearCredentials();

            var saved = store.Load();

            Assert.Null(saved);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Environment_ExposedAfterSetCredentials()
    {
        var auth = CreateAuth();
        auth.SetCredentials("user", "token", "staging");

        Assert.Equal("staging", auth.Environment);
    }

    [Fact]
    public void Environment_DefaultsToProduction()
    {
        var auth = CreateAuth();

        Assert.Equal("production", auth.Environment);
    }
}

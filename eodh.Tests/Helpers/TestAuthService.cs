using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using eodh.Services;

namespace eodh.Tests.Helpers;

/// <summary>
/// Test-only AuthService subclass that returns an HttpClient backed by
/// a FixtureHttpHandler instead of making real HTTP calls.
/// </summary>
public class TestAuthService : AuthService
{
    private readonly FixtureHttpHandler _handler;

    public TestAuthService(FixtureHttpHandler handler)
        : base(new CredentialStore(Path.Combine(Path.GetTempPath(), "eodh-test-" + Guid.NewGuid().ToString("N"))))
    {
        _handler = handler;
        SetCredentials("testuser", "test-token");
    }

    public override HttpClient CreateHttpClient()
    {
        // Note: we don't dispose the handler here because it's shared across calls
        var client = new HttpClient(_handler, disposeHandler: false)
        {
            BaseAddress = new Uri(BaseUrl)
        };
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}

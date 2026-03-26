using System.Net.Http;
using System.Net.Http.Headers;

namespace eodh.Services;

/// <summary>
/// Handles EODH authentication. Stores credentials and provides
/// Bearer token for API requests.
/// </summary>
public class AuthService
{
    private const string ProductionUrl = "https://eodatahub.org.uk";
    private const string StagingUrl = "https://staging.eodatahub.org.uk";
    private const string TestUrl = "https://test.eodatahub.org.uk";

    private readonly CredentialStore _store;
    private string? _username;
    private string? _apiToken;
    private string _environment = "production";

    public AuthService() : this(new CredentialStore()) { }

    public AuthService(CredentialStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Whether the user has provided credentials.
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_apiToken);

    /// <summary>
    /// The current username.
    /// </summary>
    public string? Username => _username;

    /// <summary>
    /// The current API token (Bearer token) for authenticated requests.
    /// </summary>
    public string? ApiToken => _apiToken;

    /// <summary>
    /// The current environment name (production, staging, test).
    /// </summary>
    public string Environment => _environment;

    /// <summary>
    /// The base URL for the current environment.
    /// </summary>
    public string BaseUrl => _environment switch
    {
        "staging" => StagingUrl,
        "test" => TestUrl,
        _ => ProductionUrl
    };

    /// <summary>
    /// Set credentials for EODH API access.
    /// </summary>
    public void SetCredentials(string username, string apiToken, string environment = "production")
    {
        _username = username;
        _apiToken = apiToken;
        _environment = environment;
        _store.Save(username, apiToken, environment);
    }

    /// <summary>
    /// Clear stored credentials (memory and disk).
    /// </summary>
    public void ClearCredentials()
    {
        _username = null;
        _apiToken = null;
        _store.Delete();
    }

    /// <summary>
    /// Try to load previously saved credentials from disk.
    /// Returns true if credentials were found and restored.
    /// </summary>
    public bool TryLoadSavedCredentials()
    {
        var saved = _store.Load();
        if (saved == null) return false;

        _username = saved.Value.Username;
        _apiToken = saved.Value.ApiToken;
        _environment = saved.Value.Environment;
        return true;
    }

    /// <summary>
    /// Apply authentication headers to an HttpClient.
    /// </summary>
    public void ApplyAuth(HttpClient client)
    {
        if (!IsAuthenticated) return;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiToken);
    }

    /// <summary>
    /// Create an authenticated HttpClient for EODH API calls.
    /// </summary>
    public virtual HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (IsAuthenticated)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiToken);
        }

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }
}

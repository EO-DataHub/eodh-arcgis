using System.IO;
using System.Net;
using System.Net.Http;

namespace eodh.Tests.Helpers;

/// <summary>
/// Custom HttpMessageHandler that serves fixture JSON files as HTTP responses.
/// Works like Python's VCR.py cassettes — register URL patterns mapped to
/// fixture files, and the handler replays them for matching requests.
/// </summary>
public class FixtureHttpHandler : HttpMessageHandler
{
    private readonly List<(string UrlPattern, HttpStatusCode Status, string Content)> _responses = [];
    private readonly Dictionary<string, Dictionary<string, string>> _headerMap = [];

    /// <summary>
    /// Register a fixture file to be returned when the request URL contains the given pattern.
    /// </summary>
    public void Register(string urlContains, string fixturePath, HttpStatusCode status = HttpStatusCode.OK)
    {
        var json = File.ReadAllText(fixturePath);
        _responses.Add((urlContains, status, json));
    }

    /// <summary>
    /// Register an inline JSON string response for a URL pattern.
    /// </summary>
    public void RegisterJson(string urlContains, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Add((urlContains, status, json));
    }

    /// <summary>
    /// Register a status-only response (no body) for a URL pattern.
    /// </summary>
    public void RegisterStatus(string urlContains, HttpStatusCode status)
    {
        _responses.Add((urlContains, status, ""));
    }

    /// <summary>
    /// Register a JSON response with custom response headers (e.g. Location).
    /// </summary>
    public void RegisterJsonWithHeaders(
        string urlContains, string json,
        Dictionary<string, string>? headers = null,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Add((urlContains, status, json));
        if (headers != null)
            _headerMap[urlContains] = headers;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var url = request.RequestUri?.ToString() ?? "";

        foreach (var (pattern, status, content) in _responses)
        {
            if (url.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                var response = new HttpResponseMessage(status)
                {
                    Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json"),
                    RequestMessage = request
                };
                if (_headerMap.TryGetValue(pattern, out var headers))
                    foreach (var (key, val) in headers)
                        response.Headers.TryAddWithoutValidation(key, val);
                return Task.FromResult(response);
            }
        }

        // No match — return 404
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            RequestMessage = request
        });
    }
}

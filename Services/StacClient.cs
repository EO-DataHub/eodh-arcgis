using System.Net.Http;
using System.Text;
using System.Text.Json;
using eodh.Models;

namespace eodh.Services;

/// <summary>
/// HTTP client for the EODH STAC API.
/// Replaces pyeodh — makes direct REST calls to STAC endpoints.
/// </summary>
public class StacClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AuthService _authService;

    public StacClient(AuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// List available STAC catalogs.
    /// EODH endpoint: GET /api/catalogue/stac/catalogs
    /// </summary>
    public async Task<List<StacCatalog>> GetCatalogsAsync(CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        var all = new List<StacCatalog>();
        string? url = "/api/catalogue/stac/catalogs";

        while (url != null)
        {
            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<StacCatalogList>(json, JsonOptions);

            if (result?.Catalogs != null)
                all.AddRange(result.Catalogs);

            url = GetLinkHref(result?.Links, "next");
        }

        return all;
    }

    /// <summary>
    /// List collections within a catalog.
    /// Uses the catalog's own "data" or "collections" link to handle nested catalogs.
    /// </summary>
    public async Task<List<StacCollection>> GetCollectionsAsync(
        StacCatalog catalog, CancellationToken ct = default)
    {
        string? url = GetLinkHref(catalog.Links, "data")
                   ?? GetLinkHref(catalog.Links, "collections")
                   ?? $"{_authService.BaseUrl}/api/catalogue/stac/catalogs/{catalog.Id}/collections";

        using var client = _authService.CreateHttpClient();
        var all = new List<StacCollection>();

        while (url != null)
        {
            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<StacCollectionList>(json, JsonOptions);

            if (result?.Collections != null)
                all.AddRange(result.Collections);

            url = GetLinkHref(result?.Links, "next");
        }

        return all;
    }

    /// <summary>
    /// Search STAC items with filters.
    /// Uses the catalog's own "search" link to handle nested catalogs.
    /// </summary>
    public async Task<StacSearchResult> SearchAsync(
        StacCatalog catalog, SearchFilters filters, CancellationToken ct = default)
    {
        var searchUrl = GetLinkHref(catalog.Links, "search")
                     ?? $"{_authService.BaseUrl}/api/catalogue/stac/catalogs/{catalog.Id}/search";

        using var client = _authService.CreateHttpClient();

        var body = filters.ToSearchParams();
        var jsonBody = JsonSerializer.Serialize(body, JsonOptions);
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        // Debug: store what we sent for diagnostics
        LastSearchDebug = $"POST {searchUrl}\n{jsonBody}";

        var response = await client.PostAsync(searchUrl, content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);

        // Debug: append raw response length and first 500 chars
        LastSearchDebug += $"\n\nResponse ({json.Length} chars):\n{json[..Math.Min(json.Length, 500)]}";

        var itemCollection = JsonSerializer.Deserialize<StacItemCollection>(json, JsonOptions);

        var items = itemCollection?.Features ?? [];
        var totalCount = itemCollection?.Context?.Matched
                         ?? itemCollection?.NumberMatched
                         ?? items.Count;

        var nextLink = itemCollection?.Links?.FirstOrDefault(l => l.Rel == "next")?.Href;

        return new StacSearchResult(items, totalCount, nextLink);
    }

    /// <summary>
    /// Debug info from the last search call. Temporary for diagnostics.
    /// </summary>
    public string? LastSearchDebug { get; private set; }

    /// <summary>
    /// Extract an href from a list of STAC links by rel type.
    /// </summary>
    private static string? GetLinkHref(List<StacLink>? links, string rel)
    {
        return links?.FirstOrDefault(l => l.Rel == rel)?.Href;
    }

    /// <summary>
    /// Fetch the next page of search results.
    /// </summary>
    public async Task<StacSearchResult> GetNextPageAsync(
        string nextUrl, CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        var response = await client.GetAsync(nextUrl, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var itemCollection = JsonSerializer.Deserialize<StacItemCollection>(json, JsonOptions);

        var items = itemCollection?.Features ?? [];
        var totalCount = itemCollection?.Context?.Matched
                         ?? itemCollection?.NumberMatched
                         ?? items.Count;
        var nextLink = itemCollection?.Links?.FirstOrDefault(l => l.Rel == "next")?.Href;

        return new StacSearchResult(items, totalCount, nextLink);
    }

    /// <summary>
    /// Fetch a single STAC item by collection and item ID.
    /// </summary>
    public async Task<StacItem?> GetItemAsync(
        string catalogId, string collectionId, string itemId, CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        var response = await client.GetAsync(
            $"/api/catalogue/stac/catalogs/{catalogId}/collections/{collectionId}/items/{itemId}", ct);

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<StacItem>(json, JsonOptions);
    }

    #region Internal response types

    private record StacCatalogList(
        [property: System.Text.Json.Serialization.JsonPropertyName("catalogs")]
        List<StacCatalog>? Catalogs,
        [property: System.Text.Json.Serialization.JsonPropertyName("links")]
        List<StacLink>? Links
    );

    private record StacCollectionList(
        [property: System.Text.Json.Serialization.JsonPropertyName("collections")]
        List<StacCollection>? Collections,
        [property: System.Text.Json.Serialization.JsonPropertyName("links")]
        List<StacLink>? Links
    );

    #endregion
}

/// <summary>
/// Result of a STAC search operation, including pagination info.
/// </summary>
public record StacSearchResult(
    List<StacItem> Items,
    int TotalCount,
    string? NextPageUrl
);

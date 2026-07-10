using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using eodh.Models;

namespace eodh.Services;

/// <summary>
/// Reads commercial order records from the current workspace's STAC catalogue.
/// </summary>
public sealed class WorkspaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AuthService _authService;

    public WorkspaceService(AuthService authService)
    {
        _authService = authService;
    }

    public async Task<List<WorkspaceCommercialRecord>> GetCommercialRecordsAsync(
        string workspaceName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceName))
            throw new ArgumentException("A workspace name is required.", nameof(workspaceName));

        using var client = _authService.CreateHttpClient();
        var collectionsUrl = new Uri(new Uri(_authService.BaseUrl),
            $"/api/catalogue/stac/catalogs/user/catalogs/{Uri.EscapeDataString(workspaceName)}/catalogs/commercial-data/collections");
        var records = new List<WorkspaceCommercialRecord>();
        var visitedCollectionPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Uri? pageUrl = collectionsUrl;

        while (pageUrl != null && visitedCollectionPages.Add(Canonicalize(pageUrl)))
        {
            using var response = await client.GetAsync(pageUrl, ct);
            await ApiResponse.EnsureSuccessAsync(response, "workspace commercial collections", ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            var page = JsonSerializer.Deserialize<StacCollectionPage>(json, JsonOptions);

            foreach (var collection in page?.Collections ?? [])
                records.AddRange(await GetCollectionRecordsAsync(client, collection, pageUrl, ct));

            pageUrl = ResolveLink(page?.Links, "next", pageUrl);
        }

        return records
            .OrderByDescending(record => ParseDate(record.Updated) ?? ParseDate(record.Created) ?? ParseDate(record.OrderDate))
            .ThenBy(record => record.ProviderLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.Item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<WorkspaceCommercialRecord>> GetCollectionRecordsAsync(
        HttpClient client,
        StacCollection collection,
        Uri collectionPageUrl,
        CancellationToken ct)
    {
        var itemsUrl = ResolveLink(collection.Links, "items", collectionPageUrl);
        if (itemsUrl == null)
            return [];

        var providerLabel = GetProviderLabel(collection, itemsUrl);
        var records = new List<WorkspaceCommercialRecord>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Uri? pageUrl = itemsUrl;

        while (pageUrl != null && visited.Add(Canonicalize(pageUrl)))
        {
            using var response = await client.GetAsync(pageUrl, ct);
            await ApiResponse.EnsureSuccessAsync(response, "workspace commercial items", ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            var page = JsonSerializer.Deserialize<StacItemCollection>(json, JsonOptions);

            foreach (var item in page?.Features ?? [])
                records.Add(new WorkspaceCommercialRecord(providerLabel, collection, item));

            pageUrl = ResolveLink(page?.Links, "next", pageUrl);
        }

        return records;
    }

    private static string GetProviderLabel(StacCollection collection, Uri itemsUrl)
    {
        var segments = itemsUrl.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var commercialDataIndex = Array.FindIndex(segments,
            segment => segment.Equals("commercial-data", StringComparison.OrdinalIgnoreCase));
        if (commercialDataIndex >= 0 && commercialDataIndex + 2 < segments.Length &&
            segments[commercialDataIndex + 1].Equals("catalogs", StringComparison.OrdinalIgnoreCase))
            return ToDisplayLabel(segments[commercialDataIndex + 2]);

        var collectionPrefix = collection.Id.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return ToDisplayLabel(collectionPrefix ?? "Commercial");
    }

    private static string ToDisplayLabel(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static Uri? ResolveLink(List<StacLink>? links, string rel, Uri relativeTo)
    {
        var href = links?.FirstOrDefault(link =>
            link.Rel.Equals(rel, StringComparison.OrdinalIgnoreCase))?.Href;
        if (string.IsNullOrWhiteSpace(href))
            return null;

        return Uri.TryCreate(href, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(relativeTo, href);
    }

    private static string Canonicalize(Uri uri) =>
        new UriBuilder(uri) { Fragment = string.Empty }.Uri.AbsoluteUri.TrimEnd('/');

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private sealed record StacCollectionPage(
        [property: JsonPropertyName("collections")] List<StacCollection>? Collections,
        [property: JsonPropertyName("links")] List<StacLink>? Links);
}

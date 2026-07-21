using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using eodh.Models;

namespace eodh.Services;

/// <summary>
/// Link-driven HTTP client for the EODH STAC API.
/// </summary>
public class StacClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AuthService _authService;
    private readonly ConcurrentDictionary<string, Lazy<Task<bool>>> _cloudCoverCapabilityCache =
        new(StringComparer.OrdinalIgnoreCase);

    public StacClient(AuthService authService)
    {
        _authService = authService;
    }

    public IReadOnlyList<CatalogRoot> CatalogRoots => CatalogRoot.All;

    /// <summary>
    /// Validate the current bearer credential against a curated STAC root
    /// without traversing the entire catalogue tree.
    /// </summary>
    public async Task ValidateCredentialsAsync(
        string workspaceName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceName))
            throw new ArgumentException("A workspace name is required.", nameof(workspaceName));

        using var client = _authService.CreateHttpClient();
        var workspacePath =
            $"/api/catalogue/stac/catalogs/user/catalogs/{Uri.EscapeDataString(workspaceName)}";
        using var response = await client.GetAsync(workspacePath, ct);
        await ApiResponse.EnsureSuccessAsync(response, "authentication", ct, _authService.ApiToken);
    }

    /// <summary>
    /// Recursively discover all collections below one curated root. Traversal
    /// follows advertised links, handles pagination, and tolerates cycles.
    /// </summary>
    public async Task<List<CatalogCollectionEntry>> DiscoverCollectionsAsync(
        CatalogRoot root,
        CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        var pending = new Queue<TraversalRequest>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new Dictionary<string, CatalogCollectionEntry>(StringComparer.OrdinalIgnoreCase);

        var rootUri = ResolveUrl(root.Path, new Uri(_authService.BaseUrl));
        pending.Enqueue(new TraversalRequest(
            rootUri,
            new TraversalContext(root.DisplayName, rootUri.ToString(), null)));

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var request = pending.Dequeue();
            var canonicalUrl = Canonicalize(request.Url);
            if (!visited.Add(canonicalUrl))
                continue;

            using var response = await client.GetAsync(request.Url, ct);
            await ApiResponse.EnsureSuccessAsync(response, "catalogue discovery", ct, _authService.ApiToken);
            var json = await response.Content.ReadAsStringAsync(ct);

            using var document = JsonDocument.Parse(json);
            ProcessDocument(document.RootElement, request, root, pending, entries);
        }

        return entries.Values
            .OrderBy(entry => entry.ProviderLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Collection.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Identity, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Search the owning catalogue for a flattened collection entry.
    /// </summary>
    public Task<StacSearchResult> SearchAsync(
        CatalogCollectionEntry entry,
        SearchFilters filters,
        CancellationToken ct = default) =>
        SearchAtUrlAsync(entry.SearchUrl, filters, ct);

    /// <summary>
    /// Probe one item to determine whether a collection publishes the STAC
    /// eo:cloud_cover property used by the search filter.
    /// </summary>
    public async Task<bool> CollectionHasCloudCoverAsync(
        CatalogCollectionEntry entry,
        CancellationToken ct = default)
    {
        var key = entry.Identity;
        var cachedProbe = _cloudCoverCapabilityCache.GetOrAdd(
            key,
            _ => new Lazy<Task<bool>>(
                () => ProbeCloudCoverCapabilityAsync(entry),
                LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await cachedProbe.Value.WaitAsync(ct);
        }
        catch
        {
            _cloudCoverCapabilityCache.TryRemove(key, out _);
            throw;
        }
    }

    private async Task<bool> ProbeCloudCoverCapabilityAsync(CatalogCollectionEntry entry)
    {
        var itemsHref = GetLinkHref(entry.Collection.Links, "items");
        if (!string.IsNullOrWhiteSpace(itemsHref))
            return await ProbeItemsForCloudCoverAsync(entry, itemsHref);

        return await ProbeSearchForCloudCoverAsync(entry);
    }

    private async Task<bool> ProbeItemsForCloudCoverAsync(
        CatalogCollectionEntry entry,
        string itemsHref)
    {
        var collectionSelfHref = GetLinkHref(entry.Collection.Links, "self");
        var collectionBase = collectionSelfHref == null
            ? new Uri(
                $"{entry.CatalogueUrl.TrimEnd('/')}/collections/" +
                $"{Uri.EscapeDataString(entry.Collection.Id)}/")
            : ResolveUrl(collectionSelfHref, new Uri(entry.CatalogueUrl));
        var itemsUrl = ResolveUrl(itemsHref, collectionBase);
        var separator = string.IsNullOrEmpty(itemsUrl.Query) ? "?" : "&";

        using var client = _authService.CreateHttpClient();
        using var response = await client.GetAsync($"{itemsUrl}{separator}limit=5");
        await ApiResponse.EnsureSuccessAsync(
            response, "cloud-cover capability detection", default, _authService.ApiToken);
        return ResponseHasCloudCoverProperty(await response.Content.ReadAsStringAsync());
    }

    private async Task<bool> ProbeSearchForCloudCoverAsync(CatalogCollectionEntry entry)
    {
        using var client = _authService.CreateHttpClient();
        var body = new SearchFilters
        {
            Collections = [entry.Collection.Id],
            Limit = 1
        }.ToSearchParams();
        var jsonBody = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(entry.SearchUrl, content);
        await ApiResponse.EnsureSuccessAsync(
            response, "cloud-cover capability detection", default, _authService.ApiToken);
        return ResponseHasCloudCoverProperty(await response.Content.ReadAsStringAsync());
    }

    private static bool ResponseHasCloudCoverProperty(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
            return false;

        return features.EnumerateArray().Any(item =>
            item.TryGetProperty("properties", out var properties) &&
            properties.ValueKind == JsonValueKind.Object &&
            properties.TryGetProperty("eo:cloud_cover", out _));
    }

    /// <summary>
    /// Compatibility overload for callers that already hold a catalogue.
    /// </summary>
    public Task<StacSearchResult> SearchAsync(
        StacCatalog catalog,
        SearchFilters filters,
        CancellationToken ct = default)
    {
        var searchUrl = GetLinkHref(catalog.Links, "search")
            ?? $"{_authService.BaseUrl}/api/catalogue/stac/catalogs/{catalog.Id}/search";
        return SearchAtUrlAsync(searchUrl, filters, ct);
    }

    private async Task<StacSearchResult> SearchAtUrlAsync(
        string searchUrl,
        SearchFilters filters,
        CancellationToken ct)
    {
        using var client = _authService.CreateHttpClient();
        var body = filters.ToSearchParams();
        var jsonBody = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(searchUrl, content, ct);
        await ApiResponse.EnsureSuccessAsync(response, "item search", ct, _authService.ApiToken);

        var json = await response.Content.ReadAsStringAsync(ct);
        var itemCollection = JsonSerializer.Deserialize<StacItemCollection>(json, JsonOptions);
        return CreateSearchResult(itemCollection);
    }

    public async Task<StacSearchResult> GetNextPageAsync(
        string nextUrl,
        CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        using var response = await client.GetAsync(nextUrl, ct);
        await ApiResponse.EnsureSuccessAsync(response, "search pagination", ct, _authService.ApiToken);
        var json = await response.Content.ReadAsStringAsync(ct);
        return CreateSearchResult(JsonSerializer.Deserialize<StacItemCollection>(json, JsonOptions));
    }

    public async Task<StacItem?> GetItemAsync(
        string catalogId,
        string collectionId,
        string itemId,
        CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        using var response = await client.GetAsync(
            $"/api/catalogue/stac/catalogs/{catalogId}/collections/{collectionId}/items/{itemId}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        await ApiResponse.EnsureSuccessAsync(response, "item retrieval", ct, _authService.ApiToken);
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<StacItem>(json, JsonOptions);
    }

    private void ProcessDocument(
        JsonElement document,
        TraversalRequest request,
        CatalogRoot root,
        Queue<TraversalRequest> pending,
        Dictionary<string, CatalogCollectionEntry> entries)
    {
        if (document.TryGetProperty("collections", out var collections) &&
            collections.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in collections.EnumerateArray())
                AddCollection(element, request.Context, request.Url, root, entries);

            EnqueueLinks(document, request, pending, ["next"]);
            return;
        }

        if (document.TryGetProperty("catalogs", out var catalogs) &&
            catalogs.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in catalogs.EnumerateArray())
                ProcessCatalog(element, request, pending);

            EnqueueLinks(document, request, pending, ["next"]);
            return;
        }

        var type = document.TryGetProperty("type", out var typeValue)
            ? typeValue.GetString()
            : null;

        if (string.Equals(type, "Collection", StringComparison.OrdinalIgnoreCase))
        {
            AddCollection(document, request.Context, request.Url, root, entries);
            return;
        }

        ProcessCatalog(document, request, pending);
    }

    private void ProcessCatalog(
        JsonElement element,
        TraversalRequest request,
        Queue<TraversalRequest> pending)
    {
        var catalog = element.Deserialize<StacCatalog>(JsonOptions);
        if (catalog == null)
            return;

        var selfUrl = GetLinkHref(catalog.Links, "self");
        var catalogueUri = selfUrl == null
            ? request.Url
            : ResolveUrl(selfUrl, request.Url);
        var providerLabel = !string.IsNullOrWhiteSpace(catalog.Title)
            ? catalog.Title!
            : !string.IsNullOrWhiteSpace(catalog.Id)
                ? catalog.Id
                : request.Context.ProviderLabel;
        var searchHref = GetLinkHref(catalog.Links, "search");
        var searchUrl = searchHref == null
            ? request.Context.SearchUrl
            : ResolveUrl(searchHref, catalogueUri).ToString();
        var context = new TraversalContext(
            providerLabel,
            catalogueUri.ToString(),
            searchUrl);

        if (catalog.Links == null)
            return;

        foreach (var link in catalog.Links)
        {
            if (link.Rel is not ("child" or "catalog" or "catalogs" or "data" or "collections" or "next"))
                continue;

            var linkUri = ResolveUrl(link.Href, catalogueUri);
            pending.Enqueue(new TraversalRequest(linkUri, context));
        }
    }

    private void AddCollection(
        JsonElement element,
        TraversalContext context,
        Uri sourceUri,
        CatalogRoot root,
        Dictionary<string, CatalogCollectionEntry> entries)
    {
        var collection = element.Deserialize<StacCollection>(JsonOptions);
        if (collection == null || string.IsNullOrWhiteSpace(collection.Id))
            return;

        var parentHref = GetLinkHref(collection.Links, "parent");
        var catalogueUrl = parentHref == null
            ? context.CatalogueUrl
            : ResolveUrl(parentHref, sourceUri).ToString();
        var searchUrl = context.SearchUrl ?? $"{catalogueUrl.TrimEnd('/')}/search";
        var entry = new CatalogCollectionEntry(
            root,
            context.ProviderLabel,
            Canonicalize(new Uri(catalogueUrl)),
            searchUrl,
            collection);

        entries.TryAdd(entry.Identity, entry);
    }

    private static void EnqueueLinks(
        JsonElement document,
        TraversalRequest request,
        Queue<TraversalRequest> pending,
        IReadOnlyCollection<string> rels)
    {
        if (!document.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
            return;

        foreach (var link in links.EnumerateArray())
        {
            if (!link.TryGetProperty("rel", out var relValue) ||
                !rels.Contains(relValue.GetString() ?? string.Empty) ||
                !link.TryGetProperty("href", out var hrefValue) ||
                string.IsNullOrWhiteSpace(hrefValue.GetString()))
                continue;

            pending.Enqueue(new TraversalRequest(
                ResolveUrl(hrefValue.GetString()!, request.Url),
                request.Context));
        }
    }

    private static StacSearchResult CreateSearchResult(StacItemCollection? itemCollection)
    {
        var items = itemCollection?.Features ?? [];
        var totalCount = itemCollection?.Context?.Matched
            ?? itemCollection?.NumberMatched
            ?? items.Count;
        var nextLink = itemCollection?.Links?.FirstOrDefault(link => link.Rel == "next")?.Href;
        return new StacSearchResult(items, totalCount, nextLink);
    }

    private static string? GetLinkHref(List<StacLink>? links, string rel) =>
        links?.FirstOrDefault(link =>
            string.Equals(link.Rel, rel, StringComparison.OrdinalIgnoreCase))?.Href;

    private static Uri ResolveUrl(string href, Uri relativeTo)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute))
            return absolute;

        return new Uri(relativeTo, href);
    }

    private static string Canonicalize(Uri uri)
    {
        var builder = new UriBuilder(uri) { Fragment = string.Empty };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private sealed record TraversalContext(
        string ProviderLabel,
        string CatalogueUrl,
        string? SearchUrl);

    private sealed record TraversalRequest(Uri Url, TraversalContext Context);
}

public record StacSearchResult(
    List<StacItem> Items,
    int TotalCount,
    string? NextPageUrl);

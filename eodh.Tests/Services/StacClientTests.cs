using System.IO;
using System.Net;
using Xunit;
using eodh.Models;
using eodh.Services;
using eodh.Tests.Helpers;

namespace eodh.Tests.Services;

/// <summary>
/// Req 2: Search & Filtering — validates StacClient correctly queries EODH STAC APIs,
/// handles pagination, and deserializes catalog/collection/item responses.
/// Uses recorded API fixtures replayed through FixtureHttpHandler.
/// </summary>
public class StacClientTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static (StacClient client, FixtureHttpHandler handler) CreateClient()
    {
        var handler = new FixtureHttpHandler();
        var auth = new TestAuthService(handler);
        return (new StacClient(auth), handler);
    }

    // Helper: a catalog with data + search links matching the real CEDA catalogue
    private static StacCatalog CedaCatalog() => new(
        "ceda-stac-catalogue",
        "CEDA STAC Catalogue",
        "Public CEDA datasets",
        [
            new StacLink("data",
                "https://eodatahub.org.uk/api/catalogue/stac/catalogs/public/catalogs/ceda-stac-catalogue/collections",
                "application/json", null),
            new StacLink("search",
                "https://eodatahub.org.uk/api/catalogue/stac/catalogs/public/catalogs/ceda-stac-catalogue/search",
                "application/geo+json", "STAC search")
        ]
    );

    [Fact]
    public async Task GetCatalogsAsync_ReturnsDeserializedCatalogs()
    {
        var (client, handler) = CreateClient();
        // catalogs.json has a "next" link — register empty page2 so pagination terminates
        handler.RegisterJson("token=processing-results", """{"catalogs":[],"links":[]}""");
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));

        var catalogs = await client.GetCatalogsAsync();

        Assert.NotNull(catalogs);
        Assert.NotEmpty(catalogs);
        Assert.Contains(catalogs, c => c.Id == "airbus");
    }

    [Fact]
    public async Task GetCatalogsAsync_FollowsPagination_ReturnsAllCatalogs()
    {
        var (client, handler) = CreateClient();
        handler.Register("/api/catalogue/stac/catalogs?token=page2", FixturePath("catalogs_page2.json"));
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs_page1.json"));

        var catalogs = await client.GetCatalogsAsync();

        Assert.Equal(3, catalogs.Count);
        Assert.Contains(catalogs, c => c.Id == "airbus");
        Assert.Contains(catalogs, c => c.Id == "ceda");
        Assert.Contains(catalogs, c => c.Id == "copernicus");
    }

    [Fact]
    public async Task GetCollectionsAsync_FollowsPagination_ReturnsAllCollections()
    {
        var (client, handler) = CreateClient();
        handler.Register("token=page2", FixturePath("collections_page2.json"));
        handler.Register("/collections", FixturePath("collections_page1.json"));

        var collections = await client.GetCollectionsAsync(CedaCatalog());

        Assert.Equal(3, collections.Count);
        Assert.Contains(collections, c => c.Id == "sentinel2_ard");
        Assert.Contains(collections, c => c.Id == "sentinel1_ard");
        Assert.Contains(collections, c => c.Id == "ukcp");
    }

    [Fact]
    public async Task GetCatalogsAsync_ReturnsEmptyList_WhenNoCatalogs()
    {
        var (client, handler) = CreateClient();
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs_empty.json"));

        var catalogs = await client.GetCatalogsAsync();

        Assert.NotNull(catalogs);
        Assert.Empty(catalogs);
    }

    [Fact]
    public async Task GetCollectionsAsync_UsesDataLink_WhenPresent()
    {
        var (client, handler) = CreateClient();
        // collections.json has a "next" link — register empty page2 so pagination terminates
        handler.RegisterJson("token=", """{"collections":[],"links":[]}""");
        handler.Register("/collections", FixturePath("collections.json"));

        var collections = await client.GetCollectionsAsync(CedaCatalog());

        Assert.NotNull(collections);
        Assert.NotEmpty(collections);
        // Real recorded data includes these collections
        Assert.Contains(collections, c => c.Id == "sentinel2_ard");
        Assert.Contains(collections, c => c.Id == "ukcp");
    }

    [Fact]
    public async Task SearchAsync_ReturnsItemsAndTotalCount()
    {
        var (client, handler) = CreateClient();
        handler.Register("/search", FixturePath("search_results.json"));

        var filters = new SearchFilters
        {
            Collections = ["sentinel2_ard"],
            Bbox = [-1.5, 51.0, 0.5, 52.0],
            Limit = 2
        };

        var result = await client.SearchAsync(CedaCatalog(), filters);

        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(6298, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_ExtractsNextPageUrl()
    {
        var (client, handler) = CreateClient();
        handler.Register("/search", FixturePath("search_results.json"));

        var filters = new SearchFilters
        {
            Collections = ["sentinel2_ard"],
            Bbox = [-1.5, 51.0, 0.5, 52.0],
            Limit = 2
        };

        var result = await client.SearchAsync(CedaCatalog(), filters);

        // Real recorded response has a "next" link for pagination
        Assert.NotNull(result.NextPageUrl);
    }

    [Fact]
    public async Task SearchAsync_DeserializesItemProperties()
    {
        var (client, handler) = CreateClient();
        handler.Register("/search", FixturePath("search_results.json"));

        var filters = new SearchFilters
        {
            Collections = ["sentinel2_ard"],
            Limit = 2
        };

        var result = await client.SearchAsync(CedaCatalog(), filters);

        var item = result.Items[0];
        Assert.Equal("sentinel2_ard", item.Collection);
        Assert.NotNull(item.Properties?.DateTime);
        Assert.Equal("2026-02-16T11:00:29Z", item.Properties!.DateTime);
        Assert.NotNull(item.Assets);
        Assert.True(item.Assets!.ContainsKey("cog"));
        Assert.True(item.Assets!.ContainsKey("thumbnail"));
    }

    [Fact]
    public async Task GetNextPageAsync_FetchesNextPageUrl()
    {
        var (client, handler) = CreateClient();
        handler.Register("/search", FixturePath("search_results_page2.json"));

        var result = await client.GetNextPageAsync(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/public/catalogs/ceda-stac-catalogue/search?page=2");

        Assert.NotNull(result);
        Assert.NotEmpty(result.Items);
    }

    [Fact]
    public async Task GetItemAsync_ReturnsItem_WhenFound()
    {
        var (client, handler) = CreateClient();
        handler.Register("/items/", FixturePath("item.json"));

        var item = await client.GetItemAsync(
            "public/catalogs/ceda-stac-catalogue", "sentinel2_ard",
            "neodc.sentinel_ard.data.sentinel_2.2026.02.19.S2A_20260219_latn510lonw0036_T30UVB_ORB137_20260219144647_utm30n_osgb");

        Assert.NotNull(item);
        Assert.Contains("sentinel_ard", item.Id);
        Assert.Equal("sentinel2_ard", item.Collection);
        Assert.NotNull(item.SelfLink);
    }

    [Fact]
    public async Task GetItemAsync_ReturnsNull_WhenNotFound()
    {
        var (client, _) = CreateClient();
        // No fixtures registered — handler returns 404 by default

        var item = await client.GetItemAsync("any", "any", "nonexistent-item-id");

        Assert.Null(item);
    }
}

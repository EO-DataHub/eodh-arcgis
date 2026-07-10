using System.Net;
using System.Net.Http;
using eodh.Models;
using eodh.Services;
using eodh.Tests.Helpers;
using Xunit;

namespace eodh.Tests.Services;

public class StacClientTests
{
    private static (StacClient client, FixtureHttpHandler handler) CreateClient()
    {
        var handler = new FixtureHttpHandler();
        return (new StacClient(new TestAuthService(handler)), handler);
    }

    private static StacCatalog CedaCatalog() => new(
        "ceda-stac-catalogue",
        "CEDA",
        null,
        [new StacLink("search", "https://eodatahub.org.uk/ceda/search", null, null)]);

    [Fact]
    public void CatalogRoots_ContainsOnlyPublicAndCommercial()
    {
        var (client, _) = CreateClient();

        Assert.Collection(client.CatalogRoots,
            root => Assert.Equal("Public", root.DisplayName),
            root => Assert.Equal("Commercial", root.DisplayName));
    }

    [Fact]
    public async Task DiscoverCollectionsAsync_RecursesPagesPreventsCyclesAndKeepsDuplicateIds()
    {
        var (client, handler) = CreateClient();
        handler.RegisterJson("/providers/zeta/collections?page=2", """
            {"collections":[{"id":"z-other","title":"Other","links":[{"rel":"parent","href":"/providers/zeta"}]}],"links":[]}
            """);
        handler.RegisterJson("/providers/zeta/collections", """
            {"collections":[{"id":"shared","title":"Bravo","links":[{"rel":"parent","href":"/providers/zeta"}]}],
             "links":[{"rel":"next","href":"?page=2"}]}
            """);
        handler.RegisterJson("/providers/alpha/collections", """
            {"collections":[{"id":"shared","title":"Alpha collection","links":[{"rel":"parent","href":"/providers/alpha"}]}],"links":[]}
            """);
        handler.RegisterJson("/providers/zeta", """
            {"id":"zeta","title":"Zeta","type":"Catalog","links":[
              {"rel":"self","href":"/providers/zeta"},{"rel":"search","href":"/providers/zeta/search"},
              {"rel":"data","href":"/providers/zeta/collections"},
              {"rel":"child","href":"/api/catalogue/stac/catalogs/public"}]}
            """);
        handler.RegisterJson("/providers/alpha", """
            {"id":"alpha","title":"Alpha","type":"Catalog","links":[
              {"rel":"self","href":"/providers/alpha"},{"rel":"search","href":"/providers/alpha/search"},
              {"rel":"collections","href":"/providers/alpha/collections"}]}
            """);
        handler.RegisterJson("/api/catalogue/stac/catalogs/public", """
            {"id":"public","title":"Public","type":"Catalog","links":[
              {"rel":"self","href":"/api/catalogue/stac/catalogs/public"},
              {"rel":"child","href":"/providers/zeta"},{"rel":"child","href":"/providers/alpha"}]}
            """);

        var entries = await client.DiscoverCollectionsAsync(CatalogRoot.Public);

        Assert.Equal(3, entries.Count);
        Assert.Equal(["Alpha", "Zeta", "Zeta"], entries.Select(entry => entry.ProviderLabel));
        Assert.Equal(2, entries.Count(entry => entry.Collection.Id == "shared"));
        Assert.Equal(1, handler.Requests.Count(request =>
            request.Url.EndsWith("/api/catalogue/stac/catalogs/public", StringComparison.OrdinalIgnoreCase)));
        Assert.All(entries, entry => Assert.DoesNotContain("/catalogs/user/", entry.CatalogueUrl));
    }

    [Fact]
    public async Task SearchAsync_UsesSelectedEntriesAdvertisedSearchUrl()
    {
        var (client, handler) = CreateClient();
        handler.RegisterJson("/provider/search", """
            {"type":"FeatureCollection","features":[],"links":[],"numMatched":0}
            """);
        var collection = new StacCollection("duplicate", "Chosen", null, null, null, null, null);
        var entry = new CatalogCollectionEntry(
            CatalogRoot.Commercial, "Provider", "https://eodatahub.org.uk/provider",
            "https://eodatahub.org.uk/provider/search", collection);

        await client.SearchAsync(entry, new SearchFilters { Collections = ["duplicate"] });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://eodatahub.org.uk/provider/search", request.Url);
    }

    [Fact]
    public async Task SearchAsync_ReturnsItemsAndTotalCount()
    {
        var (client, handler) = CreateClient();
        handler.RegisterJson("/ceda/search", """
            {"type":"FeatureCollection","features":[],"links":[],"context":{"matched":12}}
            """);

        var result = await client.SearchAsync(CedaCatalog(), new SearchFilters());

        Assert.Equal(12, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetNextPageAsync_ExtractsNextPage()
    {
        var (client, handler) = CreateClient();
        handler.RegisterJson("page=2", """
            {"type":"FeatureCollection","features":[],"links":[{"rel":"next","href":"https://example.test/page=3"}]}
            """);

        var result = await client.GetNextPageAsync("https://eodatahub.org.uk/search?page=2");

        Assert.Equal("https://example.test/page=3", result.NextPageUrl);
    }

    [Fact]
    public async Task GetItemAsync_ReturnsNullOnlyForNotFound()
    {
        var (client, handler) = CreateClient();
        handler.RegisterStatus("missing", HttpStatusCode.NotFound);

        var item = await client.GetItemAsync("catalog", "collection", "missing");

        Assert.Null(item);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_TranslatesUnauthorizedResponse()
    {
        var (client, handler) = CreateClient();
        handler.RegisterJson("/api/catalogue/stac/catalogs/user/catalogs/testuser",
            "{\"detail\":\"secret-value-must-not-be-used\"}", HttpStatusCode.Unauthorized);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            client.ValidateCredentialsAsync("testuser"));

        Assert.Equal(ApiErrorCategory.Authentication, error.Category);
        Assert.Contains("invalid or expired", error.Message);
        Assert.DoesNotContain("secret-value", error.Message);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_UsesProtectedWorkspaceCatalogue()
    {
        var (client, handler) = CreateClient();
        handler.RegisterJson("/api/catalogue/stac/catalogs/user/catalogs/",
            "{\"id\":\"my workspace\",\"type\":\"Catalog\",\"links\":[]}");

        await client.ValidateCredentialsAsync("my workspace");

        var request = Assert.Single(handler.Requests);
        Assert.Contains("/catalogs/user/catalogs/", request.Url);
        Assert.DoesNotContain("/catalogs/public", request.Url);
    }
}

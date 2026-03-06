using System.IO;
using System.Net;
using System.Net.Http;
using Xunit;
using eodh.Models;
using eodh.Services;
using eodh.Tests.Helpers;

namespace eodh.Tests.Services;

/// <summary>
/// Req 5: Workspace &amp; Purchase — validates WorkspaceService correctly queries
/// workspace listing, asset retrieval, and commercial data quote/order APIs.
/// Uses synthetic fixtures (workspace endpoints require authentication).
/// </summary>
public class WorkspaceServiceTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static (WorkspaceService service, FixtureHttpHandler handler) CreateService()
    {
        var handler = new FixtureHttpHandler();
        var auth = new TestAuthService(handler);
        return (new WorkspaceService(auth), handler);
    }

    [Fact]
    public async Task GetWorkspacesAsync_ReturnsDeserializedWorkspaces()
    {
        var (service, handler) = CreateService();
        handler.Register("/api/workspaces", FixturePath("workspaces.json"));

        var workspaces = await service.GetWorkspacesAsync();

        Assert.NotNull(workspaces);
        Assert.Equal(2, workspaces.Count);
        Assert.Equal("ws-001", workspaces[0].Id);
        Assert.Equal("EO Research", workspaces[0].Name);
        Assert.Equal(2, workspaces[0].Members.Count);
        Assert.Contains(workspaces[0].Members, m => m.Role == "owner");
    }

    [Fact]
    public async Task GetWorkspacesAsync_ReturnsEmptyList_WhenNone()
    {
        var (service, handler) = CreateService();
        handler.RegisterJson("/api/workspaces", "[]");

        var workspaces = await service.GetWorkspacesAsync();

        Assert.NotNull(workspaces);
        Assert.Empty(workspaces);
    }

    [Fact]
    public async Task GetAssetsAsync_ReturnsAssetsForWorkspace()
    {
        var (service, handler) = CreateService();
        handler.Register("/assets", FixturePath("workspace_assets.json"));

        var assets = await service.GetAssetsAsync("ws-001");

        Assert.NotNull(assets);
        Assert.Equal(2, assets.Count);
        Assert.Equal("asset-001", assets[0].Id);
        Assert.Equal("sentinel2_ard", assets[0].CollectionId);
        Assert.Equal("available", assets[0].Status);
        Assert.Equal("pending", assets[1].Status);
    }

    [Fact]
    public async Task GetQuoteAsync_ReturnsQuoteResponse()
    {
        var (service, handler) = CreateService();
        handler.Register("/quote", FixturePath("quote_response.json"));

        var request = new QuoteRequest("Standard",
            [[[-1.5, 51.0], [0.5, 51.0], [0.5, 52.0], [-1.5, 52.0], [-1.5, 51.0]]]);
        var quote = await service.GetQuoteAsync(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/airbus/collections/airbus_phr_data/items/test",
            request);

        Assert.Equal(450.00m, quote.Price);
        Assert.Equal("EUR", quote.Currency);
        Assert.Equal(125.5, quote.Area);
    }

    [Fact]
    public async Task GetQuoteAsync_Throws_OnFailure()
    {
        var (service, handler) = CreateService();
        handler.RegisterJson("/quote", """{"error":"bad request"}""", HttpStatusCode.BadRequest);

        var request = new QuoteRequest("Standard", null);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetQuoteAsync("https://example.com/items/test", request));
        Assert.Contains("400", ex.Message);
    }

    [Fact]
    public async Task PlaceOrderAsync_ReturnsSuccess_WithLocationHeader()
    {
        var (service, handler) = CreateService();
        handler.RegisterJsonWithHeaders("/order",
            """{"status":"accepted"}""",
            new Dictionary<string, string>
            {
                ["Location"] = "https://eodatahub.org.uk/api/catalogue/stac/catalogs/user/catalogs/ws-001/catalogs/commercial-data/catalogs/airbus/items/test"
            },
            HttpStatusCode.Created);

        var request = new OrderRequest("Standard", "GB", "General Use", null);
        var result = await service.PlaceOrderAsync("https://example.com/items/test", request);

        Assert.True(result.Success);
        Assert.NotNull(result.LocationUrl);
        Assert.Contains("commercial-data", result.LocationUrl!);
    }

    [Fact]
    public async Task PlaceOrderAsync_ReturnsFailure_WithErrorMessage()
    {
        var (service, handler) = CreateService();
        handler.RegisterJson("/order", """{"error":"insufficient_funds"}""",
            HttpStatusCode.PaymentRequired);

        var request = new OrderRequest("Standard", "GB", "General Use", null);
        var result = await service.PlaceOrderAsync("https://example.com/items/test", request);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("PaymentRequired", result.ErrorMessage!);
    }
}

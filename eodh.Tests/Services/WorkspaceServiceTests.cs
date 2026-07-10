using System.Net;
using eodh.Services;
using eodh.Tests.Helpers;
using Xunit;

namespace eodh.Tests.Services;

public class WorkspaceServiceTests
{
    private static (WorkspaceService service, FixtureHttpHandler handler) CreateService()
    {
        var handler = new FixtureHttpHandler();
        return (new WorkspaceService(new TestAuthService(handler)), handler);
    }

    [Fact]
    public async Task GetCommercialRecordsAsync_FollowsCollectionAndNestedItemPagination()
    {
        var (service, handler) = CreateService();
        RegisterWorkspacePages(handler);

        var records = await service.GetCommercialRecordsAsync("testuser");

        Assert.Equal(3, records.Count);
        Assert.Contains(records, record => record.Status == "pending");
        Assert.Contains(records, record => record.Status == "failed");
        Assert.Contains(records, record => record.Status == "completed" && record.IsCompleted);
        Assert.Contains(records, record => record.ProviderLabel == "Airbus");
        Assert.Contains(records, record => record.ProviderLabel == "Planet");
        Assert.Contains(handler.Requests, request => request.Url.Contains(
            "/commercial-data/catalogs/airbus/collections/airbus-orders/items"));
        Assert.Contains(handler.Requests, request => request.Url.Contains("token=items2"));
        Assert.Contains(handler.Requests, request => request.Url.Contains("token=collections2"));
        Assert.DoesNotContain(handler.Requests, request => request.Url.Contains("/api/workspaces"));
    }

    [Fact]
    public async Task GetCommercialRecordsAsync_UsesAuthenticatedWorkspaceName()
    {
        var (service, handler) = CreateService();
        handler.RegisterJson("/commercial-data/collections",
            "{\"collections\":[],\"links\":[]}");

        await service.GetCommercialRecordsAsync("my workspace");

        Assert.Contains(handler.Requests, request =>
            request.Url.Contains("/catalogs/my%20workspace/") ||
            request.Url.Contains("/catalogs/my workspace/"));
    }

    [Fact]
    public async Task GetCommercialRecordsAsync_TranslatesAuthenticationFailure()
    {
        var (service, handler) = CreateService();
        handler.RegisterStatus("/commercial-data/collections", HttpStatusCode.Unauthorized);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            service.GetCommercialRecordsAsync("testuser"));

        Assert.Equal(ApiErrorCategory.Authentication, error.Category);
        Assert.Contains("invalid or expired", error.Message);
    }

    internal static void RegisterWorkspacePages(FixtureHttpHandler handler)
    {
        handler.RegisterJson("token=items2", """
            {"type":"FeatureCollection","features":[{
              "id":"completed-order","collection":"airbus-orders","geometry":null,
              "properties":{"order:status":"completed","order:id":"order-2","created":"2026-07-01T10:00:00Z","updated":"2026-07-02T10:00:00Z"},
              "assets":{"data":{"href":"https://example.test/data.tif","type":"image/tiff","roles":["data"]}},"links":[]}],"links":[]}
            """);
        handler.RegisterJson("/commercial-data/catalogs/airbus/collections/airbus-orders/items", """
            {"type":"FeatureCollection","features":[{
              "id":"pending-order","collection":"airbus-orders","geometry":null,
              "properties":{"order:status":"pending","order:id":"order-1","order:message":"Waiting for provider"},
              "assets":{},"links":[]}],"links":[{"rel":"next","href":"?token=items2"}]}
            """);
        handler.RegisterJson("/commercial-data/catalogs/planet/collections/planet-orders/items", """
            {"type":"FeatureCollection","features":[{
              "id":"failed-order","collection":"planet-orders","geometry":null,
              "properties":{"order_status":"failed","failure_message":"Provider rejected request","updated":"2026-07-03T10:00:00Z"},
              "assets":{},"links":[]}],"links":[]}
            """);
        handler.RegisterJson("token=collections2", """
            {"collections":[{"id":"planet-orders","title":"Planet orders","links":[
              {"rel":"items","href":"/api/catalogue/stac/catalogs/user/catalogs/testuser/catalogs/commercial-data/catalogs/planet/collections/planet-orders/items"}]}],"links":[]}
            """);
        handler.RegisterJson("/catalogs/testuser/catalogs/commercial-data/collections", """
            {"collections":[{"id":"airbus-orders","title":"Airbus orders","links":[
              {"rel":"items","href":"/api/catalogue/stac/catalogs/user/catalogs/testuser/catalogs/commercial-data/catalogs/airbus/collections/airbus-orders/items"}]}],
             "links":[{"rel":"next","href":"?token=collections2"}]}
            """);
    }
}

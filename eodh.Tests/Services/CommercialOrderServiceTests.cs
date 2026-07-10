using System.Net;
using System.Text.Json;
using eodh.Models;
using eodh.Services;
using eodh.Tests.Helpers;
using Xunit;

namespace eodh.Tests.Services;

public class CommercialOrderServiceTests
{
    private static (CommercialOrderService service, FixtureHttpHandler handler) CreateService()
    {
        var handler = new FixtureHttpHandler();
        return (new CommercialOrderService(new TestAuthService(handler)), handler);
    }

    [Fact]
    public async Task GetQuoteAsync_ParsesCurrentValueUnitsAndMessageContract()
    {
        var (service, handler) = CreateService();
        handler.RegisterJson("/quote", """
            {"value":450.25,"units":"EUR","message":"Minimum order applied"}
            """);

        var quote = await service.GetQuoteAsync(
            "https://eodatahub.org.uk/items/test",
            new QuoteRequest(null, "Standard", "Visual"));

        Assert.Equal(450.25m, quote.Value);
        Assert.Equal("EUR", quote.Units);
        Assert.Equal("Minimum order applied", quote.Message);
    }

    [Fact]
    public async Task GetQuoteAsync_OmitsPlanetLicenceAndIncludesBundle()
    {
        var (service, handler) = CreateService();
        handler.RegisterJson("/quote", "{\"value\":12,\"units\":\"USD\"}");

        await service.GetQuoteAsync(
            "https://eodatahub.org.uk/items/planet",
            new QuoteRequest(
                [[[-1, 50], [0, 50], [0, 51], [-1, 51], [-1, 50]]],
                null,
                "Analytic"));

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        Assert.False(body.RootElement.TryGetProperty("licence", out _));
        Assert.Equal("Analytic", body.RootElement.GetProperty("productBundle").GetString());
        Assert.True(body.RootElement.TryGetProperty("coordinates", out _));
    }

    [Fact]
    public async Task PlaceOrderAsync_SerializesSarConditionalOptionsAndUsesMockOnly()
    {
        var (service, handler) = CreateService();
        handler.RegisterJsonWithHeaders(
            "/order",
            "{\"status\":\"accepted\"}",
            new Dictionary<string, string> { ["Location"] = "https://example.test/ordered" },
            HttpStatusCode.Created);

        var result = await service.PlaceOrderAsync(
            "https://eodatahub.org.uk/items/sar",
            new OrderRequest(
                "SSC", null, null, "Single User Licence",
                new RadarOptions("rapid", null, null)));

        Assert.True(result.Success);
        Assert.Equal("https://example.test/ordered", result.LocationUrl);
        var captured = Assert.Single(handler.Requests);
        Assert.EndsWith("/order", captured.Url);
        using var body = JsonDocument.Parse(captured.Body!);
        var radar = body.RootElement.GetProperty("radarOptions");
        Assert.Equal("rapid", radar.GetProperty("orbit").GetString());
        Assert.False(radar.TryGetProperty("resolutionVariant", out _));
        Assert.False(radar.TryGetProperty("projection", out _));
    }

    [Fact]
    public async Task PlaceOrderAsync_SerializesAirbusOpticalContract()
    {
        var (service, handler) = CreateService();
        handler.RegisterJson("/order", "{\"status\":\"accepted\"}", HttpStatusCode.Created);

        await service.PlaceOrderAsync(
            "https://eodatahub.org.uk/items/optical",
            new OrderRequest(
                "General Use",
                [[[-1, 50], [0, 50], [0, 51], [-1, 51], [-1, 50]]],
                "GB",
                "Standard",
                null));

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        Assert.Equal("General Use", body.RootElement.GetProperty("productBundle").GetString());
        Assert.Equal("GB", body.RootElement.GetProperty("endUserCountry").GetString());
        Assert.Equal("Standard", body.RootElement.GetProperty("licence").GetString());
        Assert.True(body.RootElement.TryGetProperty("coordinates", out _));
        Assert.False(body.RootElement.TryGetProperty("radarOptions", out _));
    }

    [Theory]
    [InlineData("MGD", "RE", null)]
    [InlineData("GEC", "SE", "UTM")]
    [InlineData("EEC", "RE", "UPS")]
    public async Task PlaceOrderAsync_SerializesSarBundleVariants(
        string bundle,
        string resolutionVariant,
        string? projection)
    {
        var (service, handler) = CreateService();
        handler.RegisterJson("/order", "{\"status\":\"accepted\"}", HttpStatusCode.Created);

        await service.PlaceOrderAsync(
            "https://eodatahub.org.uk/items/sar",
            new OrderRequest(
                bundle,
                null,
                null,
                "Single User Licence",
                new RadarOptions("science", resolutionVariant, projection)));

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body!);
        var radar = body.RootElement.GetProperty("radarOptions");
        Assert.Equal(resolutionVariant, radar.GetProperty("resolutionVariant").GetString());
        if (projection == null)
            Assert.False(radar.TryGetProperty("projection", out _));
        else
            Assert.Equal(projection, radar.GetProperty("projection").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{\"detail\":\"bad key\"}", ApiErrorCategory.Authentication)]
    [InlineData(HttpStatusCode.Forbidden, "{\"detail\":\"Provider credentials must be linked\"}", ApiErrorCategory.LinkedProviderMissing)]
    [InlineData(HttpStatusCode.BadRequest, "{\"message\":\"productBundle is invalid\"}", ApiErrorCategory.Validation)]
    [InlineData(HttpStatusCode.InternalServerError, "not-json", ApiErrorCategory.Server)]
    public async Task GetQuoteAsync_UsesCentralSafeErrorTranslation(
        HttpStatusCode status,
        string body,
        ApiErrorCategory expectedCategory)
    {
        var (service, handler) = CreateService();
        handler.RegisterJson("/quote", body, status);

        var error = await Assert.ThrowsAsync<ApiException>(() => service.GetQuoteAsync(
            "https://eodatahub.org.uk/items/test",
            new QuoteRequest(null, "Standard", "Visual")));

        Assert.Equal(expectedCategory, error.Category);
        Assert.DoesNotContain("test-token", error.Message);
        Assert.DoesNotContain("not-json", error.Message);
    }

    [Fact]
    public async Task GetQuoteAsync_FormatsValidationDetailArray()
    {
        var (service, handler) = CreateService();
        handler.RegisterJson("/quote", """
            {"detail":[{"loc":["body","productBundle"],"msg":"field required"}]}
            """, HttpStatusCode.UnprocessableEntity);

        var error = await Assert.ThrowsAsync<ApiException>(() => service.GetQuoteAsync(
            "https://eodatahub.org.uk/items/test",
            new QuoteRequest(null, "Standard", null)));

        Assert.Contains("body.productBundle", error.Message);
        Assert.Contains("field required", error.Message);
    }
}

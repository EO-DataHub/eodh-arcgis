using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using eodh.Models;

namespace eodh.Services;

/// <summary>
/// Focused transport for commercial quotes and orders.
/// </summary>
public sealed class CommercialOrderService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AuthService _authService;

    public CommercialOrderService(AuthService authService)
    {
        _authService = authService;
    }

    public async Task<QuoteResponse> GetQuoteAsync(
        string itemSelfHref,
        QuoteRequest request,
        CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        using var content = CreateContent(request);
        using var response = await client.PostAsync(AppendOperation(itemSelfHref, "quote"), content, ct);
        await ApiResponse.EnsureSuccessAsync(response, "commercial quote", ct, _authService.ApiToken);

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<QuoteResponse>(json, ReadOptions)
            ?? throw new HttpRequestException("The quote response was empty.");
    }

    public async Task<OrderResult> PlaceOrderAsync(
        string itemSelfHref,
        OrderRequest request,
        CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        using var content = CreateContent(request);
        using var response = await client.PostAsync(AppendOperation(itemSelfHref, "order"), content, ct);
        await ApiResponse.EnsureSuccessAsync(response, "commercial order", ct, _authService.ApiToken);
        return new OrderResult(true, response.Headers.Location?.ToString(), null);
    }

    private static StringContent CreateContent<T>(T request) =>
        new(JsonSerializer.Serialize(request, WriteOptions), Encoding.UTF8, "application/json");

    private static string AppendOperation(string itemSelfHref, string operation) =>
        $"{itemSelfHref.TrimEnd('/')}/{operation}";
}

using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using eodh.Models;

namespace eodh.Services;

/// <summary>
/// Service for EODH organisational workspace and commercial data purchase APIs.
/// </summary>
public class WorkspaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Omits null fields from request bodies (e.g. coordinates, licence).</summary>
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AuthService _authService;

    public WorkspaceService(AuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Get the list of workspaces the user has access to.
    /// </summary>
    public async Task<List<WorkspaceInfo>> GetWorkspacesAsync(CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        var response = await client.GetAsync("/api/workspaces", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<WorkspaceInfo>>(json, JsonOptions) ?? [];
    }

    /// <summary>
    /// Get assets owned by or shared with the current user.
    /// </summary>
    public async Task<List<WorkspaceAsset>> GetAssetsAsync(
        string workspaceId, CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        var response = await client.GetAsync($"/api/workspaces/{workspaceId}/assets", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<WorkspaceAsset>>(json, JsonOptions) ?? [];
    }

    /// <summary>
    /// Request a price quote for a commercial item.
    /// POST {itemSelfHref}/quote
    /// Throws HttpRequestException with details on failure.
    /// </summary>
    public async Task<QuoteResponse> GetQuoteAsync(
        string itemSelfHref, QuoteRequest request, CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        var url = $"{itemSelfHref}/quote";
        var body = JsonSerializer.Serialize(request, WriteOptions);
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Quote failed ({(int)response.StatusCode} {response.StatusCode}): {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<QuoteResponse>(json, JsonOptions)
            ?? throw new HttpRequestException("Quote response was empty");
    }

    /// <summary>
    /// Place an order for a commercial item.
    /// POST {itemSelfHref}/order
    /// Returns OrderResult with the Location header URL where the item will appear.
    /// </summary>
    public async Task<OrderResult> PlaceOrderAsync(
        string itemSelfHref, OrderRequest request, CancellationToken ct = default)
    {
        using var client = _authService.CreateHttpClient();
        var url = $"{itemSelfHref}/order";
        var body = JsonSerializer.Serialize(request, WriteOptions);
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            return new OrderResult(false, null, $"Order failed ({response.StatusCode}): {errorBody}");
        }

        var locationUrl = response.Headers.Location?.ToString();
        return new OrderResult(true, locationUrl, null);
    }
}

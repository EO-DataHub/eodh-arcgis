using System.Net.Http;
using System.Text.Json;
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

}

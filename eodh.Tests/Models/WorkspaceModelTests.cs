using Xunit;
using eodh.Models;

namespace eodh.Tests.Models;

/// <summary>
/// Req 5: Workspace & Purchase — validates workspace model records can represent
/// all roles, statuses, and purchase information for organisational data management.
/// </summary>
public class WorkspaceModelTests
{
    [Theory]
    [InlineData("owner")]
    [InlineData("member")]
    [InlineData("viewer")]
    public void WorkspaceMember_CanRepresentAllRoles(string role)
    {
        var member = new WorkspaceMember("testuser", role);

        Assert.Equal("testuser", member.Username);
        Assert.Equal(role, member.Role);
    }

    [Theory]
    [InlineData("available")]
    [InlineData("pending")]
    [InlineData("purchased")]
    public void WorkspaceAsset_CanRepresentAllStatuses(string status)
    {
        var asset = new WorkspaceAsset("asset-1", "Test Asset", null, null, status, null);

        Assert.Equal(status, asset.Status);
    }

    [Fact]
    public void QuoteRequest_CanOmitOptionalFields()
    {
        var request = new QuoteRequest(null, null);

        Assert.Null(request.Licence);
        Assert.Null(request.Coordinates);
    }

    [Fact]
    public void QuoteResponse_ContainsPriceAndCurrency()
    {
        var response = new QuoteResponse(450.00m, "EUR", 125.5, "km2");

        Assert.Equal(450.00m, response.Price);
        Assert.Equal("EUR", response.Currency);
        Assert.Equal(125.5, response.Area);
    }

    [Fact]
    public void OrderRequest_ContainsAllAirbusOpticalFields()
    {
        var request = new OrderRequest("Standard", "GB", "General Use",
            [[[-1.5, 51.0], [0.5, 51.0], [0.5, 52.0], [-1.5, 52.0], [-1.5, 51.0]]]);

        Assert.Equal("Standard", request.Licence);
        Assert.Equal("GB", request.EndUserCountry);
        Assert.Equal("General Use", request.ProductBundle);
        Assert.NotNull(request.Coordinates);
        // Outer array has 1 ring, ring has 5 points
        Assert.Single(request.Coordinates!);
        Assert.Equal(5, request.Coordinates![0].Length);
    }

    [Fact]
    public void OrderResult_RepresentsSuccessWithLocation()
    {
        var result = new OrderResult(true, "https://example.com/ordered-item", null);

        Assert.True(result.Success);
        Assert.NotNull(result.LocationUrl);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void OrderResult_RepresentsFailureWithError()
    {
        var result = new OrderResult(false, null, "Insufficient funds");

        Assert.False(result.Success);
        Assert.Null(result.LocationUrl);
        Assert.Equal("Insufficient funds", result.ErrorMessage);
    }

    [Fact]
    public void WorkspaceAsset_PurchasedAtCanBeNull()
    {
        var asset = new WorkspaceAsset("asset-1", "Test", null, null, "available", null);

        Assert.Null(asset.PurchasedAt);
    }

    [Fact]
    public void WorkspaceInfo_ContainsMembersAndAssets()
    {
        var members = new List<WorkspaceMember>
        {
            new("user1", "owner"),
            new("user2", "member")
        };
        var assets = new List<WorkspaceAsset>
        {
            new("a1", "Asset 1", null, null, "available", null)
        };
        var workspace = new WorkspaceInfo("ws-1", "Test Workspace", "A test", members, assets);

        Assert.Equal(2, workspace.Members.Count);
        Assert.Single(workspace.Assets);
        Assert.Equal("Test Workspace", workspace.Name);
    }
}

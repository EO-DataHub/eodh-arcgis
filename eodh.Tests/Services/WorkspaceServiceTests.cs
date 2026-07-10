using eodh.Services;
using eodh.Tests.Helpers;
using Xunit;

namespace eodh.Tests.Services;

/// <summary>
/// Legacy workspace tests retained until the workspace STAC view replaces
/// the unsupported list contract.
/// </summary>
public class WorkspaceServiceTests
{
    private static (WorkspaceService service, FixtureHttpHandler handler) CreateService()
    {
        var handler = new FixtureHttpHandler();
        return (new WorkspaceService(new TestAuthService(handler)), handler);
    }

    [Fact]
    public async Task GetWorkspacesAsync_ReturnsEmptyList_WhenNone()
    {
        var (service, handler) = CreateService();
        handler.RegisterJson("/api/workspaces", "[]");

        Assert.Empty(await service.GetWorkspacesAsync());
    }
}

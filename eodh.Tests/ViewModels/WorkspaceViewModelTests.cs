using System.IO;
using eodh.Services;
using eodh.Tests.Helpers;
using eodh.ViewModels;
using Xunit;

namespace eodh.Tests.ViewModels;

/// <summary>
/// Req 5: Workspace &amp; Purchase — documents expected WorkspaceViewModel behaviour:
/// workspace listing, member/asset display, refresh, and shared organisational data.
/// All tests require ArcGIS Pro SDK runtime.
/// </summary>
[Collection("ArcGIS-SDK")]
[Trait("Category", "RequiresArcGIS")]
public class WorkspaceViewModelTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static (WorkspaceViewModel vm, FixtureHttpHandler handler) CreateVm()
    {
        var handler = new FixtureHttpHandler();
        handler.Register("/api/workspaces", FixturePath("workspaces.json"));
        var auth = new TestAuthService(handler);
        return (new WorkspaceViewModel(auth), handler);
    }

    [Fact]
    public async Task InitializeAsync_LoadsWorkspaces()
    {
        var (vm, _) = CreateVm();

        await vm.InitializeAsync();

        Assert.Equal(2, vm.Workspaces.Count);
        Assert.Equal("EO Research", vm.Workspaces[0].Name);
        Assert.Equal("Sentinel Monitoring", vm.Workspaces[1].Name);
    }

    [Fact]
    public async Task InitializeAsync_SelectsFirstWorkspace()
    {
        var (vm, _) = CreateVm();

        await vm.InitializeAsync();

        Assert.NotNull(vm.SelectedWorkspace);
        Assert.Equal("ws-001", vm.SelectedWorkspace!.Id);
    }

    [Fact]
    public async Task SelectedWorkspace_PopulatesMembers()
    {
        var (vm, _) = CreateVm();
        await vm.InitializeAsync();

        // First workspace has 2 members
        Assert.Equal(2, vm.Members.Count);
        Assert.Contains(vm.Members, m => m.Username == "testuser");
        Assert.Contains(vm.Members, m => m.Username == "collaborator");
    }

    [Fact]
    public async Task SelectedWorkspace_PopulatesAssets()
    {
        var (vm, _) = CreateVm();
        await vm.InitializeAsync();

        // First workspace has 1 asset
        Assert.Single(vm.Assets);
        Assert.Equal("Sentinel-2 Scene", vm.Assets[0].Name);
    }

    [Fact]
    public async Task SelectedWorkspace_Null_ClearsMembersAndAssets()
    {
        var (vm, _) = CreateVm();
        await vm.InitializeAsync();

        Assert.True(vm.Members.Count > 0);

        vm.SelectedWorkspace = null;

        Assert.Empty(vm.Members);
        Assert.Empty(vm.Assets);
    }

    [Fact]
    public async Task RefreshCommand_ReloadsWorkspaces()
    {
        var (vm, _) = CreateVm();
        await vm.InitializeAsync();

        var initialCount = vm.Workspaces.Count;

        // Refresh reloads from the same fixture
        vm.RefreshCommand.Execute(null);
        await Task.Delay(200);

        Assert.Equal(initialCount, vm.Workspaces.Count);
    }

    [Fact]
    public async Task Members_ContainRoleInformation()
    {
        var (vm, _) = CreateVm();
        await vm.InitializeAsync();

        var owner = vm.Members.First(m => m.Username == "testuser");
        Assert.Equal("owner", owner.Role);

        var member = vm.Members.First(m => m.Username == "collaborator");
        Assert.Equal("member", member.Role);
    }

    [Fact]
    public async Task WorkspaceSupportsSharedAssets()
    {
        var (vm, _) = CreateVm();
        await vm.InitializeAsync();

        // First workspace has 2 members sharing assets
        Assert.True(vm.Members.Count >= 2);

        // Select second workspace to verify switching works
        vm.SelectedWorkspace = vm.Workspaces[1];
        Assert.Single(vm.Members);
    }
}

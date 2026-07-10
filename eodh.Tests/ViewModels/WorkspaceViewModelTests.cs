using System.Net;
using eodh.Services;
using eodh.Tests.Helpers;
using eodh.ViewModels;
using Xunit;

namespace eodh.Tests.ViewModels;

[Collection("ArcGIS-SDK")]
[Trait("Category", "RequiresArcGIS")]
public class WorkspaceViewModelTests
{
    [Fact]
    public async Task InitializeAsync_LoadsAllCommercialOrderStatesForCurrentWorkspace()
    {
        var (vm, _) = CreateVm();

        await vm.InitializeAsync();

        Assert.Equal("testuser", vm.WorkspaceName);
        Assert.Equal(3, vm.Records.Count);
        Assert.Contains(vm.Records, record => record.Status == "pending");
        Assert.Contains(vm.Records, record => record.Status == "failed");
        Assert.Contains(vm.Records, record => record.Status == "completed");
        Assert.False(vm.HasError);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task Filters_ApplyProviderAndStatusWithoutReloading()
    {
        var (vm, _) = CreateVm();
        await vm.InitializeAsync();

        vm.SelectedProvider = "Airbus";
        vm.SelectedStatus = "completed";

        var record = Assert.Single(vm.Records);
        Assert.Equal("completed-order", record.ItemId);
    }

    [Fact]
    public async Task OnlyCompletedRecordWithLoadableAsset_ExposesLoadAction()
    {
        var (vm, _) = CreateVm();
        await vm.InitializeAsync();

        Assert.True(vm.Records.Single(record => record.Status == "completed").CanLoadIntoMap);
        Assert.False(vm.Records.Single(record => record.Status == "pending").CanLoadIntoMap);
        Assert.False(vm.Records.Single(record => record.Status == "failed").CanLoadIntoMap);
    }

    [Fact]
    public async Task AuthenticationFailure_IsExplicitAndRetryable()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterStatus("/commercial-data/collections", HttpStatusCode.Unauthorized);
        var auth = new TestAuthService(handler);
        var vm = new WorkspaceViewModel(auth);

        await vm.InitializeAsync();

        Assert.True(vm.HasError);
        Assert.True(vm.IsAuthenticationError);
        Assert.Contains("invalid or expired", vm.ErrorMessage);
        Assert.True(vm.RefreshCommand.CanExecute(null));
    }

    private static (WorkspaceViewModel vm, FixtureHttpHandler handler) CreateVm()
    {
        var handler = new FixtureHttpHandler();
        eodh.Tests.Services.WorkspaceServiceTests.RegisterWorkspacePages(handler);
        var auth = new TestAuthService(handler);
        return (new WorkspaceViewModel(auth), handler);
    }
}

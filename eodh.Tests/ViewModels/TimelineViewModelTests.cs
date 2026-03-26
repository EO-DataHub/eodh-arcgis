using eodh.Models;
using eodh.Services;
using eodh.Tests.Helpers;
using eodh.ViewModels;
using Xunit;

namespace eodh.Tests.ViewModels;

/// <summary>
/// Req 4: Preview UX — documents expected TimelineViewModel behaviour:
/// chronological ordering, previous/next navigation, and date display.
/// All tests require ArcGIS Pro SDK runtime.
/// </summary>
[Collection("ArcGIS-SDK")]
[Trait("Category", "RequiresArcGIS")]
public class TimelineViewModelTests
{
    private static TimelineViewModel CreateVm()
    {
        var handler = new FixtureHttpHandler();
        var auth = new TestAuthService(handler);
        var stacClient = new StacClient(auth);
        var layerService = new LayerService(auth);
        var thumbnailCache = new ThumbnailCache();
        return new TimelineViewModel(stacClient, layerService, thumbnailCache);
    }

    private static List<StacItem> CreateDatedItems(params string[] dates)
    {
        return dates.Select((dt, i) =>
            new StacItem($"item-{i}", "sentinel2_ard", null, null,
                new StacItemProperties(dt, null, null, null, null, null, null, null, null),
                new Dictionary<string, StacAsset>
                {
                    ["data"] = new("https://example.com/data.tif",
                        "image/tiff; application=geotiff", "Data", ["data"], null)
                },
                null)).ToList();
    }

    [Fact]
    public void LoadResults_SortsItemsByDate()
    {
        var vm = CreateVm();
        var items = CreateDatedItems(
            "2026-02-15T10:00:00Z",
            "2026-01-10T10:00:00Z",
            "2026-02-01T10:00:00Z");

        vm.LoadResults(items);

        Assert.Equal(3, vm.TimelineEntries.Count);
        // Should be sorted ascending by date
        Assert.Equal("01/10", vm.TimelineEntries[0].DateLabel);
        Assert.Equal("02/01", vm.TimelineEntries[1].DateLabel);
        Assert.Equal("02/15", vm.TimelineEntries[2].DateLabel);
    }

    [Fact]
    public void LoadResults_FiltersItemsWithoutDate()
    {
        var vm = CreateVm();
        var items = new List<StacItem>
        {
            new("item-1", "col", null, null,
                new StacItemProperties("2026-01-15T10:00:00Z", null, null, null, null, null, null, null, null),
                null, null),
            new("item-2", "col", null, null,
                new StacItemProperties(null, null, null, null, null, null, null, null, null),
                null, null),
            new("item-3", "col", null, null,
                null, null, null)
        };

        vm.LoadResults(items);

        // Only item-1 has a parseable date
        Assert.Single(vm.TimelineEntries);
    }

    [Fact]
    public void LoadResults_SelectsLastEntry()
    {
        var vm = CreateVm();
        var items = CreateDatedItems(
            "2026-01-10T10:00:00Z",
            "2026-01-20T10:00:00Z",
            "2026-01-30T10:00:00Z");

        vm.LoadResults(items);

        // Last entry (most recent) should be auto-selected
        Assert.True(vm.TimelineEntries[2].IsSelected);
        Assert.False(vm.TimelineEntries[0].IsSelected);
        Assert.False(vm.TimelineEntries[1].IsSelected);
    }

    [Fact]
    public void LoadResults_ClearsPreviousEntries()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateDatedItems(
            "2026-01-10T10:00:00Z",
            "2026-01-20T10:00:00Z"));
        Assert.Equal(2, vm.TimelineEntries.Count);

        vm.LoadResults(CreateDatedItems(
            "2026-02-01T10:00:00Z"));
        Assert.Single(vm.TimelineEntries);
    }

    [Fact]
    public void PreviousCommand_CanExecute_WhenNotAtStart()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateDatedItems(
            "2026-01-10T10:00:00Z",
            "2026-01-20T10:00:00Z",
            "2026-01-30T10:00:00Z"));

        // Last entry is auto-selected — Previous should be available
        Assert.True(vm.PreviousCommand.CanExecute(null));
    }

    [Fact]
    public void PreviousCommand_CannotExecute_AtStart()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateDatedItems(
            "2026-01-10T10:00:00Z",
            "2026-01-20T10:00:00Z",
            "2026-01-30T10:00:00Z"));

        // Navigate to first entry
        vm.PreviousCommand.Execute(null); // index 2 to 1
        vm.PreviousCommand.Execute(null); // index 1 to 0

        Assert.False(vm.PreviousCommand.CanExecute(null));
    }

    [Fact]
    public void NextCommand_CanExecute_WhenNotAtEnd()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateDatedItems(
            "2026-01-10T10:00:00Z",
            "2026-01-20T10:00:00Z",
            "2026-01-30T10:00:00Z"));

        // Navigate to first entry
        vm.PreviousCommand.Execute(null);
        vm.PreviousCommand.Execute(null);

        Assert.True(vm.NextCommand.CanExecute(null));
    }

    [Fact]
    public void NextCommand_CannotExecute_AtEnd()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateDatedItems(
            "2026-01-10T10:00:00Z",
            "2026-01-20T10:00:00Z",
            "2026-01-30T10:00:00Z"));

        // Last entry is already selected
        Assert.False(vm.NextCommand.CanExecute(null));
    }

    [Fact]
    public void SelectEntry_DeselectsPrevious_SelectsNew()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateDatedItems(
            "2026-01-10T10:00:00Z",
            "2026-01-20T10:00:00Z",
            "2026-01-30T10:00:00Z"));

        // Last (index 2) is selected
        Assert.True(vm.TimelineEntries[2].IsSelected);

        // Navigate to previous
        vm.PreviousCommand.Execute(null);

        Assert.True(vm.TimelineEntries[1].IsSelected);
        Assert.False(vm.TimelineEntries[2].IsSelected);
    }

    [Fact]
    public void TimelineEntry_DateLabel_ShowsMonthDay()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateDatedItems("2026-03-15T10:00:00Z"));

        Assert.Equal("03/15", vm.TimelineEntries[0].DateLabel);
    }
}

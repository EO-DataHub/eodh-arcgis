using eodh.Models;
using eodh.Services;
using eodh.Tests.Helpers;
using eodh.ViewModels;
using Xunit;

namespace eodh.Tests.ViewModels;

/// <summary>
/// Req 3: Results Display — documents expected ResultsViewModel behaviour:
/// result list population, metadata display, and loading into map.
/// Req 6: Loading Data — double-click loads best asset, asset selection priority.
/// All tests require ArcGIS Pro SDK runtime.
/// </summary>
[Collection("ArcGIS-SDK")]
[Trait("Category", "RequiresArcGIS")]
public class ResultsViewModelTests
{
    private static ResultsViewModel CreateVm()
    {
        var handler = new FixtureHttpHandler();
        var auth = new TestAuthService(handler);
        var stacClient = new StacClient(auth);
        var layerService = new LayerService(auth);
        var thumbnailCache = new ThumbnailCache();
        return new ResultsViewModel(stacClient, layerService, thumbnailCache);
    }

    private static List<StacItem> CreateTestItems(int count)
    {
        var items = new List<StacItem>();
        for (var i = 0; i < count; i++)
        {
            items.Add(new StacItem($"item-{i}", "sentinel2_ard", null, null,
                new StacItemProperties($"2026-01-{i + 1:D2}T10:00:00Z", 5.0, 10.0, null, null, null, null, null, null),
                new Dictionary<string, StacAsset>
                {
                    ["data"] = new("https://example.com/data.tif",
                        "image/tiff; application=geotiff", "Data", ["data"], null)
                },
                null));
        }
        return items;
    }

    [Fact]
    public void LoadResults_PopulatesResultsCollection()
    {
        var vm = CreateVm();
        var items = CreateTestItems(10);

        vm.LoadResults(items, null);

        Assert.Equal(10, vm.Results.Count);
    }

    [Fact]
    public void LoadResults_ClearsPreviousResults()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateTestItems(5), null);
        Assert.Equal(5, vm.Results.Count);

        vm.LoadResults(CreateTestItems(3), null);
        Assert.Equal(3, vm.Results.Count);
    }

    [Fact]
    public void LoadResults_SetsStatusMessage()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateTestItems(7), null);

        Assert.Equal("7 results", vm.StatusMessage);
    }

    [Fact]
    public void LoadSelectedCommand_CanExecute_WhenItemSelected()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateTestItems(3), null);
        vm.SelectedItem = vm.Results[0];

        Assert.True(vm.LoadSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void LoadSelectedCommand_CannotExecute_WhenNoSelection()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateTestItems(3), null);
        vm.SelectedItem = null;

        Assert.False(vm.LoadSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void LoadResults_PassesAoiBbox_ToResultItems()
    {
        var vm = CreateVm();
        var items = new List<StacItem>
        {
            new("item-1", "col", null,
                [0, 0, 10, 10],  // fully inside AOI
                new StacItemProperties("2026-01-01T10:00:00Z", null, null, null, null, null, null, null, null),
                null, null)
        };
        var filters = new SearchFilters { Bbox = [0, 0, 10, 10] };

        vm.LoadResults(items, filters);

        Assert.Single(vm.Results);
        Assert.Equal("100% overlap", vm.Results[0].AoiOverlap);
    }

    [Fact]
    public void LoadResults_AoiOverlapNull_WhenNoFilters()
    {
        var vm = CreateVm();
        var items = new List<StacItem>
        {
            new("item-1", "col", null,
                [0, 0, 10, 10],
                new StacItemProperties("2026-01-01T10:00:00Z", null, null, null, null, null, null, null, null),
                null, null)
        };

        vm.LoadResults(items, null);

        Assert.Single(vm.Results);
        Assert.Null(vm.Results[0].AoiOverlap);
    }

    [Fact]
    public void LoadResults_HasResultsFalse_WhenNoResults()
    {
        var vm = CreateVm();
        vm.LoadResults([], null);

        Assert.False(vm.HasResults);
    }

    [Fact]
    public void LoadResults_HasResultsTrue_WhenResultsExist()
    {
        var vm = CreateVm();
        vm.LoadResults(CreateTestItems(3), null);

        Assert.True(vm.HasResults);
    }

    [Fact]
    public void LoadResults_PassesCollectionLicense_ToResultItems()
    {
        var vm = CreateVm();
        var items = new List<StacItem>
        {
            new("item-1", "col", null, null,
                new StacItemProperties("2026-01-01T10:00:00Z", null, null, null, null, null, null, null, null),
                null, null)
        };

        vm.LoadResults(items, null, "OGL-UK-3.0");

        Assert.Single(vm.Results);
        Assert.Equal("License: OGL-UK-3.0", vm.Results[0].LicenseInfo);
    }
}

/// <summary>
/// Req 3: Results Display — documents expected ResultItemViewModel behaviour:
/// formatted metadata properties and asset selection for map loading.
/// </summary>
[Collection("ArcGIS-SDK")]
[Trait("Category", "RequiresArcGIS")]
public class ResultItemViewModelTests
{
    private static ResultItemViewModel CreateItemVm(StacItem item, double[]? aoiBbox = null, string? collectionLicense = null)
    {
        var thumbnailCache = new ThumbnailCache();
        var layerService = new LayerService(new AuthService());
        return new ResultItemViewModel(item, thumbnailCache, layerService, aoiBbox, collectionLicense);
    }

    [Fact]
    public void AcquisitionDate_FormattedFromItemProperties()
    {
        var item = new StacItem("test", "col", null, null,
            new StacItemProperties("2026-01-15T14:30:00Z", null, null, null, null, null, null, null, null),
            null, null);
        var vm = CreateItemVm(item);

        Assert.Equal("2026-01-15 14:30", vm.AcquisitionDate);
    }

    [Fact]
    public void Resolution_FormattedWithUnit()
    {
        var item = new StacItem("test", "col", null, null,
            new StacItemProperties("2026-01-15T10:00:00Z", null, 10.0, null, null, null, null, null, null),
            null, null);
        var vm = CreateItemVm(item);

        Assert.Equal("10.0 m", vm.Resolution);
    }

    [Fact]
    public void CloudCover_FormattedAsPercentage()
    {
        var item = new StacItem("test", "col", null, null,
            new StacItemProperties("2026-01-15T10:00:00Z", 15.3, null, null, null, null, null, null, null),
            null, null);
        var vm = CreateItemVm(item);

        Assert.Equal("15.3%", vm.CloudCover);
    }

    [Fact]
    public void AoiOverlap_FormattedWhenBboxProvided()
    {
        var item = new StacItem("test", "col", null,
            [-5, -5, 5, 5],  // item bbox: 10x10
            new StacItemProperties("2026-01-15T10:00:00Z", null, null, null, null, null, null, null, null),
            null, null);
        double[] aoi = [0, 0, 10, 10];  // AOI: 10x10, overlap = 5x5=25 / 100 = 25%
        var vm = CreateItemVm(item, aoi);

        Assert.Equal("25% overlap", vm.AoiOverlap);
    }

    [Fact]
    public void AoiOverlap_NullWhenNoAoiBbox()
    {
        var item = new StacItem("test", "col", null,
            [0, 0, 10, 10],
            new StacItemProperties("2026-01-15T10:00:00Z", null, null, null, null, null, null, null, null),
            null, null);
        var vm = CreateItemVm(item);

        Assert.Null(vm.AoiOverlap);
    }

    [Fact]
    public void LocationalAccuracy_FormattedWhenRmsePresent()
    {
        var item = new StacItem("test", "col", null, null,
            new StacItemProperties("2026-01-15T10:00:00Z", null, null, null, null, null, null, null, 12.5),
            null, null);
        var vm = CreateItemVm(item);

        Assert.Equal("RMSE: 12.5 m", vm.LocationalAccuracy);
    }

    [Fact]
    public void LocationalAccuracy_NullWhenRmseAbsent()
    {
        var item = new StacItem("test", "col", null, null,
            new StacItemProperties("2026-01-15T10:00:00Z", null, null, null, null, null, null, null, null),
            null, null);
        var vm = CreateItemVm(item);

        Assert.Null(vm.LocationalAccuracy);
    }

    [Fact]
    public void LicenseInfo_FormattedWhenLicenseProvided()
    {
        var item = new StacItem("test", "col", null, null,
            new StacItemProperties("2026-01-15T10:00:00Z", null, null, null, null, null, null, null, null),
            null, null);
        var vm = CreateItemVm(item, collectionLicense: "proprietary");

        Assert.Equal("License: proprietary", vm.LicenseInfo);
    }

    [Fact]
    public void LicenseInfo_NullWhenNoLicense()
    {
        var item = new StacItem("test", "col", null, null,
            new StacItemProperties("2026-01-15T10:00:00Z", null, null, null, null, null, null, null, null),
            null, null);
        var vm = CreateItemVm(item);

        Assert.Null(vm.LicenseInfo);
    }

    [Fact]
    public void IsCommercial_True_WhenSelfLinkIsCommercial()
    {
        var item = new StacItem("test", "airbus_phr_data", null, null, null, null,
            [new StacLink("self",
                "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/airbus/collections/airbus_phr_data/items/test",
                null, null)]);
        var vm = CreateItemVm(item);

        Assert.True(vm.IsCommercial);
        Assert.Equal(CommercialProvider.AirbusOptical, vm.Provider);
    }

    [Fact]
    public void IsCommercial_False_WhenSelfLinkIsPublic()
    {
        var item = new StacItem("test", "sentinel2_ard", null, null, null, null,
            [new StacLink("self",
                "https://eodatahub.org.uk/api/catalogue/stac/catalogs/supported-datasets/catalogs/ceda/collections/sentinel2_ard/items/test",
                null, null)]);
        var vm = CreateItemVm(item);

        Assert.False(vm.IsCommercial);
    }

    [Fact]
    public void Commercial_HasLicenceOptions_ForAirbusOptical()
    {
        var item = new StacItem("test", "airbus_phr_data", null, null, null, null,
            [new StacLink("self",
                "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/airbus/collections/airbus_phr_data/items/test",
                null, null)]);
        var vm = CreateItemVm(item);

        Assert.NotNull(vm.LicenceOptions);
        Assert.Contains("Standard", vm.LicenceOptions!);
        Assert.Equal("Standard", vm.SelectedLicence);
    }

    [Fact]
    public void Commercial_RequiresEndUserCountry_ForAirbusOptical()
    {
        var item = new StacItem("test", "airbus_phr_data", null, null, null, null,
            [new StacLink("self",
                "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/airbus/collections/airbus_phr_data/items/test",
                null, null)]);
        var vm = CreateItemVm(item);

        Assert.True(vm.RequiresEndUserCountry);
        Assert.Equal("GB", vm.EndUserCountry);
    }

    [Fact]
    public void NonCommercial_HasNoLicenceOptions()
    {
        var item = new StacItem("test", "sentinel2_ard", null, null, null, null, null);
        var vm = CreateItemVm(item);

        Assert.False(vm.IsCommercial);
        Assert.Null(vm.LicenceOptions);
    }
}

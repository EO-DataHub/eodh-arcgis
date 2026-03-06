using System.IO;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using eodh.Models;
using eodh.Services;
using eodh.Tests.Helpers;
using eodh.ViewModels;
using Xunit;

namespace eodh.Tests.ViewModels;

/// <summary>
/// Req 2: Search &amp; Filtering — documents expected SearchViewModel behaviour:
/// AOI input, date/cloud cover defaults, catalog/collection loading, and search execution.
/// All tests require ArcGIS Pro SDK runtime.
/// </summary>
[Collection("ArcGIS-SDK")]
[Trait("Category", "RequiresArcGIS")]
public class SearchViewModelTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static SearchViewModel CreateVm(
        FixtureHttpHandler? handler = null,
        Action<List<StacItem>>? onComplete = null)
    {
        handler ??= new FixtureHttpHandler();
        var auth = new TestAuthService(handler);
        var stacClient = new StacClient(auth);
        return new SearchViewModel(stacClient, onComplete ?? (_ => { }));
    }

    [Fact]
    public void CanSearch_False_WhenNoAoiSet()
    {
        var vm = CreateVm();
        Assert.False(vm.SearchCommand.CanExecute(null));
    }

    [Fact]
    public void CanSearch_True_WhenAoiSet()
    {
        var vm = CreateVm();
        var envelope = EnvelopeBuilderEx.CreateEnvelope(
            new Coordinate2D(-1.5, 51.0),
            new Coordinate2D(0.5, 52.0));

        vm.SetAoiFromPolygon(envelope);

        Assert.True(vm.SearchCommand.CanExecute(null));
    }

    [Fact]
    public void StartDate_DefaultsTwoMonthsAgo()
    {
        var vm = CreateVm();
        var expected = DateTime.Today.AddMonths(-2);
        Assert.NotNull(vm.StartDate);
        Assert.Equal(expected.Date, vm.StartDate!.Value.Date);
    }

    [Fact]
    public void EndDate_DefaultsToToday()
    {
        var vm = CreateVm();
        Assert.NotNull(vm.EndDate);
        Assert.Equal(DateTime.Today, vm.EndDate!.Value.Date);
    }

    [Fact]
    public void MaxCloudCover_DefaultsTo100()
    {
        var vm = CreateVm();
        Assert.Equal(100, vm.MaxCloudCover);
    }

    [Fact]
    public async Task ExecuteSearch_IncludesSelectedCollection()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterJson("token=", """{"catalogs":[],"collections":[],"links":[]}""");
        handler.Register("/collections", FixturePath("collections.json"));
        handler.Register("/search", FixturePath("search_results.json"));
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));
        var vm = CreateVm(handler);

        await vm.LoadCatalogsAsync();
        await Task.Delay(200);

        if (vm.Collections.Count > 0)
            vm.SelectedCollection = vm.Collections[0];

        var envelope = EnvelopeBuilderEx.CreateEnvelope(
            new Coordinate2D(-1.5, 51.0),
            new Coordinate2D(0.5, 52.0));
        vm.SetAoiFromPolygon(envelope);

        vm.SearchCommand.Execute(null);
        await Task.Delay(200);

        Assert.NotNull(vm.CurrentFilters);
        Assert.True(vm.CurrentFilters!.Collections.Count > 0);
    }

    [Fact]
    public async Task ExecuteSearch_EmptyCollections_WhenNoneSelected()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterJson("token=", """{"catalogs":[],"collections":[],"links":[]}""");
        handler.Register("/collections", FixturePath("collections.json"));
        handler.Register("/search", FixturePath("search_results.json"));
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));
        var vm = CreateVm(handler);

        await vm.LoadCatalogsAsync();
        await Task.Delay(200);

        vm.SelectedCollection = null;

        var envelope = EnvelopeBuilderEx.CreateEnvelope(
            new Coordinate2D(-1.5, 51.0),
            new Coordinate2D(0.5, 52.0));
        vm.SetAoiFromPolygon(envelope);

        vm.SearchCommand.Execute(null);
        await Task.Delay(200);

        Assert.NotNull(vm.CurrentFilters);
        Assert.Empty(vm.CurrentFilters!.Collections);
    }

    [Fact]
    public async Task ExecuteSearch_CallsStacClientSearchAsync()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterJson("token=", """{"catalogs":[],"collections":[],"links":[]}""");
        handler.Register("/collections", FixturePath("collections.json"));
        handler.Register("/search", FixturePath("search_results.json"));
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));

        List<StacItem>? receivedItems = null;
        var vm = CreateVm(handler, items => receivedItems = items);

        await vm.LoadCatalogsAsync();
        await Task.Delay(200);

        var envelope = EnvelopeBuilderEx.CreateEnvelope(
            new Coordinate2D(-1.5, 51.0),
            new Coordinate2D(0.5, 52.0));
        vm.SetAoiFromPolygon(envelope);

        vm.SearchCommand.Execute(null);
        await Task.Delay(200);

        Assert.NotNull(receivedItems);
        Assert.True(receivedItems!.Count > 0);
    }

    [Fact]
    public async Task ExecuteSearch_InvokesOnSearchCompleted_WithResults()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterJson("token=", """{"catalogs":[],"collections":[],"links":[]}""");
        handler.Register("/collections", FixturePath("collections.json"));
        handler.Register("/search", FixturePath("search_results.json"));
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));

        List<StacItem>? receivedItems = null;
        var vm = CreateVm(handler, items => receivedItems = items);

        await vm.LoadCatalogsAsync();
        await Task.Delay(200);

        var envelope = EnvelopeBuilderEx.CreateEnvelope(
            new Coordinate2D(-1.5, 51.0),
            new Coordinate2D(0.5, 52.0));
        vm.SetAoiFromPolygon(envelope);

        vm.SearchCommand.Execute(null);
        await Task.Delay(200);

        Assert.NotNull(receivedItems);
        // search_results.json fixture has 2 features
        Assert.Equal(2, receivedItems!.Count);
    }

    [Fact]
    public async Task ExecuteSearch_SetsIsSearching_DuringOperation()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterJson("token=", """{"catalogs":[],"collections":[],"links":[]}""");
        handler.Register("/search", FixturePath("search_results.json"));
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));
        var vm = CreateVm(handler);

        await vm.LoadCatalogsAsync();

        var envelope = EnvelopeBuilderEx.CreateEnvelope(
            new Coordinate2D(-1.5, 51.0),
            new Coordinate2D(0.5, 52.0));
        vm.SetAoiFromPolygon(envelope);

        vm.SearchCommand.Execute(null);
        await Task.Delay(200);

        // After completion, IsSearching should be false (finally block ran)
        Assert.False(vm.IsSearching);
        Assert.NotEmpty(vm.ResultSummary);
    }

    [Fact]
    public async Task LoadCatalogsAsync_PopulatesCatalogsList()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterJson("token=", """{"catalogs":[],"collections":[],"links":[]}""");
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));
        var vm = CreateVm(handler);

        await vm.LoadCatalogsAsync();

        Assert.True(vm.Catalogs.Count > 0);
    }

    [Fact]
    public async Task SelectedCatalog_TriggersCollectionLoad()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterJson("token=", """{"catalogs":[],"collections":[],"links":[]}""");
        handler.Register("/collections", FixturePath("collections.json"));
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));
        var vm = CreateVm(handler);

        await vm.LoadCatalogsAsync();
        // LoadCatalogsAsync auto-selects first catalog which triggers LoadCollectionsAsync
        await Task.Delay(200);

        Assert.True(vm.Collections.Count > 0);
    }

    #region AOI: Draw polygon

    [Fact]
    public void DrawAoiCommand_CanExecute()
    {
        var vm = CreateVm();
        Assert.True(vm.DrawAoiCommand.CanExecute(null));
    }

    [Fact]
    public void SetAoiFromPolygon_SetsAoiDescription()
    {
        var vm = CreateVm();
        var envelope = EnvelopeBuilderEx.CreateEnvelope(
            new Coordinate2D(-1.5, 51.0),
            new Coordinate2D(0.5, 52.0));

        vm.SetAoiFromPolygon(envelope);

        Assert.Contains("Drawn AOI", vm.AoiDescription);
        Assert.Contains("-1.50", vm.AoiDescription);
        Assert.Contains("52.00", vm.AoiDescription);
    }

    #endregion

    #region AOI: Use map canvas extent

    [Fact]
    public void UseMapExtentCommand_CanExecute()
    {
        var vm = CreateVm();
        Assert.True(vm.UseMapExtentCommand.CanExecute(null));
    }

    [Fact]
    public void UseMapExtentCommand_SetsAoi_WhenMapActive()
    {
        var vm = CreateVm();
        vm.UseMapExtentCommand.Execute(null);

        // MapView.Active getter may throw NRE when Pro isn't initialised
        bool hasMap;
        try { hasMap = MapView.Active?.Map != null; }
        catch (NullReferenceException) { hasMap = false; }

        if (hasMap)
        {
            Assert.True(vm.SearchCommand.CanExecute(null));
            Assert.Contains("Map extent", vm.AoiDescription);
        }
        else
        {
            // No active map — AOI stays unset
            Assert.False(vm.SearchCommand.CanExecute(null));
        }
    }

    #endregion

    #region AOI: Import GeoJSON

    [Fact]
    public void ImportAoiCommand_CanExecute()
    {
        var vm = CreateVm();
        Assert.True(vm.ImportAoiCommand.CanExecute(null));
    }

    [Fact]
    public void SetAoiFromGeoJson_UsesBboxProperty()
    {
        var vm = CreateVm();
        var geoJson = """
            {
                "type": "Feature",
                "bbox": [-1.5, 51.0, 0.5, 52.0],
                "geometry": {
                    "type": "Polygon",
                    "coordinates": [[[-1.5, 51.0], [0.5, 51.0], [0.5, 52.0], [-1.5, 52.0], [-1.5, 51.0]]]
                }
            }
            """;

        vm.SetAoiFromGeoJson(geoJson);

        Assert.True(vm.SearchCommand.CanExecute(null));
        Assert.Contains("Imported AOI", vm.AoiDescription);
        Assert.Contains("-1.50", vm.AoiDescription);
        Assert.Contains("52.00", vm.AoiDescription);
    }

    [Fact]
    public void SetAoiFromGeoJson_ComputesExtent_FromGeometryCoordinates()
    {
        var vm = CreateVm();
        var geoJson = """
            {
                "type": "Polygon",
                "coordinates": [[[-2.0, 50.0], [1.0, 50.0], [1.0, 53.0], [-2.0, 53.0], [-2.0, 50.0]]]
            }
            """;

        vm.SetAoiFromGeoJson(geoJson);

        Assert.True(vm.SearchCommand.CanExecute(null));
        Assert.Contains("-2.00", vm.AoiDescription);
        Assert.Contains("50.00", vm.AoiDescription);
        Assert.Contains("1.00", vm.AoiDescription);
        Assert.Contains("53.00", vm.AoiDescription);
    }

    [Fact]
    public void SetAoiFromGeoJson_HandlesFeatureCollection()
    {
        var vm = CreateVm();
        var geoJson = """
            {
                "type": "FeatureCollection",
                "features": [{
                    "type": "Feature",
                    "geometry": {
                        "type": "Polygon",
                        "coordinates": [[[-1.0, 51.0], [0.5, 51.0], [0.5, 52.0], [-1.0, 52.0], [-1.0, 51.0]]]
                    }
                }]
            }
            """;

        vm.SetAoiFromGeoJson(geoJson);

        Assert.True(vm.SearchCommand.CanExecute(null));
        Assert.Contains("Imported AOI", vm.AoiDescription);
    }

    #endregion
}

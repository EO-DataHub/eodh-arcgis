using System.Net.Http;
using ArcGIS.Core.Geometry;
using eodh.Models;
using eodh.Services;
using eodh.Tests.Helpers;
using eodh.ViewModels;
using Xunit;

namespace eodh.Tests.ViewModels;

[Collection("ArcGIS-SDK")]
[Trait("Category", "RequiresArcGIS")]
public class SearchViewModelTests
{
    [Fact]
    public async Task LoadCatalogsAsync_ExposesOnlyCuratedRootsAndFlatEntries()
    {
        var (vm, handler) = CreateVm();
        RegisterDiscovery(handler);

        await vm.LoadCatalogsAsync();
        await WaitUntilAsync(() => vm.Collections.Count == 1);

        Assert.Equal(["Public", "Commercial"], vm.Catalogs.Select(root => root.DisplayName));
        Assert.Equal("Provider — Collection title", vm.Collections[0].DisplayName);
    }

    [Fact]
    public void SearchRequiresBothAoiAndCollection()
    {
        var (vm, _) = CreateVm();
        vm.SetAoiFromPolygon(CreateEnvelope());
        Assert.False(vm.SearchCommand.CanExecute(null));

        vm.SelectedCollection = CreateEntry();

        Assert.True(vm.SearchCommand.CanExecute(null));
    }

    [Fact]
    public void IsCommercialCatalog_TracksSelectedCatalog()
    {
        var (vm, _) = CreateVm();

        vm.SelectedCatalog = CatalogRoot.Public;
        Assert.False(vm.IsCommercialCatalog);

        vm.SelectedCatalog = CatalogRoot.Commercial;
        Assert.True(vm.IsCommercialCatalog);
    }

    [Fact]
    public void SelectedCollection_PrefillsPublishedSpatialAndTemporalExtent()
    {
        var (vm, _) = CreateVm();
        vm.SelectedCollection = CreateEntry(new StacExtent(
            new StacSpatialExtent([[-3, 50, 2, 55]]),
            new StacTemporalExtent([["2024-01-02T00:00:00Z", "2025-03-04T23:59:59Z"]])));

        Assert.Equal(new DateTime(2024, 1, 2), vm.StartDate);
        Assert.Equal(new DateTime(2025, 3, 4), vm.EndDate);
        Assert.Contains("Collection extent: -3.00, 50.00 to 2.00, 55.00", vm.AoiDescription);
        Assert.True(vm.SearchCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedCollection_DefaultsMissingDatesToLastMonthThroughToday()
    {
        var (vm, _) = CreateVm();

        vm.SelectedCollection = CreateEntry();

        Assert.Equal(DateTime.Today.AddMonths(-1), vm.StartDate);
        Assert.Equal(DateTime.Today, vm.EndDate);
    }

    [Fact]
    public async Task MaxCloudCover100_SendsNoCloudPredicate()
    {
        var (vm, handler) = CreateVm();
        RegisterDiscovery(handler);
        await PrepareSearchAsync(vm);
        vm.MaxCloudCover = 100;

        vm.SearchCommand.Execute(null);
        await WaitUntilAsync(() => vm.CurrentFilters != null && !vm.IsSearching);

        Assert.Null(vm.CurrentFilters!.MaxCloudCover);
        Assert.DoesNotContain("DEBUG:", vm.ResultSummary);
    }

    [Fact]
    public async Task CloudCoverFilter_ShowsOnlyWhenSamplePublishesProperty()
    {
        var (vm, handler) = CreateVm();
        RegisterDiscovery(handler);

        await vm.LoadCatalogsAsync();
        await WaitUntilAsync(() => vm.HasCloudCoverFilter);

        Assert.True(vm.HasCloudCoverFilter);
    }

    [Fact]
    public async Task MaxCloudCoverBelow100_ProducesCloudPredicate()
    {
        var (vm, handler) = CreateVm();
        RegisterDiscovery(handler);
        await PrepareSearchAsync(vm);
        vm.MaxCloudCover = 38;

        vm.SearchCommand.Execute(null);
        await WaitUntilAsync(() => vm.CurrentFilters != null && !vm.IsSearching);

        Assert.Equal(38, vm.CurrentFilters!.MaxCloudCover);
        var request = handler.Requests.Last(request => request.Method == HttpMethod.Post);
        Assert.Contains("properties.eo:cloud_cover", request.Body);
    }

    [Fact]
    public void SetAoiFromGeoJson_ComputesExtent()
    {
        var (vm, _) = CreateVm();
        vm.SetAoiFromGeoJson("""
            {"type":"Polygon","coordinates":[[[-2,50],[1,50],[1,53],[-2,53],[-2,50]]]}
            """);

        Assert.Contains("Imported AOI", vm.AoiDescription);
        Assert.Contains("-2.00", vm.AoiDescription);
        Assert.True(vm.ClearAoiCommand.CanExecute(null));
    }

    private static async Task PrepareSearchAsync(SearchViewModel vm)
    {
        await vm.LoadCatalogsAsync();
        await WaitUntilAsync(() => vm.SelectedCollection != null);
        await WaitUntilAsync(() => vm.HasCloudCoverFilter);
        vm.SetAoiFromPolygon(CreateEnvelope());
    }

    private static (SearchViewModel vm, FixtureHttpHandler handler) CreateVm()
    {
        var handler = new FixtureHttpHandler();
        var vm = new SearchViewModel(new StacClient(new TestAuthService(handler)), _ => { });
        return (vm, handler);
    }

    private static void RegisterDiscovery(FixtureHttpHandler handler)
    {
        handler.RegisterJson("/provider/collections", """
            {"collections":[{"id":"collection","title":"Collection title","links":[{"rel":"parent","href":"/provider"}]}],"links":[]}
            """);
        handler.RegisterJson("/provider/search", """
            {"type":"FeatureCollection","features":[
              {"id":"sample","properties":{"eo:cloud_cover":20}}
            ],"links":[],"numMatched":1}
            """);
        handler.RegisterJson("/provider", """
            {"id":"provider","title":"Provider","type":"Catalog","links":[
              {"rel":"self","href":"/provider"},{"rel":"search","href":"/provider/search"},
              {"rel":"collections","href":"/provider/collections"}]}
            """);
        handler.RegisterJson("/api/catalogue/stac/catalogs/public", """
            {"id":"public","title":"Public","type":"Catalog","links":[{"rel":"child","href":"/provider"}]}
            """);
    }

    private static CatalogCollectionEntry CreateEntry(StacExtent? extent = null) => new(
        CatalogRoot.Public,
        "Provider",
        "https://eodatahub.org.uk/provider",
        "https://eodatahub.org.uk/provider/search",
        new StacCollection("collection", "Collection title", null, null, null, extent, null));

    private static Envelope CreateEnvelope() => EnvelopeBuilderEx.CreateEnvelope(
        new Coordinate2D(-1.5, 51), new Coordinate2D(0.5, 52));

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var timeout = DateTime.UtcNow.AddSeconds(3);
        while (!predicate() && DateTime.UtcNow < timeout)
            await Task.Delay(20);
        Assert.True(predicate(), "Timed out waiting for asynchronous view-model state.");
    }
}

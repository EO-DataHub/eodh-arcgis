using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Xunit;
using eodh.Models;
using eodh.Services;
using eodh.Tests.Helpers;

namespace eodh.Tests.Integration;

/// <summary>
/// Req 6: Loading Data into ArcGIS — tests LayerService behaviour with
/// ArcGIS Pro running in test mode. Requires Pro SDK and license.
/// </summary>
[Collection("Integration")]
[Trait("Category", "RequiresArcGIS")]
public class LayerServiceTests
{
    private readonly LayerService _layerService = new();

    /// <summary>
    /// MapView.Active throws NRE when Pro isn't initialised (not just null).
    /// </summary>
    private static bool HasActiveMap()
    {
        try { return MapView.Active?.Map != null; }
        catch (NullReferenceException) { return false; }
    }

    [Fact]
    public async Task LoadAssetAsync_ReturnsNull_WhenNoActiveMap()
    {
        // With no project/map open, MapView.Active is null
        var item = new StacItem("test-item", "test-collection", null, null,
            new StacItemProperties("2026-01-01T00:00:00Z", null, null, null, null, null, null, null, null),
            null, null);

        var asset = new StacAsset(
            "https://example.com/test.tif",
            "image/tiff; application=geotiff", "Test", ["data"], null);

        var result = await _layerService.LoadAssetAsync(item, asset);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAssetAsync_LoadsCogLayer()
    {
        var item = new StacItem("test-cog-item", "sentinel2_ard", null, null,
            new StacItemProperties("2026-01-01T00:00:00Z", null, null, null, null, null, null, null, null),
            null, null);

        var asset = new StacAsset(
            "https://dap.ceda.ac.uk/neodc/sentinel_ard/data/sentinel_2/2026/02/16/S2B_20260216_latn518lonw0008_T30UXC_ORB094_20260216132351_utm30n_osgb_vmsk_sharp_rad_srefdem_stdsref.tif",
            "image/tiff; application=geotiff", "COG", ["data"], null);

        var result = await _layerService.LoadAssetAsync(item, asset, "cog");

        // If a map is active, layer should be created; if not, null is acceptable
        // The main assertion is that it doesn't throw
        if (HasActiveMap())
            Assert.NotNull(result);
    }

    [Fact]
    public async Task LoadAssetAsync_LoadsGeoTiffLayer()
    {
        var item = new StacItem("test-geotiff-item", "sentinel2_ard", null, null,
            new StacItemProperties("2026-01-01T00:00:00Z", null, null, null, null, null, null, null, null),
            null, null);

        var asset = new StacAsset(
            "https://dap.ceda.ac.uk/neodc/sentinel_ard/data/sentinel_2/2026/02/16/S2B_20260216_latn518lonw0008_T30UXC_ORB094_20260216132351_utm30n_osgb_clouds.tif",
            "image/tiff; application=geotiff", "Cloud mask", ["data"], null);

        var result = await _layerService.LoadAssetAsync(item, asset);

        if (HasActiveMap())
            Assert.NotNull(result);
    }

    [Fact]
    public async Task LoadServiceLayerAsync_LoadsWmsLayer()
    {
        // Skip if no active map — Pro test mode may not always create one
        if (!HasActiveMap())
            return;

        var result = await _layerService.LoadServiceLayerAsync(
            "https://ows.terrestris.de/osm/service?SERVICE=WMS&VERSION=1.1.1&REQUEST=GetMap",
            "Test WMS");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task LoadServiceLayerAsync_LoadsWmtsLayer()
    {
        if (!HasActiveMap())
            return;

        var result = await _layerService.LoadServiceLayerAsync(
            "https://services.arcgisonline.com/arcgis/rest/services/World_Imagery/MapServer/WMTS",
            "Test WMTS");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task SetMapToOsgbAsync_SetsSpatialReference27700()
    {
        if (!HasActiveMap())
            return;

        await _layerService.SetMapToOsgbAsync();

        var sr = await QueuedTask.Run(() => MapView.Active?.Map?.SpatialReference);
        Assert.NotNull(sr);
        Assert.Equal(27700, sr!.Wkid);
    }

    [Fact]
    public async Task LoadAssetAsync_StreamsCogViaVsicurl()
    {
        var item = new StacItem("test-vsicurl-item", "sentinel2_ard", null, null,
            new StacItemProperties("2026-01-01T00:00:00Z", null, null, null, null, null, null, null, null),
            null, null);

        var asset = new StacAsset(
            "https://dap.ceda.ac.uk/neodc/sentinel_ard/data/sentinel_2/2026/02/16/" +
            "S2B_20260216_latn518lonw0008_T30UXC_ORB094_20260216132351_utm30n_osgb_vmsk_sharp_rad_srefdem_stdsref.tif",
            "image/tiff; application=geotiff; profile=cloud-optimized", "COG", ["data"], null);

        // Clear download cache to ensure we're testing streaming, not cache
        var cachedPath = eodh.Tools.FileDownloader.GetCachePath(asset.Href);
        if (System.IO.File.Exists(cachedPath))
            System.IO.File.Delete(cachedPath);

        var result = await _layerService.LoadAssetAsync(item, asset, "vsicurl-test");

        if (HasActiveMap())
        {
            Assert.NotNull(result);

            // Verify the file was NOT downloaded — /vsicurl/ should stream without temp file
            Assert.False(System.IO.File.Exists(cachedPath),
                "File was downloaded to cache — /vsicurl/ streaming was not used");
        }
    }

    [Fact]
    public async Task DoubleClickResult_LoadsAllLoadableAssets()
    {
        // Tests that all loadable assets can be loaded independently
        var assets = new Dictionary<string, StacAsset>
        {
            ["thumbnail"] = new("https://example.com/thumb.jpg", "image/jpeg", "Thumb", ["thumbnail"], null),
            ["cog"] = new("https://example.com/data.tif", "image/tiff; application=geotiff", "Data", ["data"], null),
            ["cloud"] = new("https://example.com/cloud.tif", "image/tiff; application=geotiff", "Cloud", ["data"], null),
            ["metadata"] = new("https://example.com/meta.xml", "application/xml", "Meta", ["metadata"], null)
        };

        var item = new StacItem("test-all-assets", "sentinel2_ard", null, null,
            new StacItemProperties("2026-01-01T00:00:00Z", null, null, null, null, null, null, null, null),
            assets, null);

        var loadable = eodh.Tools.AssetSelector.GetLoadableAssets(assets);
        Assert.Equal(2, loadable.Count);

        // Each loadable asset should load independently without throwing
        foreach (var (key, asset) in loadable)
        {
            var result = await _layerService.LoadAssetAsync(item, asset, key);

            if (HasActiveMap())
                Assert.NotNull(result);
        }
    }
}

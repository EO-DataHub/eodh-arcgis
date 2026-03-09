using System.Diagnostics;
using System.IO;
using System.Linq;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using eodh.Models;
using eodh.Tools;

namespace eodh.Services;

/// <summary>
/// Loads STAC assets into ArcGIS Pro as map layers.
/// Handles COG, NetCDF, WMS/WMTS, and reprojection to EPSG:27700 (OSGB).
/// </summary>
public class LayerService
{
    /// <summary>OSGB 1936 / British National Grid.</summary>
    private const int OsgbEpsg = 27700;

    static LayerService()
    {
        // Parallel HTTP range requests for COG tile fetching
        Environment.SetEnvironmentVariable("GDAL_HTTP_MULTIPLEX", "YES");
        // Skip directory listings (not needed for direct file access)
        Environment.SetEnvironmentVariable("GDAL_DISABLE_READDIR_ON_OPEN", "EMPTY_DIR");
        // Enable GDAL's in-memory VSI cache for streamed tiles
        Environment.SetEnvironmentVariable("VSI_CACHE", "TRUE");
        Environment.SetEnvironmentVariable("VSI_CACHE_SIZE", "67108864"); // 64 MB
        // Skip unnecessary HEAD requests
        Environment.SetEnvironmentVariable("CPL_VSIL_CURL_USE_HEAD", "NO");
        // Allow .tif and .tiff for streaming
        Environment.SetEnvironmentVariable("CPL_VSIL_CURL_ALLOWED_EXTENSIONS", ".tif,.tiff,.nc");
        // Set GDAL block cache to 256 MB
        Environment.SetEnvironmentVariable("GDAL_CACHEMAX", "256");
    }

    /// <summary>
    /// MapView.Active throws NRE when Pro isn't fully initialised (not just null).
    /// </summary>
    private static bool HasActiveMap()
    {
        try { return MapView.Active?.Map != null; }
        catch (NullReferenceException) { return false; }
    }

    /// <summary>
    /// Load a STAC asset into the active map as a raster layer.
    /// </summary>
    public async Task<Layer?> LoadAssetAsync(StacItem item, StacAsset asset, string? assetKey = null)
    {
        var shortId = item.Id.Length > 60 ? item.Id[..60] : item.Id;
        var layerName = $"{item.Collection ?? "item"} - {shortId}";
        if (assetKey != null)
            layerName += $" ({assetKey})";
        if (layerName.Length > 128)
            layerName = layerName[..128];

        var href = asset.Href;
        var fileType = asset.FileType;

        if (fileType is not ("COG" or "GeoTIFF" or "NetCDF"))
            return null;

        // No active map — nowhere to add a layer
        if (!HasActiveMap())
            return null;

        // NetCDF uses a dedicated loading path with MakeMultidimensionalRasterLayer
        if (fileType == "NetCDF")
            return await LoadNetCdfAssetAsync(asset, assetKey, layerName);

        var sw = Stopwatch.StartNew();
        Log($"LOAD START: {assetKey ?? "?"} | {fileType} | {href}");

        // Tier 0: Load from download cache if already downloaded
        var cachedPath = FileDownloader.GetCachedPath(href);
        if (cachedPath != null)
        {
            var result = await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                if (map == null) return null;
                return LoadCogLayer(map, cachedPath, layerName);
            });
            Log($"TIER 0 CACHE HIT: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms | {cachedPath}");
            return result;
        }

        // Tier 1: Try /vsicurl/ streaming via MakeRasterLayer GP tool
        if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var vsicurlPath = "/vsicurl/" + href;
                var gpArgs = Geoprocessing.MakeValueArray(vsicurlPath, layerName);
                var gpResult = await Geoprocessing.ExecuteToolAsync(
                    "management.MakeRasterLayer", gpArgs);
                if (!gpResult.IsFailed)
                {
                    var layer = await QueuedTask.Run(() =>
                        MapView.Active?.Map?.GetLayersAsFlattenedList()
                            .OfType<RasterLayer>()
                            .FirstOrDefault(l => l.Name == layerName));
                    Log($"TIER 1 VSICURL OK: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms");
                    return layer;
                }
                Log($"TIER 1 VSICURL FAILED (IsFailed): {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Log($"TIER 1 VSICURL ERROR: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms | {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Tier 2: Try raw URL via LayerFactory
        try
        {
            var result = await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                if (map == null) return null;
                return LoadCogLayer(map, href, layerName);
            });
            Log($"TIER 2 RAW URL OK: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms");
            return result;
        }
        catch (Exception ex)
        {
            Log($"TIER 2 RAW URL ERROR: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms | {ex.GetType().Name}: {ex.Message}");
        }

        // Tier 3: Download to temp (with caching) and load locally
        Log($"TIER 3 DOWNLOAD START: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms");
        var tempPath = await FileDownloader.DownloadToTempAsync(href);
        if (tempPath == null) throw new InvalidOperationException(
            $"Failed to download asset from {href}");

        Log($"TIER 3 DOWNLOAD DONE: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms | {tempPath}");
        var finalResult = await QueuedTask.Run(() =>
        {
            var map = MapView.Active?.Map;
            if (map == null) return null;
            return LoadCogLayer(map, tempPath, layerName);
        });
        Log($"TIER 3 LOCAL LOAD OK: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms");
        return finalResult;
    }

    /// <summary>
    /// Load a WMS/WMTS service layer into the active map.
    /// </summary>
    public async Task<Layer?> LoadServiceLayerAsync(string serviceUrl, string layerName)
    {
        if (!HasActiveMap())
            return null;

        return await QueuedTask.Run(() =>
        {
            var map = MapView.Active?.Map;
            if (map == null) return null;

            var uri = new Uri(serviceUrl);
            return LayerFactory.Instance.CreateLayer(uri, map, layerName: layerName);
        });
    }

    /// <summary>
    /// Set the active map's spatial reference to OSGB (EPSG:27700).
    /// </summary>
    public async Task SetMapToOsgbAsync()
    {
        if (!HasActiveMap()) return;

        await QueuedTask.Run(() =>
        {
            var map = MapView.Active?.Map;
            if (map == null) return;

            var osgb = SpatialReferenceBuilder.CreateSpatialReference(OsgbEpsg);
            map.SetSpatialReference(osgb);
        });
    }

    #region Private Methods

    /// <summary>
    /// Load a NetCDF asset using MakeMultidimensionalRasterLayer GP tool.
    /// Tries vsicurl streaming first, then falls back to download.
    /// </summary>
    private async Task<Layer?> LoadNetCdfAssetAsync(StacAsset asset, string? assetKey, string layerName)
    {
        var href = asset.Href;
        var sw = Stopwatch.StartNew();
        Log($"NETCDF LOAD START: {assetKey ?? "?"} | {href}");

        // Tier 0: Load from download cache
        var cachedPath = FileDownloader.GetCachedPath(href);
        if (cachedPath != null)
        {
            Log($"NETCDF CACHE HIT: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms | {cachedPath}");
            return await LoadMultidimensionalLayer(cachedPath, layerName, assetKey, sw);
        }

        // Tier 1: Try /vsicurl/ streaming via MakeMultidimensionalRasterLayer
        if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var vsicurlPath = "/vsicurl/" + href;
                var gpArgs = Geoprocessing.MakeValueArray(vsicurlPath, layerName);
                var gpResult = await Geoprocessing.ExecuteToolAsync(
                    "md.MakeMultidimensionalRasterLayer", gpArgs);
                if (!gpResult.IsFailed)
                {
                    var layer = await QueuedTask.Run(() =>
                        MapView.Active?.Map?.GetLayersAsFlattenedList()
                            .FirstOrDefault(l => l.Name == layerName));
                    Log($"NETCDF TIER 1 VSICURL OK: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms");
                    return layer;
                }
                Log($"NETCDF TIER 1 VSICURL FAILED: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Log($"NETCDF TIER 1 VSICURL ERROR: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms | {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Tier 2: Download to temp, then load locally
        Log($"NETCDF DOWNLOAD START: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms");
        var localPath = await FileDownloader.DownloadToTempAsync(href);
        if (localPath == null)
            throw new InvalidOperationException($"Failed to download NetCDF asset from {href}");

        Log($"NETCDF DOWNLOAD DONE: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms | {localPath}");
        return await LoadMultidimensionalLayer(localPath, layerName, assetKey, sw);
    }

    /// <summary>
    /// Load a local file as a multidimensional raster layer, falling back to LayerFactory.
    /// </summary>
    private async Task<Layer?> LoadMultidimensionalLayer(
        string localPath, string layerName, string? assetKey, Stopwatch sw)
    {
        // Try MakeMultidimensionalRasterLayer GP tool
        try
        {
            var gpArgs = Geoprocessing.MakeValueArray(localPath, layerName);
            var gpResult = await Geoprocessing.ExecuteToolAsync(
                "md.MakeMultidimensionalRasterLayer", gpArgs);
            if (!gpResult.IsFailed)
            {
                var layer = await QueuedTask.Run(() =>
                    MapView.Active?.Map?.GetLayersAsFlattenedList()
                        .FirstOrDefault(l => l.Name == layerName));
                Log($"NETCDF MULTIDIM OK: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms");
                return layer;
            }
            Log($"NETCDF MULTIDIM FAILED: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Log($"NETCDF MULTIDIM ERROR: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms | {ex.GetType().Name}: {ex.Message}");
        }

        // Fallback: LayerFactory (ArcGIS Pro 3.x can open .nc directly)
        var result = await QueuedTask.Run(() =>
        {
            var map = MapView.Active?.Map;
            if (map == null) return null;
            return LoadCogLayer(map, localPath, layerName);
        });
        Log($"NETCDF LAYERFACTORY FALLBACK OK: {assetKey ?? "?"} | {sw.ElapsedMilliseconds}ms");
        return result;
    }

    private static Layer? LoadCogLayer(Map map, string href, string layerName)
    {
        var uri = new Uri(href);
        return LayerFactory.Instance.CreateLayer(uri, map, layerName: layerName);
    }

    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "eodh_layer_load.log");

    private static void Log(string message)
    {
        try
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
            File.AppendAllText(LogPath, line);
        }
        catch { /* logging is best-effort */ }
    }

    #endregion
}

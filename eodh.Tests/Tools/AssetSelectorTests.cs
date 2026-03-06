using eodh.Models;
using eodh.Tools;
using Xunit;

namespace eodh.Tests.Tools;

/// <summary>
/// Req 6: Loading Data — tests for asset selection logic that determines
/// which STAC assets are loadable (COG, GeoTIFF, NetCDF).
/// </summary>
public class AssetSelectorTests
{
    [Fact]
    public void GetLoadableAssets_ReturnsAllLoadable()
    {
        var assets = new Dictionary<string, StacAsset>
        {
            ["cog"] = new("https://example.com/cog.tif",
                "image/tiff; application=geotiff; profile=cloud-optimized", "COG", ["data"], null),
            ["cloud"] = new("https://example.com/cloud.tif",
                "image/tiff; application=geotiff", "Cloud mask", ["data"], null),
            ["thumbnail"] = new("https://example.com/thumb.jpg",
                "image/jpeg", "Thumb", ["thumbnail"], null),
            ["metadata"] = new("https://example.com/meta.xml",
                "application/xml", "Meta", ["metadata"], null)
        };

        var result = AssetSelector.GetLoadableAssets(assets);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetLoadableAssets_ExcludesThumbnails()
    {
        var assets = new Dictionary<string, StacAsset>
        {
            ["thumbnail"] = new("https://example.com/thumb.jpg",
                "image/jpeg", "Thumb", ["thumbnail"], null)
        };

        var result = AssetSelector.GetLoadableAssets(assets);

        Assert.Empty(result);
    }

    [Fact]
    public void GetLoadableAssets_ExcludesMetadata()
    {
        var assets = new Dictionary<string, StacAsset>
        {
            ["metadata"] = new("https://example.com/meta.xml",
                "application/xml", "Meta", ["metadata"], null)
        };

        var result = AssetSelector.GetLoadableAssets(assets);

        Assert.Empty(result);
    }

    [Fact]
    public void GetLoadableAssets_IncludesCogAndGeoTiff()
    {
        var assets = new Dictionary<string, StacAsset>
        {
            ["cog"] = new("https://example.com/cog.tif",
                "image/tiff; application=geotiff; profile=cloud-optimized", "COG", ["data"], null),
            ["geotiff"] = new("https://example.com/data.tif",
                "image/tiff; application=geotiff", "GeoTIFF", ["data"], null)
        };

        var result = AssetSelector.GetLoadableAssets(assets);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Key == "cog");
        Assert.Contains(result, r => r.Key == "geotiff");
    }

    [Fact]
    public void GetLoadableAssets_IncludesNetCdf()
    {
        var assets = new Dictionary<string, StacAsset>
        {
            ["netcdf"] = new("https://example.com/data.nc",
                "application/x-netcdf", "NetCDF", ["data"], null)
        };

        var result = AssetSelector.GetLoadableAssets(assets);

        Assert.Single(result);
        Assert.Equal("netcdf", result[0].Key);
    }

    [Fact]
    public void GetLoadableAssets_ReturnsEmpty_WhenNull()
    {
        var result = AssetSelector.GetLoadableAssets(null);

        Assert.Empty(result);
    }

    [Fact]
    public void GetLoadableAssets_ReturnsEmpty_WhenEmpty()
    {
        var result = AssetSelector.GetLoadableAssets(new Dictionary<string, StacAsset>());

        Assert.Empty(result);
    }
}

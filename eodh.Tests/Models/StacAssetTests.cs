using Xunit;
using eodh.Models;

namespace eodh.Tests.Models;

/// <summary>
/// Req 3: Results Display — asset type detection for display metadata.
/// Req 6: Loading Data — determines which assets are loadable (COG/GeoTIFF/NetCDF).
/// </summary>
public class StacAssetTests
{
    [Theory]
    [InlineData("image/tiff; application=geotiff; profile=cloud-optimized", "COG")]
    [InlineData("image/tiff; application=geotiff", "GeoTIFF")]
    [InlineData("image/tiff", "GeoTIFF")]
    [InlineData("application/x-netcdf", "NetCDF")]
    [InlineData("application/netcdf", "NetCDF")]
    [InlineData("image/png", "PNG")]
    [InlineData("image/jpeg", "JPEG")]
    public void FileType_ReturnsCorrectType_ForMediaType(string mediaType, string expected)
    {
        var asset = new StacAsset("https://example.com/data", mediaType, null, null, null);
        Assert.Equal(expected, asset.FileType);
    }

    [Theory]
    [InlineData("https://example.com/data.tif", "GeoTIFF")]
    [InlineData("https://example.com/data.tiff", "GeoTIFF")]
    [InlineData("https://example.com/data.nc", "NetCDF")]
    [InlineData("https://example.com/data.xyz", "XYZ")]
    public void FileType_FallsBackToExtension_WhenMediaTypeUnknown(string href, string expected)
    {
        // application/octet-stream is a generic binary type — fall back to extension
        var asset = new StacAsset(href, null, null, null, null);
        Assert.Equal(expected, asset.FileType);
    }

    [Theory]
    [InlineData("image/tiff; application=geotiff; profile=cloud-optimized", true)]
    [InlineData("image/tiff; application=geotiff", true)]
    [InlineData("application/x-netcdf", true)]
    [InlineData("image/png", false)]
    [InlineData("image/jpeg", false)]
    public void IsLoadable_CorrectForMediaType(string mediaType, bool expected)
    {
        var asset = new StacAsset("https://example.com/data", mediaType, null, null, null);
        Assert.Equal(expected, asset.IsLoadable);
    }

    [Fact]
    public void IsThumbnail_TrueWhenRolesContainThumbnail()
    {
        var asset = new StacAsset("https://example.com/thumb.png", "image/png", null, ["thumbnail"], null);
        Assert.True(asset.IsThumbnail);
    }

    [Fact]
    public void IsThumbnail_FalseWhenNoThumbnailRole()
    {
        var asset = new StacAsset("https://example.com/data.tif", "image/tiff", null, ["data"], null);
        Assert.False(asset.IsThumbnail);
    }

    [Fact]
    public void IsThumbnail_FalseWhenRolesNull()
    {
        var asset = new StacAsset("https://example.com/data.tif", "image/tiff", null, null, null);
        Assert.False(asset.IsThumbnail);
    }

    [Fact]
    public void IsData_TrueForDataRole()
    {
        var asset = new StacAsset("https://example.com/data.tif", "image/tiff", null, ["data"], null);
        Assert.True(asset.IsData);
    }

    [Fact]
    public void IsData_TrueForVisualRole()
    {
        var asset = new StacAsset("https://example.com/visual.tif", "image/tiff", null, ["visual"], null);
        Assert.True(asset.IsData);
    }

    [Fact]
    public void IsData_FalseForThumbnailOnlyRole()
    {
        var asset = new StacAsset("https://example.com/thumb.png", "image/png", null, ["thumbnail"], null);
        Assert.False(asset.IsData);
    }
}

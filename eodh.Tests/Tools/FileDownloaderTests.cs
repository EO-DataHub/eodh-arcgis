using System.IO;
using eodh.Tools;
using Xunit;

namespace eodh.Tests.Tools;

/// <summary>
/// Req 6: Loading Data — tests for temp file download with caching when
/// ArcGIS Pro cannot stream a remote GeoTIFF directly.
/// </summary>
public class FileDownloaderTests
{
    [Fact]
    public async Task DownloadToTempAsync_DownloadsSmallFile()
    {
        // Real CEDA cloud mask — a small COG that ArcGIS fails to load remotely
        var url = "https://dap.ceda.ac.uk/neodc/sentinel_ard/data/sentinel_2/2026/02/16/" +
                  "S2B_20260216_latn518lonw0008_T30UXC_ORB094_20260216132351_utm30n_osgb_clouds.tif";

        var tempPath = await FileDownloader.DownloadToTempAsync(url);

        Assert.NotNull(tempPath);
        Assert.True(File.Exists(tempPath));

        var info = new FileInfo(tempPath!);
        Assert.True(info.Length > 0);
    }

    [Fact]
    public async Task DownloadToTempAsync_ReturnsNull_ForInvalidUrl()
    {
        var tempPath = await FileDownloader.DownloadToTempAsync(
            "https://example.invalid/does_not_exist.tif");

        Assert.Null(tempPath);
    }

    [Fact]
    public async Task DownloadToTempAsync_UsesEodhSubdirectory()
    {
        var url = "https://dap.ceda.ac.uk/neodc/sentinel_ard/data/sentinel_2/2026/02/16/" +
                  "S2B_20260216_latn518lonw0008_T30UXC_ORB094_20260216132351_utm30n_osgb_clouds.tif";

        var tempPath = await FileDownloader.DownloadToTempAsync(url);

        Assert.NotNull(tempPath);
        Assert.Contains("eodh", tempPath!);
    }

    [Fact]
    public async Task DownloadToTempAsync_ReturnsCachedFile_WhenAlreadyExists()
    {
        var url = "https://example.com/test-cache-hit.tif";
        var cachePath = FileDownloader.GetCachePath(url);

        // Pre-create the cached file
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await File.WriteAllBytesAsync(cachePath, new byte[] { 1, 2, 3 });

        try
        {
            // Should return cached path without downloading (example.com would fail)
            var result = await FileDownloader.DownloadToTempAsync(url);

            Assert.NotNull(result);
            Assert.Equal(cachePath, result);
        }
        finally
        {
            File.Delete(cachePath);
        }
    }

    [Fact]
    public void GetCachePath_ReturnsDeterministicPath()
    {
        var url = "https://example.com/data.tif";

        var path1 = FileDownloader.GetCachePath(url);
        var path2 = FileDownloader.GetCachePath(url);

        Assert.Equal(path1, path2);
    }

    [Fact]
    public void GetCachePath_DifferentUrlsDifferentPaths()
    {
        var path1 = FileDownloader.GetCachePath("https://example.com/a.tif");
        var path2 = FileDownloader.GetCachePath("https://example.com/b.tif");

        Assert.NotEqual(path1, path2);
    }

    [Fact]
    public void GetCachePath_PreservesExtension()
    {
        var path = FileDownloader.GetCachePath("https://example.com/data.tif");

        Assert.EndsWith(".tif", path);
    }

    [Fact]
    public void GetCachePath_DefaultsToTifWhenNoExtension()
    {
        var path = FileDownloader.GetCachePath("https://example.com/data");

        Assert.EndsWith(".tif", path);
    }

    [Fact]
    public void GetCachePath_PreservesNcExtension()
    {
        var path = FileDownloader.GetCachePath("https://example.com/data.nc");

        Assert.EndsWith(".nc", path);
    }
}

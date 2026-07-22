using System.IO;
using System.Net;
using System.Net.Http;
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
    public async Task DownloadToTempAsync_ReportsTransferredAndTotalBytes()
    {
        var payload = new byte[300_000];
        Random.Shared.NextBytes(payload);
        var url = $"https://example.test/{Guid.NewGuid():N}.tif";
        using var client = new HttpClient(new StubHttpHandler(payload));
        var progress = new CollectingProgress();
        string? path = null;

        try
        {
            path = await FileDownloader.DownloadToTempAsync(
                url,
                progress: progress,
                httpClient: client);

            Assert.NotNull(path);
            Assert.Equal(payload, await File.ReadAllBytesAsync(path!));
            var final = Assert.Single(progress.Updates, update =>
                update.BytesReceived == payload.Length);
            Assert.Equal(payload.Length, final.TotalBytes);
        }
        finally
        {
            if (path != null)
                File.Delete(path);
        }
    }

    [Fact]
    public async Task DownloadToTempAsync_DownloadsSmallFile()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var url = $"https://example.test/{Guid.NewGuid():N}.tif";
        using var client = new HttpClient(new StubHttpHandler(payload));
        string? tempPath = null;

        try
        {
            tempPath = await FileDownloader.DownloadToTempAsync(
                url,
                httpClient: client);

            Assert.NotNull(tempPath);
            Assert.True(File.Exists(tempPath));
            Assert.Equal(payload, await File.ReadAllBytesAsync(tempPath!));
        }
        finally
        {
            if (tempPath != null)
                File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task DownloadToTempAsync_ReturnsNull_ForInvalidUrl()
    {
        using var client = new HttpClient(new ThrowingHttpHandler());
        var tempPath = await FileDownloader.DownloadToTempAsync(
            "https://example.test/does_not_exist.tif",
            httpClient: client);

        Assert.Null(tempPath);
    }

    [Fact]
    public async Task DownloadToTempAsync_UsesEodhSubdirectory()
    {
        var url = $"https://example.test/{Guid.NewGuid():N}.tif";
        using var client = new HttpClient(new StubHttpHandler([1]));
        string? tempPath = null;

        try
        {
            tempPath = await FileDownloader.DownloadToTempAsync(
                url,
                httpClient: client);

            Assert.NotNull(tempPath);
            Assert.Contains("eodh", tempPath!);
        }
        finally
        {
            if (tempPath != null)
                File.Delete(tempPath);
        }
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

    private sealed class CollectingProgress : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> Updates { get; } = [];
        public void Report(DownloadProgress value) => Updates.Add(value);
    }

    private sealed class StubHttpHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent(payload);
            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content,
                RequestMessage = request
            });
        }
    }

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated network failure.");
    }
}

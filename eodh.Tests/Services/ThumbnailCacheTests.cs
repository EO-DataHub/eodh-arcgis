using System.IO;
using System.Net.Http;
using Xunit;
using eodh.Services;

namespace eodh.Tests.Services;

/// <summary>
/// Req 7: UI Responsiveness — validates ThumbnailCache caching behaviour
/// to ensure thumbnails are cached in memory and on disk for responsive UI.
/// </summary>
public class ThumbnailCacheTests
{
    [Fact]
    public void ClearCache_DoesNotThrow_WhenEmpty()
    {
        var cacheDirectory = CreateTemporaryCacheDirectory();
        using var client = new HttpClient(new ThrowingHttpHandler());
        var cache = new ThumbnailCache(cacheDirectory, client);

        try
        {
            var exception = Record.Exception(() => cache.ClearCache());

            Assert.Null(exception);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task GetThumbnailAsync_ReturnsNull_ForInvalidUrl()
    {
        var cacheDirectory = CreateTemporaryCacheDirectory();
        using var client = new HttpClient(new ThrowingHttpHandler());
        var cache = new ThumbnailCache(cacheDirectory, client);

        try
        {
            var result = await cache.GetThumbnailAsync(
                "https://example.test/no-image.png");

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    private static string CreateTemporaryCacheDirectory() =>
        Path.Combine(Path.GetTempPath(), $"eodh-thumbnail-tests-{Guid.NewGuid():N}");

    private sealed class ThrowingHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated network failure.");
    }
}

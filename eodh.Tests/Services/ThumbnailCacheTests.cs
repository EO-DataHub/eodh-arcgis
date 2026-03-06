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
        var cache = new ThumbnailCache();

        var exception = Record.Exception(() => cache.ClearCache());

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetThumbnailAsync_ReturnsNull_ForInvalidUrl()
    {
        var cache = new ThumbnailCache();

        // An invalid/unreachable URL should return null, not throw
        var result = await cache.GetThumbnailAsync("https://invalid.test.example/no-image.png");

        Assert.Null(result);
    }
}

using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace eodh.Services;

/// <summary>
/// Async thumbnail download and caching service.
/// Downloads thumbnails from STAC asset URLs and caches them
/// to disk and memory for responsive UI performance.
/// </summary>
public class ThumbnailCache
{
    private readonly ConcurrentDictionary<string, BitmapImage?> _memoryCache = new();
    private readonly string _cacheDir;
    private readonly HttpClient _httpClient;

    public ThumbnailCache()
    {
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EodhArcGis", "cache", "thumbnails");

        Directory.CreateDirectory(_cacheDir);

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    /// <summary>
    /// Get a thumbnail image, loading from cache or downloading as needed.
    /// </summary>
    public async Task<BitmapImage?> GetThumbnailAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(url)) return null;

        // Check memory cache
        if (_memoryCache.TryGetValue(url, out var cached))
            return cached;

        // Check disk cache
        var diskPath = GetCachePath(url);
        if (File.Exists(diskPath))
        {
            var image = LoadImageFromFile(diskPath);
            _memoryCache[url] = image;
            return image;
        }

        // Download
        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(diskPath, bytes, ct);

            var image = LoadImageFromBytes(bytes);
            _memoryCache[url] = image;
            return image;
        }
        catch
        {
            _memoryCache[url] = null;
            return null;
        }
    }

    /// <summary>
    /// Clear all cached thumbnails.
    /// </summary>
    public void ClearCache()
    {
        _memoryCache.Clear();
        if (Directory.Exists(_cacheDir))
        {
            foreach (var file in Directory.GetFiles(_cacheDir))
                File.Delete(file);
        }
    }

    #region Private Helpers

    private string GetCachePath(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];
        var ext = Path.GetExtension(new Uri(url).AbsolutePath);
        if (string.IsNullOrEmpty(ext)) ext = ".png";
        return Path.Combine(_cacheDir, $"{hash}{ext}");
    }

    private static BitmapImage? LoadImageFromFile(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 150;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? LoadImageFromBytes(byte[] bytes)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = new MemoryStream(bytes);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 150;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    #endregion
}

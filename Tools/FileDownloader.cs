using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace eodh.Tools;

/// <summary>
/// Downloads remote files to a local temp directory with disk caching.
/// Used as fallback when ArcGIS Pro cannot stream a remote GeoTIFF directly.
/// Files are cached using URL-hashed filenames so repeated loads are instant.
/// </summary>
internal static class FileDownloader
{
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    {
        Timeout = TimeSpan.FromMinutes(5)
    };
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "eodh");

    public static async Task<string?> DownloadToTempAsync(string url)
    {
        try
        {
            var tempPath = GetCachePath(url);
            Directory.CreateDirectory(TempDir);

            // Return cached file if it exists and has content
            if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 0)
                return tempPath;

            using var response = await Client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            // Reject HTML responses (login pages, soft-404 error pages after redirect)
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                return null;

            await using var fs = File.Create(tempPath);
            await response.Content.CopyToAsync(fs);

            return tempPath;
        }
        catch
        {
            return null;
        }
    }

    public static string? GetCachedPath(string url)
    {
        var path = GetCachePath(url);
        return File.Exists(path) && new FileInfo(path).Length > 0 ? path : null;
    }

    internal static string GetCachePath(string url)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)))[..16];
        var ext = Path.GetExtension(new Uri(url).LocalPath);
        if (string.IsNullOrEmpty(ext)) ext = ".tif";
        return Path.Combine(TempDir, $"{hash}{ext}");
    }
}

using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace eodh.Tools;

internal sealed record DownloadProgress(long BytesReceived, long? TotalBytes);

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
        // Large EO assets commonly take longer than five minutes. Cancellation
        // remains available through the per-request token.
        Timeout = TimeSpan.FromMinutes(30)
    };
    private static readonly string TempDir = Path.Combine(Path.GetTempPath(), "eodh");

    public static async Task<string?> DownloadToTempAsync(
        string url,
        string? bearerToken = null,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default,
        HttpClient? httpClient = null)
    {
        string? partialPath = null;
        try
        {
            var tempPath = GetCachePath(url);
            partialPath = tempPath + ".part";
            Directory.CreateDirectory(TempDir);

            // Return cached file if it exists and has content
            if (File.Exists(tempPath) && new FileInfo(tempPath).Length > 0)
                return tempPath;

            TryDelete(partialPath);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(bearerToken))
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

            using var response = await (httpClient ?? Client).SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            // Reject HTML responses (login pages, soft-404 error pages after redirect)
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != null && contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                return null;

            var totalBytes = response.Content.Headers.ContentLength;
            progress?.Report(new DownloadProgress(0, totalBytes));

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var destination = new FileStream(
                partialPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[128 * 1024];
                long bytesReceived = 0;
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await destination.WriteAsync(
                        buffer.AsMemory(0, bytesRead),
                        cancellationToken);
                    bytesReceived += bytesRead;
                    progress?.Report(new DownloadProgress(bytesReceived, totalBytes));
                }

                await destination.FlushAsync(cancellationToken);
            }

            File.Move(partialPath, tempPath, true);

            return tempPath;
        }
        catch
        {
            if (partialPath != null)
                TryDelete(partialPath);
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

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { /* Cleanup is best-effort. */ }
    }
}

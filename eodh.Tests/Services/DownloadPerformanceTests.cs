using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using eodh.Tools;
using Xunit;
using Xunit.Abstractions;

namespace eodh.Tests.Services;

/// <summary>
/// Performance benchmark: parallel vs sequential asset downloads using a
/// deterministic delayed transport rather than an external service.
/// </summary>
public class DownloadPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public DownloadPerformanceTests(ITestOutputHelper output) => _output = output;

    private static readonly string[] TestUrls =
    [
        "https://performance.test/first.tif",
        "https://performance.test/second.tif",
        "https://performance.test/third.tif",
    ];

    private static void ClearCache()
    {
        foreach (var url in TestUrls)
        {
            var path = FileDownloader.GetCachePath(url);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ParallelDownload_FasterThanSequential()
    {
        using var client = new HttpClient(new DelayedStubHttpHandler(
            TimeSpan.FromMilliseconds(150),
            [1, 2, 3]));

        try
        {
            ClearCache();
            var swSeq = Stopwatch.StartNew();
            foreach (var url in TestUrls)
            {
                var path = await FileDownloader.DownloadToTempAsync(
                    url,
                    httpClient: client);
                Assert.NotNull(path);
            }
            swSeq.Stop();

            ClearCache();
            var swPar = Stopwatch.StartNew();
            var results = await Task.WhenAll(TestUrls.Select(url =>
                FileDownloader.DownloadToTempAsync(url, httpClient: client)));
            swPar.Stop();

            foreach (var path in results)
                Assert.NotNull(path);

            _output.WriteLine($"Sequential: {swSeq.ElapsedMilliseconds}ms");
            _output.WriteLine($"Parallel:   {swPar.ElapsedMilliseconds}ms");
            _output.WriteLine(
                $"Speedup:    {(double)swSeq.ElapsedMilliseconds / swPar.ElapsedMilliseconds:F1}x");

            Assert.True(swPar.ElapsedMilliseconds < swSeq.ElapsedMilliseconds,
                $"Parallel ({swPar.ElapsedMilliseconds}ms) should be faster than sequential ({swSeq.ElapsedMilliseconds}ms)");
        }
        finally
        {
            ClearCache();
        }
    }

    private sealed class DelayedStubHttpHandler(TimeSpan delay, byte[] payload)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            var content = new ByteArrayContent(payload);
            content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content,
                RequestMessage = request
            };
        }
    }
}

using System.Diagnostics;
using System.IO;
using eodh.Tools;
using Xunit;
using Xunit.Abstractions;

namespace eodh.Tests.Services;

/// <summary>
/// Performance benchmark: parallel vs sequential asset downloads.
/// Uses real EODH public STAC assets (small GeoTIFFs, no auth needed).
/// Skips automatically if network is unavailable or blocked by proxy.
/// </summary>
[Trait("Category", "Network")]
public class DownloadPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public DownloadPerformanceTests(ITestOutputHelper output) => _output = output;

    // Small GeoTIFFs from a real Sentinel-2 ARD item (all under 2MB)
    private static readonly string[] TestUrls =
    [
        "https://dap.ceda.ac.uk/neodc/sentinel_ard/data/sentinel_2/2026/02/16/S2B_20260216_latn518lonw0008_T30UXC_ORB094_20260216132351_utm30n_osgb_clouds.tif",
        "https://dap.ceda.ac.uk/neodc/sentinel_ard/data/sentinel_2/2026/02/16/S2B_20260216_latn518lonw0008_T30UXC_ORB094_20260216132351_utm30n_osgb_sat.tif",
        "https://dap.ceda.ac.uk/neodc/sentinel_ard/data/sentinel_2/2026/02/16/S2B_20260216_latn518lonw0008_T30UXC_ORB094_20260216132351_utm30n_osgb_valid.tif",
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
        // Probe: skip if the network/proxy blocks downloads
        ClearCache();
        var probe = await FileDownloader.DownloadToTempAsync(TestUrls[0]);
        if (probe == null)
        {
            _output.WriteLine("SKIP: Cannot reach CEDA — network blocked or proxy returned HTML.");
            return;
        }

        // --- Sequential ---
        ClearCache();
        var swSeq = Stopwatch.StartNew();
        foreach (var url in TestUrls)
        {
            var path = await FileDownloader.DownloadToTempAsync(url);
            Assert.NotNull(path);
        }
        swSeq.Stop();

        // --- Parallel ---
        ClearCache();
        var swPar = Stopwatch.StartNew();
        var results = await Task.WhenAll(TestUrls.Select(FileDownloader.DownloadToTempAsync));
        swPar.Stop();

        foreach (var path in results)
            Assert.NotNull(path);

        _output.WriteLine($"Sequential: {swSeq.ElapsedMilliseconds}ms");
        _output.WriteLine($"Parallel:   {swPar.ElapsedMilliseconds}ms");
        _output.WriteLine($"Speedup:    {(double)swSeq.ElapsedMilliseconds / swPar.ElapsedMilliseconds:F1}x");

        Assert.True(swPar.ElapsedMilliseconds < swSeq.ElapsedMilliseconds,
            $"Parallel ({swPar.ElapsedMilliseconds}ms) should be faster than sequential ({swSeq.ElapsedMilliseconds}ms)");
    }
}

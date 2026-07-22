using eodh.ViewModels;
using Xunit;

namespace eodh.Tests.ViewModels;

public class AssetLoadStatusViewModelTests
{
    [Fact]
    public void Begin_ShowsCurrentAssetAndIndeterminateProgress()
    {
        var status = new AssetLoadStatusViewModel();

        status.Begin("a-long-item-id", "cog", "COG", null, 2, 4);

        Assert.True(status.IsActive);
        Assert.True(status.IsBusy);
        Assert.True(status.HasNonReadyStatus);
        Assert.True(status.IsIndeterminate);
        Assert.Equal("Asset 2 of 4: cog (COG)", status.StatusText);
        Assert.Contains("a-long-item-id", status.DetailText);
    }

    [Fact]
    public void ReportDownload_WithKnownLength_ShowsBytesAndPercentage()
    {
        var status = new AssetLoadStatusViewModel();
        var operationId = status.Begin("item", "data", "GeoTIFF", null, 1, 1);

        status.ReportDownload(operationId, 256 * 1024, 1024 * 1024);

        Assert.False(status.IsIndeterminate);
        Assert.True(status.IsBusy);
        Assert.Equal(25, status.ProgressValue);
        Assert.Contains("256.0 KB", status.DetailText);
        Assert.Contains("1.0 MB", status.DetailText);
        Assert.Contains("25%", status.DetailText);
        Assert.Equal("25%", status.ProgressText);
    }

    [Fact]
    public void Complete_StopsAnimationWhileCompletedMessageRemainsVisible()
    {
        var status = new AssetLoadStatusViewModel();
        var operationId = status.Begin("item", "data", "GeoTIFF", null, 1, 1);

        status.Complete(operationId, "data");

        Assert.True(status.IsActive);
        Assert.False(status.IsBusy);
        Assert.True(status.HasNonReadyStatus);
        Assert.Equal("Asset loaded", status.StatusText);
    }

    [Fact]
    public void Reset_HidesActivityStripWhenStatusReturnsToReady()
    {
        var status = new AssetLoadStatusViewModel();
        status.Begin("item", "data", "GeoTIFF", null, 1, 1);

        status.Reset();

        Assert.Equal("Ready", status.StatusText);
        Assert.False(status.HasNonReadyStatus);
    }

    [Fact]
    public void UpdatesFromSupersededOperation_AreIgnored()
    {
        var status = new AssetLoadStatusViewModel();
        var first = status.Begin("first", "old", "COG", null, 1, 1);
        status.Begin("second", "current", "COG", null, 1, 1);

        status.ReportStage(first, "stale update");

        Assert.Equal("Asset: current (COG)", status.StatusText);
        Assert.DoesNotContain("stale", status.DetailText);
    }

    [Fact]
    public void Stage_WithExpectedSize_ShowsAdvertisedAssetSize()
    {
        var status = new AssetLoadStatusViewModel();
        var operationId = status.Begin(
            "item",
            "data",
            "GeoTIFF",
            512 * 1024 * 1024,
            1,
            1);

        status.ReportStage(operationId, "Opening remote raster in ArcGIS Pro...");

        Assert.Contains("Opening remote raster", status.DetailText);
        Assert.Contains("512.0 MB expected", status.DetailText);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1073741824, "1.0 GB")]
    public void FormatBytes_UsesReadableBinaryUnits(long bytes, string expected)
    {
        Assert.Equal(expected, AssetLoadStatusViewModel.FormatBytes(bytes));
    }
}

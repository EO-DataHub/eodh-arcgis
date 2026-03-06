using eodh.Tools;
using Xunit;

namespace eodh.Tests.Tools;

/// <summary>
/// Req 3: Results Display — tests for bbox overlap calculation used
/// to show "Footprint overlap with AOI" in the results list.
/// </summary>
public class BboxMathTests
{
    // Bbox format: [west, south, east, north]

    [Fact]
    public void FullOverlap_Returns100_WhenItemContainsAoi()
    {
        // Item fully contains the AOI
        double[] item = [-10, -10, 10, 10];
        double[] aoi = [-5, -5, 5, 5];

        var result = BboxMath.CalculateOverlapPercent(item, aoi);

        Assert.NotNull(result);
        Assert.Equal(100.0, result!.Value, precision: 1);
    }

    [Fact]
    public void NoOverlap_Returns0_WhenBboxesDisjoint()
    {
        double[] item = [10, 10, 20, 20];
        double[] aoi = [-10, -10, 0, 0];

        var result = BboxMath.CalculateOverlapPercent(item, aoi);

        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Value, precision: 1);
    }

    [Fact]
    public void PartialOverlap_ReturnsCorrectPercent()
    {
        // AOI: 10x10 area. Item overlaps half of the AOI width.
        double[] item = [-5, 0, 5, 10];   // 10 wide, 10 tall
        double[] aoi = [0, 0, 10, 10];     // 10 wide, 10 tall, area=100
        // Intersection: [0,0,5,10] = 5 wide, 10 tall = 50
        // 50 / 100 = 50%

        var result = BboxMath.CalculateOverlapPercent(item, aoi);

        Assert.NotNull(result);
        Assert.Equal(50.0, result!.Value, precision: 1);
    }

    [Fact]
    public void NullItemBbox_ReturnsNull()
    {
        double[] aoi = [0, 0, 10, 10];

        var result = BboxMath.CalculateOverlapPercent(null, aoi);

        Assert.Null(result);
    }

    [Fact]
    public void NullAoiBbox_ReturnsNull()
    {
        double[] item = [0, 0, 10, 10];

        var result = BboxMath.CalculateOverlapPercent(item, null);

        Assert.Null(result);
    }

    [Fact]
    public void ItemSmallerThanAoi_ReturnsPartialPercent()
    {
        // AOI is 20x20 = 400 area. Item is 10x10 = 100 area, fully inside AOI.
        double[] item = [0, 0, 10, 10];
        double[] aoi = [-5, -5, 15, 15];   // 20 wide, 20 tall, area=400
        // Intersection = item itself = 100
        // 100 / 400 = 25%

        var result = BboxMath.CalculateOverlapPercent(item, aoi);

        Assert.NotNull(result);
        Assert.Equal(25.0, result!.Value, precision: 1);
    }

    [Fact]
    public void ItemBboxTooShort_ReturnsNull()
    {
        double[] item = [0, 0];
        double[] aoi = [0, 0, 10, 10];

        var result = BboxMath.CalculateOverlapPercent(item, aoi);

        Assert.Null(result);
    }

    [Fact]
    public void ZeroAreaAoi_ReturnsNull()
    {
        // AOI is a line (zero area)
        double[] item = [0, 0, 10, 10];
        double[] aoi = [5, 0, 5, 10];

        var result = BboxMath.CalculateOverlapPercent(item, aoi);

        Assert.Null(result);
    }

    [Fact]
    public void SixElementBbox_CalculatesCorrectly_IgnoringElevation()
    {
        // 3D bbox: [west, south, minElev, east, north, maxElev]
        double[] item = [-10, -10, 0, 10, 10, 100];
        double[] aoi = [-5, -5, 5, 5];

        var result = BboxMath.CalculateOverlapPercent(item, aoi);

        Assert.NotNull(result);
        Assert.Equal(100.0, result!.Value, precision: 1);
    }

    [Fact]
    public void SixElementBbox_NoOverlap_ReturnsZero()
    {
        double[] item = [20, 20, 0, 30, 30, 500];
        double[] aoi = [0, 0, 10, 10];

        var result = BboxMath.CalculateOverlapPercent(item, aoi);

        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Value, precision: 1);
    }

    [Fact]
    public void SixElementBbox_PartialOverlap_ReturnsCorrectPercent()
    {
        // 3D bbox: [west=-5, south=0, minElev=0, east=5, north=10, maxElev=100]
        double[] item = [-5, 0, 0, 5, 10, 100];
        double[] aoi = [0, 0, 10, 10];
        // Intersection: [0,0,5,10] = 5*10 = 50/100 = 50%

        var result = BboxMath.CalculateOverlapPercent(item, aoi);

        Assert.NotNull(result);
        Assert.Equal(50.0, result!.Value, precision: 1);
    }
}

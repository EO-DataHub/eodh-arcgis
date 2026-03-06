using eodh.Tools;
using Xunit;

namespace eodh.Tests.Tools;

/// <summary>
/// Req 3: Results Display — tests for result item formatting functions
/// used to display metadata in the results list.
/// </summary>
public class ResultFormatTests
{
    [Fact]
    public void FormatOverlap_FormatsPercentage()
    {
        Assert.Equal("50% overlap", ResultFormat.FormatOverlap(50.0));
    }

    [Fact]
    public void FormatOverlap_ReturnsNull_WhenNull()
    {
        Assert.Null(ResultFormat.FormatOverlap(null));
    }

    [Fact]
    public void FormatOverlap_Rounds_ToWholeNumber()
    {
        Assert.Equal("85% overlap", ResultFormat.FormatOverlap(85.4));
    }

    [Fact]
    public void FormatOverlap_ShowsLessThanOne_WhenSmallPositiveValue()
    {
        Assert.Equal("< 1% overlap", ResultFormat.FormatOverlap(0.3));
    }

    [Fact]
    public void FormatOverlap_ShowsZero_WhenExactlyZero()
    {
        Assert.Equal("0% overlap", ResultFormat.FormatOverlap(0.0));
    }

    [Fact]
    public void FormatOverlap_ShowsLessThanOne_WhenPointFive()
    {
        // F0 banker's rounding: 0.5 rounds to 0, so it falls into "< 1%" range
        Assert.Equal("< 1% overlap", ResultFormat.FormatOverlap(0.5));
    }

    [Fact]
    public void FormatOverlap_ShowsOne_WhenOnePointZero()
    {
        Assert.Equal("1% overlap", ResultFormat.FormatOverlap(1.0));
    }

    [Fact]
    public void FormatLicense_FormatsLicenseString()
    {
        Assert.Equal("License: proprietary", ResultFormat.FormatLicense("proprietary"));
    }

    [Fact]
    public void FormatLicense_ReturnsNull_WhenNull()
    {
        Assert.Null(ResultFormat.FormatLicense(null));
    }

    [Fact]
    public void FormatLicense_ReturnsNull_WhenEmpty()
    {
        Assert.Null(ResultFormat.FormatLicense(""));
    }

    [Fact]
    public void FormatLicense_ReturnsNull_WhenWhitespace()
    {
        Assert.Null(ResultFormat.FormatLicense("  "));
    }

    [Fact]
    public void FormatLicense_FormatsOglLicense()
    {
        Assert.Equal("License: OGL-UK-3.0", ResultFormat.FormatLicense("OGL-UK-3.0"));
    }

    [Fact]
    public void FormatAccuracy_FormatsRmse()
    {
        Assert.Equal("RMSE: 12.5 m", ResultFormat.FormatAccuracy(12.5));
    }

    [Fact]
    public void FormatAccuracy_ReturnsNull_WhenNull()
    {
        Assert.Null(ResultFormat.FormatAccuracy(null));
    }
}

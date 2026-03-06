using System.Globalization;
using System.Windows;
using Xunit;
using eodh.Converters;

namespace eodh.Tests.Converters;

/// <summary>
/// Req 3: Results Display — validates WPF value converters used for formatting
/// dates, cloud cover, and controlling UI element visibility in results views.
/// </summary>
public class CommonConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // BoolToVisibilityConverter

    [Fact]
    public void BoolToVisibility_True_ReturnsVisible()
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.Convert(true, typeof(Visibility), null!, Culture);

        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void BoolToVisibility_False_ReturnsCollapsed()
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.Convert(false, typeof(Visibility), null!, Culture);

        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void BoolToVisibility_ConvertBack_VisibleReturnsTrue()
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.ConvertBack(Visibility.Visible, typeof(bool), null!, Culture);

        Assert.Equal(true, result);
    }

    [Fact]
    public void BoolToVisibility_ConvertBack_CollapsedReturnsFalse()
    {
        var converter = new BoolToVisibilityConverter();
        var result = converter.ConvertBack(Visibility.Collapsed, typeof(bool), null!, Culture);

        Assert.Equal(false, result);
    }

    // InverseBoolToVisibilityConverter

    [Fact]
    public void InverseBoolToVisibility_True_ReturnsCollapsed()
    {
        var converter = new InverseBoolToVisibilityConverter();
        var result = converter.Convert(true, typeof(Visibility), null!, Culture);

        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void InverseBoolToVisibility_False_ReturnsVisible()
    {
        var converter = new InverseBoolToVisibilityConverter();
        var result = converter.Convert(false, typeof(Visibility), null!, Culture);

        Assert.Equal(Visibility.Visible, result);
    }

    // DateTimeFormatConverter

    [Fact]
    public void DateTimeFormat_DateTimeOffset_FormatsCorrectly()
    {
        var converter = new DateTimeFormatConverter();
        var dto = new DateTimeOffset(2024, 3, 15, 10, 30, 0, TimeSpan.Zero);
        var result = converter.Convert(dto, typeof(string), null!, Culture);

        Assert.Equal("2024-03-15 10:30", result);
    }

    [Fact]
    public void DateTimeFormat_DateTime_FormatsCorrectly()
    {
        var converter = new DateTimeFormatConverter();
        var dt = new DateTime(2024, 3, 15, 10, 30, 0);
        var result = converter.Convert(dt, typeof(string), null!, Culture);

        Assert.Equal("2024-03-15 10:30", result);
    }

    [Fact]
    public void DateTimeFormat_NonDateValue_ReturnsEmptyString()
    {
        var converter = new DateTimeFormatConverter();
        var result = converter.Convert("not a date", typeof(string), null!, Culture);

        Assert.Equal(string.Empty, result);
    }

    // CloudCoverConverter

    [Fact]
    public void CloudCover_Double_FormatsAsPercentage()
    {
        var converter = new CloudCoverConverter();
        var result = converter.Convert(15.23, typeof(string), null!, Culture);

        Assert.Equal("15.2%", result);
    }

    [Fact]
    public void CloudCover_NonDouble_ReturnsNA()
    {
        var converter = new CloudCoverConverter();
        var result = converter.Convert("unknown", typeof(string), null!, Culture);

        Assert.Equal("N/A", result);
    }

    // NullToVisibilityConverter

    [Fact]
    public void NullToVisibility_Null_ReturnsCollapsed()
    {
        var converter = new NullToVisibilityConverter();
        var result = converter.Convert(null, typeof(Visibility), null!, Culture);

        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void NullToVisibility_NonNull_ReturnsVisible()
    {
        var converter = new NullToVisibilityConverter();
        var result = converter.Convert("something", typeof(Visibility), null!, Culture);

        Assert.Equal(Visibility.Visible, result);
    }
}

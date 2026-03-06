using Xunit;
using eodh.Models;

namespace eodh.Tests.Models;

/// <summary>
/// Req 2: Search & Filtering — validates SearchFilters.ToSearchParams() produces
/// correct STAC API POST bodies for bbox, date range, collections, and cloud cover.
/// </summary>
public class SearchFiltersTests
{
    [Fact]
    public void ToSearchParams_WithBbox_IncludesBboxArray()
    {
        var filters = new SearchFilters { Bbox = new[] { -1.5, 51.0, 0.5, 52.0 } };
        var result = filters.ToSearchParams();

        Assert.True(result.ContainsKey("bbox"));
        var bbox = (double[])result["bbox"];
        Assert.Equal(new[] { -1.5, 51.0, 0.5, 52.0 }, bbox);
    }

    [Fact]
    public void ToSearchParams_WithNullBbox_OmitsBbox()
    {
        var filters = new SearchFilters { Bbox = null };
        var result = filters.ToSearchParams();

        Assert.False(result.ContainsKey("bbox"));
    }

    [Fact]
    public void ToSearchParams_WithDateRange_IncludesDatetimeInterval()
    {
        var filters = new SearchFilters
        {
            StartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var result = filters.ToSearchParams();

        Assert.True(result.ContainsKey("datetime"));
        var datetime = (string)result["datetime"];
        Assert.Contains("2024-01-01", datetime);
        Assert.Contains("2024-06-01", datetime);
        Assert.Contains("/", datetime);
    }

    [Fact]
    public void ToSearchParams_WithOnlyStartDate_UsesOpenEnd()
    {
        var filters = new SearchFilters
        {
            StartDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = null
        };
        var result = filters.ToSearchParams();

        var datetime = (string)result["datetime"];
        Assert.EndsWith("/..", datetime);
    }

    [Fact]
    public void ToSearchParams_WithOnlyEndDate_UsesOpenStart()
    {
        var filters = new SearchFilters
        {
            StartDate = null,
            EndDate = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var result = filters.ToSearchParams();

        var datetime = (string)result["datetime"];
        Assert.StartsWith("../", datetime);
    }

    [Fact]
    public void ToSearchParams_WithNoDates_OmitsDatetime()
    {
        var filters = new SearchFilters { StartDate = null, EndDate = null };
        var result = filters.ToSearchParams();

        Assert.False(result.ContainsKey("datetime"));
    }

    [Fact]
    public void ToSearchParams_WithCollections_IncludesCollectionsArray()
    {
        var filters = new SearchFilters { Collections = ["sentinel-2-ard", "sentinel-1-rtc"] };
        var result = filters.ToSearchParams();

        Assert.True(result.ContainsKey("collections"));
        var collections = (List<string>)result["collections"];
        Assert.Contains("sentinel-2-ard", collections);
        Assert.Contains("sentinel-1-rtc", collections);
    }

    [Fact]
    public void ToSearchParams_WithEmptyCollections_OmitsCollections()
    {
        var filters = new SearchFilters { Collections = [] };
        var result = filters.ToSearchParams();

        Assert.False(result.ContainsKey("collections"));
    }

    [Fact]
    public void ToSearchParams_WithMaxCloudCover_IncludesCql2Filter()
    {
        var filters = new SearchFilters { MaxCloudCover = 30.0 };
        var result = filters.ToSearchParams();

        Assert.True(result.ContainsKey("filter"));
        Assert.True(result.ContainsKey("filter-lang"));
        Assert.Equal("cql2-json", result["filter-lang"]);

        var filter = (Dictionary<string, object>)result["filter"];
        Assert.Equal("<=", filter["op"]);
    }

    [Fact]
    public void ToSearchParams_WithNullCloudCover_OmitsFilter()
    {
        var filters = new SearchFilters { MaxCloudCover = null };
        var result = filters.ToSearchParams();

        Assert.False(result.ContainsKey("filter"));
        Assert.False(result.ContainsKey("filter-lang"));
    }

    [Fact]
    public void ToSearchParams_AlwaysIncludesLimit()
    {
        var filters = new SearchFilters();
        var result = filters.ToSearchParams();

        Assert.True(result.ContainsKey("limit"));
    }

    [Fact]
    public void DefaultLimit_Is50()
    {
        var filters = new SearchFilters();
        Assert.Equal(50, filters.Limit);
    }
}

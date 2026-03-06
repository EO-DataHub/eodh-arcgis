using Xunit;
using eodh.Models;

namespace eodh.Tests.Models;

/// <summary>
/// Req 3: Results Display — validates datetime parsing and link navigation
/// used for displaying acquisition dates and fetching item details.
/// </summary>
public class StacItemTests
{
    [Fact]
    public void ParsedDateTime_ValidIsoString_ReturnsDateTimeOffset()
    {
        var props = new StacItemProperties(
            "2024-03-15T10:30:00Z", null, null, null, null, null, null, null, null);

        Assert.NotNull(props.ParsedDateTime);
        Assert.Equal(2024, props.ParsedDateTime!.Value.Year);
        Assert.Equal(3, props.ParsedDateTime!.Value.Month);
        Assert.Equal(15, props.ParsedDateTime!.Value.Day);
        Assert.Equal(10, props.ParsedDateTime!.Value.Hour);
        Assert.Equal(30, props.ParsedDateTime!.Value.Minute);
    }

    [Fact]
    public void ParsedDateTime_NullString_ReturnsNull()
    {
        var props = new StacItemProperties(
            null, null, null, null, null, null, null, null, null);

        Assert.Null(props.ParsedDateTime);
    }

    [Fact]
    public void ParsedDateTime_EmptyString_ReturnsNull()
    {
        var props = new StacItemProperties(
            "", null, null, null, null, null, null, null, null);

        Assert.Null(props.ParsedDateTime);
    }

    [Fact]
    public void ParsedDateTime_InvalidString_ReturnsNull()
    {
        var props = new StacItemProperties(
            "not-a-date", null, null, null, null, null, null, null, null);

        Assert.Null(props.ParsedDateTime);
    }

    [Fact]
    public void SelfLink_ReturnsHref_WhenSelfLinkExists()
    {
        var links = new List<StacLink>
        {
            new("self", "https://api.example.com/items/123", null, null),
            new("parent", "https://api.example.com/collection", null, null)
        };
        var item = new StacItem("item-1", "collection-1", null, null, null, null, links);

        Assert.Equal("https://api.example.com/items/123", item.SelfLink);
    }

    [Fact]
    public void SelfLink_ReturnsNull_WhenNoSelfLink()
    {
        var links = new List<StacLink>
        {
            new("parent", "https://api.example.com/collection", null, null)
        };
        var item = new StacItem("item-1", "collection-1", null, null, null, null, links);

        Assert.Null(item.SelfLink);
    }

    [Fact]
    public void SelfLink_ReturnsNull_WhenLinksNull()
    {
        var item = new StacItem("item-1", "collection-1", null, null, null, null, null);

        Assert.Null(item.SelfLink);
    }
}

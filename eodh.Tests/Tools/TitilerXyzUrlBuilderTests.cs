using eodh.Models;
using eodh.Tools;
using Xunit;

namespace eodh.Tests.Tools;

public class TitilerXyzUrlBuilderTests
{
    [Fact]
    public void Build_CreatesAssetSpecificXyzUrl()
    {
        var item = CreateItem(
            "https://eodatahub.org.uk/api/catalogue/stac/collections/s2/items/item 1",
            "visual asset",
            new StacAsset("https://example.test/data.tif", "image/tiff", null, ["data"], null));

        var result = TitilerXyzUrlBuilder.Build(
            "https://eodatahub.org.uk/", item, "visual asset");

        Assert.NotNull(result);
        Assert.StartsWith(
            "https://eodatahub.org.uk/titiler/core/stac/tiles/WebMercatorQuad/{z}/{x}/{y}@1x?",
            result);
        Assert.Contains(
            "url=https%3A%2F%2Feodatahub.org.uk%2Fapi%2Fcatalogue%2Fstac%2Fcollections%2Fs2%2Fitems%2Fitem%201",
            result);
        Assert.Contains("assets=visual%20asset", result);
        Assert.Contains("bidx=3&bidx=2&bidx=1", result);
        Assert.Contains("color_formula=Gamma%20RGB%206%20Saturation%200.8%20Sigmoidal%20RGB%2025%200.35", result);

        var layerUri = new Uri(result, UriKind.Absolute);
        Assert.Contains("/{z}/{x}/{y}@1x", layerUri.OriginalString);
    }

    [Fact]
    public void Build_ReturnsNullWithoutItemSelfLink()
    {
        var item = CreateItem(null, "cog",
            new StacAsset("https://example.test/data.tif", "image/tiff", null, ["data"], null));

        Assert.Null(TitilerXyzUrlBuilder.Build("https://eodatahub.org.uk", item, "cog"));
    }

    [Fact]
    public void Build_ReturnsNullForUnknownAsset()
    {
        var item = CreateItem("https://example.test/item", "cog",
            new StacAsset("https://example.test/data.tif", "image/tiff", null, ["data"], null));

        Assert.Null(TitilerXyzUrlBuilder.Build("https://eodatahub.org.uk", item, "missing"));
    }

    [Theory]
    [InlineData("application/x-netcdf")]
    [InlineData("application/json")]
    [InlineData("image/jpeg")]
    public void Build_ReturnsNullForUnsupportedAsset(string mediaType)
    {
        var item = CreateItem("https://example.test/item", "data",
            new StacAsset("https://example.test/data", mediaType, null, ["data"], null));

        Assert.Null(TitilerXyzUrlBuilder.Build("https://eodatahub.org.uk", item, "data"));
    }

    private static StacItem CreateItem(string? selfLink, string assetKey, StacAsset asset)
    {
        var links = selfLink == null
            ? null
            : new List<StacLink> { new("self", selfLink, null, null) };
        return new StacItem("item", "collection", null, null, null,
            new Dictionary<string, StacAsset> { [assetKey] = asset }, links);
    }
}

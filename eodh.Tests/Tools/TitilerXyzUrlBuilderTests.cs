using System.Text.Json;
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
            "cog",
            new StacAsset("https://example.test/data.tif", "image/tiff", null, ["data"], null),
            collection: "sentinel2_ard");

        var result = TitilerXyzUrlBuilder.Build(
            "https://eodatahub.org.uk/", item, "cog");

        Assert.NotNull(result);
        Assert.StartsWith(
            "https://eodatahub.org.uk/titiler/core/stac/tiles/WebMercatorQuad/{z}/{x}/{y}@1x?",
            result);
        Assert.Contains(
            "url=https%3A%2F%2Feodatahub.org.uk%2Fapi%2Fcatalogue%2Fstac%2Fcollections%2Fs2%2Fitems%2Fitem%201",
            result);
        Assert.Contains("assets=cog", result);
        Assert.Contains("bidx=3&bidx=2&bidx=1", result);
        Assert.Contains("color_formula=Gamma%20RGB%206%20Saturation%200.8%20Sigmoidal%20RGB%2025%200.35", result);

        var layerUri = new Uri(result, UriKind.Absolute);
        Assert.Contains("/{z}/{x}/{y}@1x", layerUri.OriginalString);
    }

    [Fact]
    public void Build_UsesSentinel1ArdVvRenderByDefault()
    {
        var item = CreateItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/public/" +
            "catalogs/ceda-stac-catalogue/collections/sentinel1_ard/items/" +
            "neodc.sentinel_ard.data.sentinel_1.2026.07.15.example",
            "data",
            new StacAsset(
                "https://example.test/sentinel1.tif",
                "image/tiff; application=geotiff",
                null,
                ["data"],
                null),
            collection: "sentinel1_ard");

        var result = TitilerXyzUrlBuilder.Build(
            "https://eodatahub.org.uk", item, "data");

        Assert.NotNull(result);
        Assert.Contains(
            "title=Backscatter%20VV%20%28Vertical-Vertical%29", result);
        Assert.Contains("assets=data", result);
        Assert.Contains("bidx=1", result);
        Assert.Contains("rescale=-24%2C-9", result);
        Assert.Contains("id=VV", result);
        Assert.DoesNotContain("bidx=2", result);
        Assert.DoesNotContain("color_formula=", result);
        Assert.DoesNotContain("nodata=", result);
    }

    [Fact]
    public void Build_CanSelectSentinel1ArdVhRender()
    {
        var item = CreateItem(
            "https://example.test/collections/sentinel1_ard/items/item",
            "data",
            new StacAsset(
                "https://example.test/sentinel1.tif",
                "image/tiff; application=geotiff",
                null,
                ["data"],
                null),
            collection: "sentinel1_ard");

        var result = TitilerXyzUrlBuilder.Build(
            "https://eodatahub.org.uk", item, "data", renderId: "VH");

        Assert.NotNull(result);
        Assert.Contains(
            "title=Backscatter%20VH%20%28Vertical-Horizontal%29", result);
        Assert.Contains("bidx=2", result);
        Assert.Contains("rescale=-40%2C-24", result);
        Assert.Contains("id=VH", result);
    }

    [Fact]
    public void Build_IncludesExpressionParametersForSelectedRender()
    {
        var item = CreateItem(
            "https://example.test/collections/sentinel2_ard/items/item",
            "cog",
            new StacAsset(
                "https://example.test/sentinel2.tif",
                "image/tiff; application=geotiff",
                null,
                ["data"],
                null),
            collection: "sentinel2_ard");

        var result = TitilerXyzUrlBuilder.Build(
            "https://eodatahub.org.uk", item, "cog", renderId: "NDVI");

        Assert.NotNull(result);
        Assert.Contains("bidx=7&bidx=3", result);
        Assert.Contains("rescale=-1%2C1", result);
        Assert.Contains("nodata=0", result);
        Assert.Contains("colormap_name=rdylgn", result);
        Assert.Contains(
            "expression=%28cog_b7-cog_b3%29%2F%28cog_b7%2Bcog_b3%29",
            result);
        Assert.Contains("id=NDVI", result);
    }

    [Fact]
    public void Build_UsesCategoricalColormapForEsaCciLandCover()
    {
        var item = CreateItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/public/" +
            "catalogs/ceda-stac-catalogue/collections/land_cover/items/item",
            "GeoTIFF",
            new StacAsset(
                "https://example.test/land-cover.tif",
                "image/tiff; application=geotiff",
                null,
                ["data"],
                null),
            collection: "land_cover");

        var result = TitilerXyzUrlBuilder.Build(
            "https://eodatahub.org.uk", item, "GeoTIFF");

        Assert.NotNull(result);
        Assert.Contains("title=Land%20Cover%20Map", result);
        Assert.Contains("assets=GeoTIFF", result);
        Assert.Contains("id=Land%20Cover%20Map", result);
        Assert.DoesNotContain("bidx=", result);
        Assert.DoesNotContain("color_formula=", result);

        var colormapParameter = result.Split('?', 2)[1]
            .Split('&')
            .Single(parameter => parameter.StartsWith("colormap="))
            .Split('=', 2)[1];
        var colormap = JsonSerializer.Deserialize<Dictionary<string, string>>(
            Uri.UnescapeDataString(colormapParameter));

        Assert.NotNull(colormap);
        Assert.Equal("#006400", colormap["50"]);
        Assert.Equal("#0046c8", colormap["210"]);
    }

    [Fact]
    public void Build_ReturnsNullWithoutItemSelfLink()
    {
        var item = CreateItem(null, "cog",
            new StacAsset("https://example.test/data.tif", "image/tiff", null, ["data"], null),
            collection: "sentinel2_ard");

        Assert.Null(TitilerXyzUrlBuilder.Build("https://eodatahub.org.uk", item, "cog"));
    }

    [Fact]
    public void Build_ReturnsNullForUnknownAsset()
    {
        var item = CreateItem("https://example.test/item", "cog",
            new StacAsset("https://example.test/data.tif", "image/tiff", null, ["data"], null),
            collection: "sentinel2_ard");

        Assert.Null(TitilerXyzUrlBuilder.Build("https://eodatahub.org.uk", item, "missing"));
    }

    [Fact]
    public void Build_UsesXarrayEndpointForMultidimensionalRender()
    {
        var item = CreateItem(
            "https://example.test/collections/eocis-sst-cdrv3/items/item",
            "reference_file",
            new StacAsset(
                "https://example.test/reference.json",
                "application/json",
                null,
                ["data"],
                null),
            collection: "eocis-sst-cdrv3");

        var result = TitilerXyzUrlBuilder.Build(
            "https://eodatahub.org.uk", item, "reference_file");

        Assert.NotNull(result);
        Assert.StartsWith(
            "https://eodatahub.org.uk/titiler/xarray/tiles/{z}/{x}/{y}@1x?",
            result);
        Assert.Contains("url=https%3A%2F%2Fexample.test%2Freference.json", result);
        Assert.Contains("assets=reference_file", result);
        Assert.Contains("variable=analysed_sst", result);
        Assert.Contains("colormap_name=turbo", result);
        Assert.Contains("rescale=271%2C306", result);
        Assert.Contains("reference=true", result);
        Assert.Contains("id=analysed_sst", result);
        Assert.True(TitilerXyzUrlBuilder.CanBuild(item, "reference_file"));
    }

    [Fact]
    public void Build_ReturnsNullForQuicklookOnlyRender()
    {
        var item = CreateItem(
            "https://example.test/collections/PSScene/items/item",
            "thumbnail",
            new StacAsset(
                "https://example.test/thumbnail.jpg",
                "image/jpeg",
                null,
                ["thumbnail"],
                null),
            collection: "PSScene");

        Assert.False(TitilerXyzUrlBuilder.CanBuild(item, "thumbnail"));
        Assert.Null(TitilerXyzUrlBuilder.Build(
            "https://eodatahub.org.uk", item, "thumbnail"));
    }

    [Fact]
    public void Build_ReturnsNullForCollectionWithoutRenderConfiguration()
    {
        var item = CreateItem(
            "https://example.test/collections/unknown/items/item",
            "data",
            new StacAsset(
                "https://example.test/data.tif",
                "image/tiff",
                null,
                ["data"],
                null),
            collection: "unknown");

        Assert.False(TitilerXyzUrlBuilder.CanBuild(item, "data"));
        Assert.Null(TitilerXyzUrlBuilder.Build(
            "https://eodatahub.org.uk", item, "data"));
    }

    private static StacItem CreateItem(
        string? selfLink,
        string assetKey,
        StacAsset asset,
        string collection = "collection")
    {
        var links = selfLink == null
            ? null
            : new List<StacLink> { new("self", selfLink, null, null) };
        return new StacItem("item", collection, null, null, null,
            new Dictionary<string, StacAsset> { [assetKey] = asset }, links);
    }
}

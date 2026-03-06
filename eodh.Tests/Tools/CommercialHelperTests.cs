using eodh.Models;
using eodh.Tools;
using Xunit;

namespace eodh.Tests.Tools;

public class CommercialHelperTests
{
    private static StacItem MakeItem(string selfLink, string? collection = null)
    {
        return new StacItem("item-1", collection, null, null, null, null,
            [new StacLink("self", selfLink, null, null)]);
    }

    [Fact]
    public void IsCommercialItem_True_WhenSelfLinkContainsCommercial()
    {
        var item = MakeItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/airbus/collections/airbus_phr_data/items/test");
        Assert.True(CommercialHelper.IsCommercialItem(item));
    }

    [Fact]
    public void IsCommercialItem_False_WhenSelfLinkIsPublic()
    {
        var item = MakeItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/supported-datasets/catalogs/ceda/collections/sentinel2_ard/items/test");
        Assert.False(CommercialHelper.IsCommercialItem(item));
    }

    [Fact]
    public void IsCommercialItem_False_WhenNoSelfLink()
    {
        var item = new StacItem("item-1", null, null, null, null, null, null);
        Assert.False(CommercialHelper.IsCommercialItem(item));
    }

    [Fact]
    public void IsCommercialItem_False_WhenNoLinks()
    {
        var item = new StacItem("item-1", null, null, null, null, null, []);
        Assert.False(CommercialHelper.IsCommercialItem(item));
    }

    [Fact]
    public void DetectProvider_AirbusOptical_ForPhrCollection()
    {
        var item = MakeItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/airbus/collections/airbus_phr_data/items/test",
            "airbus_phr_data");
        Assert.Equal(CommercialProvider.AirbusOptical, CommercialHelper.DetectProvider(item));
    }

    [Fact]
    public void DetectProvider_AirbusSar_ForSarCollection()
    {
        var item = MakeItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/airbus/collections/airbus_sar_data/items/test",
            "airbus_sar_data");
        Assert.Equal(CommercialProvider.AirbusSar, CommercialHelper.DetectProvider(item));
    }

    [Fact]
    public void DetectProvider_Planet_ForPlanetCatalog()
    {
        var item = MakeItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/planet/collections/PSScene/items/test",
            "PSScene");
        Assert.Equal(CommercialProvider.Planet, CommercialHelper.DetectProvider(item));
    }

    [Fact]
    public void DetectProvider_Unknown_WhenNoSelfLink()
    {
        var item = new StacItem("item-1", null, null, null, null, null, null);
        Assert.Equal(CommercialProvider.Unknown, CommercialHelper.DetectProvider(item));
    }

    [Fact]
    public void DetectProvider_Unknown_WhenNonCommercialLink()
    {
        var item = MakeItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/ceda/collections/sentinel2_ard/items/test");
        Assert.Equal(CommercialProvider.Unknown, CommercialHelper.DetectProvider(item));
    }

    [Fact]
    public void BboxToCoordinateRing_ReturnsGeoJsonPolygonCoordinates()
    {
        var coords = CommercialHelper.BboxToCoordinateRing([-1.5, 51.0, 0.5, 52.0]);

        Assert.NotNull(coords);
        // Outer array: list of rings (just one exterior ring)
        Assert.Single(coords!);
        var ring = coords[0];
        // Ring: 5 points (4 corners + closing point)
        Assert.Equal(5, ring.Length);
        // First and last points should be identical (closed ring)
        Assert.Equal(ring[0][0], ring[4][0]);
        Assert.Equal(ring[0][1], ring[4][1]);
        // Verify corners
        Assert.Equal([-1.5, 51.0], ring[0]); // SW
        Assert.Equal([0.5, 51.0], ring[1]);   // SE
        Assert.Equal([0.5, 52.0], ring[2]);   // NE
        Assert.Equal([-1.5, 52.0], ring[3]);  // NW
    }

    [Fact]
    public void BboxToCoordinateRing_ReturnsNull_WhenBboxNull()
    {
        Assert.Null(CommercialHelper.BboxToCoordinateRing(null));
    }

    [Fact]
    public void BboxToCoordinateRing_ReturnsNull_WhenBboxWrongLength()
    {
        Assert.Null(CommercialHelper.BboxToCoordinateRing([1.0, 2.0]));
    }

    [Fact]
    public void SupportsCoordinates_False_ForAirbusSar()
    {
        Assert.False(CommercialHelper.SupportsCoordinates(CommercialProvider.AirbusSar));
    }

    [Fact]
    public void SupportsCoordinates_True_ForOtherProviders()
    {
        Assert.True(CommercialHelper.SupportsCoordinates(CommercialProvider.AirbusOptical));
        Assert.True(CommercialHelper.SupportsCoordinates(CommercialProvider.Planet));
    }

    [Fact]
    public void RequiresLicence_False_ForPlanet()
    {
        Assert.False(CommercialHelper.RequiresLicence(CommercialProvider.Planet));
    }

    [Fact]
    public void RequiresLicence_True_ForAirbus()
    {
        Assert.True(CommercialHelper.RequiresLicence(CommercialProvider.AirbusOptical));
        Assert.True(CommercialHelper.RequiresLicence(CommercialProvider.AirbusSar));
    }

    [Fact]
    public void RequiresEndUserCountry_True_OnlyForAirbusOptical()
    {
        Assert.True(CommercialHelper.RequiresEndUserCountry(CommercialProvider.AirbusOptical));
        Assert.False(CommercialHelper.RequiresEndUserCountry(CommercialProvider.AirbusSar));
        Assert.False(CommercialHelper.RequiresEndUserCountry(CommercialProvider.Planet));
    }

    [Fact]
    public void GetLicenceOptions_ReturnsMultiple_ForAirbusOptical()
    {
        var options = CommercialHelper.GetLicenceOptions(CommercialProvider.AirbusOptical);
        Assert.Contains("Standard", options);
        Assert.True(options.Length > 1);
    }

    [Fact]
    public void GetLicenceOptions_ReturnsSingle_ForAirbusSar()
    {
        var options = CommercialHelper.GetLicenceOptions(CommercialProvider.AirbusSar);
        Assert.Single(options);
        Assert.Equal("Single User Licence", options[0]);
    }

    [Fact]
    public void GetLicenceOptions_ReturnsEmpty_ForPlanet()
    {
        Assert.Empty(CommercialHelper.GetLicenceOptions(CommercialProvider.Planet));
    }
}

using eodh.Models;
using eodh.Tools;
using Xunit;

namespace eodh.Tests.Tools;

public class CommercialHelperTests
{
    private static StacItem MakeItem(string selfLink, string? collection = null) =>
        new("item-1", collection, null, null, null, null,
            [new StacLink("self", selfLink, null, null)]);

    [Fact]
    public void DetectProvider_DistinguishesSupportedCommercialProviders()
    {
        Assert.Equal(CommercialProvider.AirbusOptical, CommercialHelper.DetectProvider(MakeItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/airbus/collections/phr/items/1",
            "airbus_phr_data")));
        Assert.Equal(CommercialProvider.AirbusSar, CommercialHelper.DetectProvider(MakeItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/airbus/collections/sar/items/1",
            "airbus_sar_data")));
        Assert.Equal(CommercialProvider.Planet, CommercialHelper.DetectProvider(MakeItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/planet/collections/PSScene/items/1",
            "PSScene")));
        Assert.Equal(CommercialProvider.OpenCosmos, CommercialHelper.DetectProvider(MakeItem(
            "https://eodatahub.org.uk/api/catalogue/stac/catalogs/commercial/catalogs/open-cosmos/collections/accelerator/items/1",
            "accelerator")));
    }

    [Fact]
    public void BboxToCoordinateRing_PreservesCurrentClosedBoundingBoxBehavior()
    {
        var coordinates = CommercialHelper.BboxToCoordinateRing([-1.5, 51, 0.5, 52]);

        Assert.NotNull(coordinates);
        var ring = Assert.Single(coordinates!);
        Assert.Equal(5, ring.Length);
        Assert.Equal(ring[0], ring[^1]);
        Assert.Equal([-1.5, 51], ring[0]);
        Assert.Equal([0.5, 52], ring[2]);
    }

    [Fact]
    public void AirbusOpticalCapabilities_MatchProviderContract()
    {
        var capabilities = CommercialHelper.GetCapabilities(CommercialProvider.AirbusOptical);

        Assert.True(capabilities.SupportsCoordinates);
        Assert.True(capabilities.RequiresEndUserCountry);
        Assert.Equal(9, capabilities.LicenceOptions.Count);
        Assert.Equal(["Visual", "General Use", "Basic", "Analytic"], capabilities.ProductBundles);
        Assert.False(capabilities.HasRadarOptions);
    }

    [Fact]
    public void AirbusSarCapabilities_MatchConditionalProviderContract()
    {
        var capabilities = CommercialHelper.GetCapabilities(CommercialProvider.AirbusSar);

        Assert.False(capabilities.SupportsCoordinates);
        Assert.Equal(3, capabilities.LicenceOptions.Count);
        Assert.Equal(["SSC", "MGD", "GEC", "EEC"], capabilities.ProductBundles);
        Assert.Equal(["rapid", "science"], capabilities.OrbitOptions);
        Assert.False(capabilities.RequiresResolutionVariant("SSC"));
        Assert.True(capabilities.RequiresResolutionVariant("MGD"));
        Assert.False(capabilities.RequiresProjection("SSC"));
        Assert.False(capabilities.RequiresProjection("MGD"));
        Assert.True(capabilities.RequiresProjection("GEC"));
    }

    [Fact]
    public void PlanetCapabilities_HaveNoLicencePicker()
    {
        var capabilities = CommercialHelper.GetCapabilities(CommercialProvider.Planet);

        Assert.Empty(capabilities.LicenceOptions);
        Assert.False(capabilities.RequiresLicence);
        Assert.Equal(["Visual", "General Use", "Basic", "Analytic"], capabilities.ProductBundles);
    }

    [Fact]
    public void OpenCosmosCapabilities_HaveNoProductBundles()
    {
        var capabilities = CommercialHelper.GetCapabilities(CommercialProvider.OpenCosmos);

        Assert.True(capabilities.SupportsCoordinates);
        Assert.False(capabilities.RequiresProductBundle);
        Assert.Empty(capabilities.ProductBundles);
    }
}

using eodh.Models;

namespace eodh.Tools;

internal static class CommercialHelper
{
    private static readonly IReadOnlyList<string> OpticalBundles =
        ["Visual", "General Use", "Basic", "Analytic"];

    private static readonly CommercialProviderCapabilities UnknownCapabilities = new(
        CommercialProvider.Unknown, false, false, [], [], [], [], []);

    private static readonly IReadOnlyDictionary<CommercialProvider, CommercialProviderCapabilities> Capabilities =
        new Dictionary<CommercialProvider, CommercialProviderCapabilities>
        {
            [CommercialProvider.AirbusOptical] = new(
                CommercialProvider.AirbusOptical,
                SupportsCoordinates: true,
                RequiresEndUserCountry: true,
                LicenceOptions:
                [
                    "Standard",
                    "Background Layer",
                    "Standard + Background Layer",
                    "Academic",
                    "Media Licence",
                    "Standard Multi End-Users (2-5)",
                    "Standard Multi End-Users (6-10)",
                    "Standard Multi End-Users (11-30)",
                    "Standard Multi End-Users (>30)"
                ],
                ProductBundles: OpticalBundles,
                OrbitOptions: [],
                ResolutionVariantOptions: [],
                ProjectionOptions: []),
            [CommercialProvider.AirbusSar] = new(
                CommercialProvider.AirbusSar,
                SupportsCoordinates: true,
                RequiresEndUserCountry: false,
                LicenceOptions:
                [
                    "Single User Licence",
                    "Multi User (2 - 5) Licence",
                    "Multi User (6 - 30) Licence"
                ],
                ProductBundles: ["SSC", "MGD", "GEC", "EEC"],
                OrbitOptions: ["rapid", "science"],
                ResolutionVariantOptions: ["RE", "SE"],
                ProjectionOptions: ["Auto", "UTM", "UPS"]),
            [CommercialProvider.Planet] = new(
                CommercialProvider.Planet,
                SupportsCoordinates: true,
                RequiresEndUserCountry: false,
                LicenceOptions: [],
                ProductBundles: OpticalBundles,
                OrbitOptions: [],
                ResolutionVariantOptions: [],
                ProjectionOptions: []),
            [CommercialProvider.OpenCosmos] = new(
                CommercialProvider.OpenCosmos,
                SupportsCoordinates: true,
                RequiresEndUserCountry: false,
                LicenceOptions: [],
                ProductBundles: [],
                OrbitOptions: [],
                ResolutionVariantOptions: [],
                ProjectionOptions: [])
        };

    public static bool IsCommercialItem(StacItem item) =>
        item.SelfLink?.Contains("/catalogs/commercial/", StringComparison.OrdinalIgnoreCase) == true;

    public static CommercialProvider DetectProvider(StacItem item)
    {
        var selfLink = item.SelfLink;
        if (string.IsNullOrEmpty(selfLink))
            return CommercialProvider.Unknown;

        const string marker = "/catalogs/commercial/catalogs/";
        var index = selfLink.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return CommercialProvider.Unknown;

        var providerSegment = selfLink[(index + marker.Length)..].Split('/')[0];
        return providerSegment.ToLowerInvariant() switch
        {
            "airbus" when IsSarCollection(item) => CommercialProvider.AirbusSar,
            "airbus" => CommercialProvider.AirbusOptical,
            "planet" => CommercialProvider.Planet,
            "opencosmos" or "open-cosmos" or "open_cosmos" => CommercialProvider.OpenCosmos,
            _ => CommercialProvider.Unknown
        };
    }

    public static CommercialProviderCapabilities GetCapabilities(CommercialProvider provider) =>
        Capabilities.TryGetValue(provider, out var capabilities)
            ? capabilities
            : UnknownCapabilities;

    public static double[][][]? BboxToCoordinateRing(double[]? bbox)
    {
        if (bbox is not { Length: 4 })
            return null;

        var (west, south, east, north) = (bbox[0], bbox[1], bbox[2], bbox[3]);
        return
        [
            [
                [west, south],
                [east, south],
                [east, north],
                [west, north],
                [west, south]
            ]
        ];
    }

    private static bool IsSarCollection(StacItem item)
    {
        var collection = item.Collection?.ToLowerInvariant() ?? string.Empty;
        return collection.Contains("sar") || collection.Contains("tsx") || collection.Contains("terrasar");
    }
}

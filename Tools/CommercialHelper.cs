using eodh.Models;

namespace eodh.Tools;

/// <summary>
/// Pure helper methods for commercial data detection and order parameter building.
/// </summary>
internal static class CommercialHelper
{
    /// <summary>
    /// Determine if a STAC item is from a commercial catalogue based on its self link.
    /// </summary>
    public static bool IsCommercialItem(StacItem item)
    {
        return item.SelfLink?.Contains("/catalogs/commercial/", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Detect the commercial provider from the item's self link URL.
    /// Looks for the path segment after "/catalogs/commercial/catalogs/".
    /// </summary>
    public static CommercialProvider DetectProvider(StacItem item)
    {
        var selfLink = item.SelfLink;
        if (string.IsNullOrEmpty(selfLink)) return CommercialProvider.Unknown;

        const string marker = "/catalogs/commercial/catalogs/";
        var idx = selfLink.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return CommercialProvider.Unknown;

        var afterMarker = selfLink[(idx + marker.Length)..];
        var providerSegment = afterMarker.Split('/')[0].ToLowerInvariant();

        return providerSegment switch
        {
            "airbus" when IsSarCollection(item) => CommercialProvider.AirbusSar,
            "airbus" => CommercialProvider.AirbusOptical,
            "planet" => CommercialProvider.Planet,
            _ => CommercialProvider.Unknown
        };
    }

    /// <summary>
    /// Whether the provider supports AOI clipping coordinates in quote/order.
    /// Airbus SAR does NOT support coordinates.
    /// </summary>
    public static bool SupportsCoordinates(CommercialProvider provider) =>
        provider != CommercialProvider.AirbusSar;

    /// <summary>
    /// Whether the provider requires a licence field.
    /// Planet does not.
    /// </summary>
    public static bool RequiresLicence(CommercialProvider provider) =>
        provider != CommercialProvider.Planet;

    /// <summary>
    /// Whether the provider requires endUserCountry.
    /// Only Airbus Optical.
    /// </summary>
    public static bool RequiresEndUserCountry(CommercialProvider provider) =>
        provider == CommercialProvider.AirbusOptical;

    /// <summary>
    /// Convert a bounding box [west, south, east, north] to GeoJSON Polygon
    /// coordinates suitable for the quote/order API: [[[lon,lat], ...]].
    /// The outer array is the list of rings (just one exterior ring here).
    /// </summary>
    public static double[][][]? BboxToCoordinateRing(double[]? bbox)
    {
        if (bbox is not { Length: 4 }) return null;

        var west = bbox[0];
        var south = bbox[1];
        var east = bbox[2];
        var north = bbox[3];

        // Wrap the ring in an outer array (GeoJSON Polygon coordinates format)
        return
        [
            [
                [west, south],
                [east, south],
                [east, north],
                [west, north],
                [west, south] // close the ring
            ]
        ];
    }

    /// <summary>
    /// Get default licence options for a provider.
    /// </summary>
    public static string[] GetLicenceOptions(CommercialProvider provider) =>
        provider switch
        {
            CommercialProvider.AirbusOptical =>
            [
                "Standard",
                "Background Layer",
                "Standard + Background Layer",
                "Academic",
                "Media Licence",
                "Standard Multi End-Users (2-5)"
            ],
            CommercialProvider.AirbusSar =>
            [
                "Single User Licence"
            ],
            _ => []
        };

    /// <summary>
    /// Heuristic: SAR collections include "sar", "tsx", or "terrasar" in the collection ID.
    /// </summary>
    private static bool IsSarCollection(StacItem item)
    {
        var col = item.Collection?.ToLowerInvariant() ?? "";
        return col.Contains("sar") || col.Contains("tsx") || col.Contains("terrasar");
    }
}

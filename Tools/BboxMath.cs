namespace eodh.Tools;

/// <summary>
/// Pure math utilities for bounding box operations.
/// </summary>
internal static class BboxMath
{
    /// <summary>
    /// Calculate what percentage of the AOI bbox is covered by the item bbox.
    /// Both bboxes are in WGS84. Supports 4-element [west, south, east, north]
    /// and 6-element [west, south, minElev, east, north, maxElev] (3D) bboxes.
    /// Returns 0-100, or null if either bbox is invalid/null.
    /// </summary>
    public static double? CalculateOverlapPercent(IReadOnlyList<double>? itemBbox, double[]? aoiBbox)
    {
        if (itemBbox == null || aoiBbox == null) return null;
        if (itemBbox.Count < 4 || aoiBbox.Length < 4) return null;

        if (!TryExtract2D(itemBbox, out var itemWest, out var itemSouth, out var itemEast, out var itemNorth))
            return null;
        if (!TryExtract2D(aoiBbox, out var aoiWest, out var aoiSouth, out var aoiEast, out var aoiNorth))
            return null;

        var aoiWidth = aoiEast - aoiWest;
        var aoiHeight = aoiNorth - aoiSouth;
        var aoiArea = aoiWidth * aoiHeight;
        if (aoiArea <= 0) return null;

        var interWest = Math.Max(itemWest, aoiWest);
        var interSouth = Math.Max(itemSouth, aoiSouth);
        var interEast = Math.Min(itemEast, aoiEast);
        var interNorth = Math.Min(itemNorth, aoiNorth);

        var interWidth = interEast - interWest;
        var interHeight = interNorth - interSouth;

        if (interWidth <= 0 || interHeight <= 0) return 0.0;

        var interArea = interWidth * interHeight;
        var percent = interArea / aoiArea * 100.0;
        return Math.Min(percent, 100.0);
    }

    /// <summary>
    /// Extract the 2D (west, south, east, north) values from a bbox array,
    /// handling both 4-element and 6-element (3D) formats per the STAC/GeoJSON spec.
    /// </summary>
    private static bool TryExtract2D(IReadOnlyList<double> bbox,
        out double west, out double south, out double east, out double north)
    {
        if (bbox.Count == 6)
        {
            // 3D bbox: [west, south, minElev, east, north, maxElev]
            west = bbox[0];
            south = bbox[1];
            east = bbox[3];
            north = bbox[4];
            return true;
        }

        if (bbox.Count >= 4)
        {
            // 2D bbox: [west, south, east, north]
            west = bbox[0];
            south = bbox[1];
            east = bbox[2];
            north = bbox[3];
            return true;
        }

        west = south = east = north = 0;
        return false;
    }
}

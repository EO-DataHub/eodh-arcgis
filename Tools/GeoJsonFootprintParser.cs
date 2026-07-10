using System.Text.Json;
using eodh.Models;

namespace eodh.Tools;

internal sealed record FootprintPosition(double X, double Y);

internal sealed record FootprintPolygon(
    IReadOnlyList<IReadOnlyList<FootprintPosition>> Rings);

/// <summary>
/// Pure GeoJSON Polygon/MultiPolygon parser used before ArcGIS geometry creation.
/// </summary>
internal static class GeoJsonFootprintParser
{
    public static bool TryParse(
        GeoJsonGeometry? geometry,
        out IReadOnlyList<FootprintPolygon> polygons)
    {
        polygons = [];
        if (geometry == null)
            return false;

        try
        {
            var parsed = geometry.Type switch
            {
                "Polygon" => ParsePolygonCollection([geometry.Coordinates]),
                "MultiPolygon" => ParsePolygonCollection(geometry.Coordinates.EnumerateArray()),
                _ => []
            };

            if (parsed.Count == 0)
                return false;

            polygons = parsed;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static List<FootprintPolygon> ParsePolygonCollection(
        IEnumerable<JsonElement> polygonElements)
    {
        var polygons = new List<FootprintPolygon>();
        foreach (var polygonElement in polygonElements)
        {
            if (polygonElement.ValueKind != JsonValueKind.Array)
                continue;

            var rings = new List<IReadOnlyList<FootprintPosition>>();
            foreach (var ringElement in polygonElement.EnumerateArray())
            {
                var ring = ParseRing(ringElement);
                if (ring != null)
                    rings.Add(ring);
            }

            if (rings.Count > 0)
                polygons.Add(new FootprintPolygon(rings));
        }

        return polygons;
    }

    private static IReadOnlyList<FootprintPosition>? ParseRing(JsonElement ringElement)
    {
        if (ringElement.ValueKind != JsonValueKind.Array)
            return null;

        var positions = new List<FootprintPosition>();
        foreach (var positionElement in ringElement.EnumerateArray())
        {
            if (positionElement.ValueKind != JsonValueKind.Array ||
                positionElement.GetArrayLength() < 2)
                return null;

            var x = positionElement[0].GetDouble();
            var y = positionElement[1].GetDouble();
            if (!double.IsFinite(x) || !double.IsFinite(y))
                return null;

            positions.Add(new FootprintPosition(x, y));
        }

        if (positions.Count < 3)
            return null;

        if (positions[0] != positions[^1])
            positions.Add(positions[0]);

        return positions.Count >= 4 ? positions : null;
    }
}

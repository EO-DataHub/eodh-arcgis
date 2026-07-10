using System.Text.Json;
using eodh.Models;
using eodh.Tools;
using Xunit;

namespace eodh.Tests.Tools;

public class GeoJsonFootprintParserTests
{
    [Fact]
    public void TryParse_Polygon_PreservesInteriorRing()
    {
        var geometry = CreateGeometry("Polygon", """
            [
              [[0,0],[4,0],[4,4],[0,4],[0,0]],
              [[1,1],[1,2],[2,2],[2,1],[1,1]]
            ]
            """);

        var parsed = GeoJsonFootprintParser.TryParse(geometry, out var polygons);

        Assert.True(parsed);
        var polygon = Assert.Single(polygons);
        Assert.Equal(2, polygon.Rings.Count);
        Assert.Equal(5, polygon.Rings[0].Count);
        Assert.Equal(new FootprintPosition(1, 1), polygon.Rings[1][0]);
    }

    [Fact]
    public void TryParse_MultiPolygon_ReturnsEveryPolygon()
    {
        var geometry = CreateGeometry("MultiPolygon", """
            [
              [[[0,0],[1,0],[1,1],[0,1],[0,0]]],
              [[[10,10],[11,10],[11,11],[10,11],[10,10]]]
            ]
            """);

        var parsed = GeoJsonFootprintParser.TryParse(geometry, out var polygons);

        Assert.True(parsed);
        Assert.Equal(2, polygons.Count);
    }

    [Fact]
    public void TryParse_ClosesOpenRing()
    {
        var geometry = CreateGeometry("Polygon", "[[[0,0],[1,0],[0,1]]]");

        Assert.True(GeoJsonFootprintParser.TryParse(geometry, out var polygons));
        var ring = Assert.Single(polygons).Rings[0];
        Assert.Equal(ring[0], ring[^1]);
    }

    [Theory]
    [InlineData("Point", "[0,0]")]
    [InlineData("Polygon", "[[[0,0],[1,1]]]")]
    public void TryParse_UnsupportedOrInvalidGeometry_ReturnsFalse(string type, string coordinates)
    {
        var geometry = CreateGeometry(type, coordinates);

        Assert.False(GeoJsonFootprintParser.TryParse(geometry, out var polygons));
        Assert.Empty(polygons);
    }

    private static GeoJsonGeometry CreateGeometry(string type, string coordinates)
    {
        using var document = JsonDocument.Parse(coordinates);
        return new GeoJsonGeometry(type, document.RootElement.Clone());
    }
}

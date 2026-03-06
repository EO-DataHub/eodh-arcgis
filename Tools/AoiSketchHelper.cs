using ArcGIS.Core.Geometry;

namespace eodh.Tools;

/// <summary>
/// Testable helper for AOI sketch processing. Separated from DrawAoiTool
/// (which inherits MapTool) so tests can call it without loading the
/// ArcGIS Desktop Extensions module.
/// </summary>
internal static class AoiSketchHelper
{
    /// <summary>
    /// Raised when the user completes drawing an AOI rectangle.
    /// The envelope is in WGS84 (EPSG:4326).
    /// </summary>
    internal static event Action<Envelope>? AoiDrawn;

    /// <summary>
    /// Projects the drawn geometry to WGS84 and raises AoiDrawn.
    /// </summary>
    internal static bool ProcessSketchGeometry(Geometry? geometry)
    {
        if (geometry == null) return false;

        try
        {
            var wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326);
            var projected = GeometryEngine.Instance.Project(geometry, wgs84);
            var envelope = projected.Extent;

            AoiDrawn?.Invoke(envelope);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

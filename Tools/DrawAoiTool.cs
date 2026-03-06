using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace eodh.Tools;

/// <summary>
/// Map tool for drawing an Area of Interest (AOI) polygon.
/// When the user sketches a rectangle on the map,
/// the bounding envelope is sent to the SearchViewModel as the search AOI.
/// </summary>
internal class DrawAoiTool : MapTool
{
    public DrawAoiTool()
    {
        IsSketchTool = true;
        SketchType = SketchGeometryType.Rectangle;
        SketchOutputMode = SketchOutputMode.Map;
    }

    protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
    {
        return QueuedTask.Run(() => AoiSketchHelper.ProcessSketchGeometry(geometry));
    }
}

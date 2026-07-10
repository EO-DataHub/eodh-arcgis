using ArcGIS.Core.Geometry;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using eodh.Models;
using eodh.Tools;

namespace eodh.Services;

internal sealed record FootprintRenderResult(int RenderedCount, int SkippedCount);

/// <summary>
/// Owns every temporary search-footprint overlay and the selected highlight.
/// No persistent layer or project data is created.
/// </summary>
internal sealed class FootprintOverlayService : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<IDisposable> _overlays = [];
    private readonly Dictionary<string, Geometry> _geometries =
        new(StringComparer.Ordinal);
    private IDisposable? _selectionOverlay;
    private bool _disposed;

    public async Task<FootprintRenderResult> ReplaceAsync(
        IEnumerable<StacItem> items,
        string? selectedItemId,
        bool isVisible)
    {
        var snapshot = items.ToList();
        await _gate.WaitAsync();
        try
        {
            if (_disposed)
                return new FootprintRenderResult(0, snapshot.Count);

            return await QueuedTask.Run(() =>
            {
                ClearCore();
                if (!isVisible)
                    return new FootprintRenderResult(0, 0);

                var mapView = GetActiveMapView();
                if (mapView?.Map == null)
                    return new FootprintRenderResult(0, snapshot.Count);

                var ordinarySymbol = CreateOrdinarySymbol();
                var rendered = 0;
                var skipped = 0;

                foreach (var item in snapshot)
                {
                    var geometry = CreateGeometry(item.Geometry, mapView.Map.SpatialReference);
                    if (geometry == null)
                    {
                        skipped++;
                        continue;
                    }

                    _overlays.Add(mapView.AddOverlay(geometry, ordinarySymbol));
                    _geometries[item.Id] = geometry;
                    rendered++;
                }

                HighlightCore(mapView, selectedItemId);
                return new FootprintRenderResult(rendered, skipped);
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task HighlightAsync(string? itemId)
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed)
                return;

            await QueuedTask.Run(() =>
            {
                _selectionOverlay?.Dispose();
                _selectionOverlay = null;
                HighlightCore(GetActiveMapView(), itemId);
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!_disposed)
                await QueuedTask.Run(ClearCore);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _ = QueuedTask.Run(ClearCore);
        _gate.Dispose();
    }

    private void HighlightCore(MapView? mapView, string? itemId)
    {
        if (mapView == null || itemId == null || !_geometries.TryGetValue(itemId, out var geometry))
            return;

        _selectionOverlay = mapView.AddOverlay(geometry, CreateSelectedSymbol());
    }

    private void ClearCore()
    {
        _selectionOverlay?.Dispose();
        _selectionOverlay = null;
        foreach (var overlay in _overlays)
            overlay.Dispose();
        _overlays.Clear();
        _geometries.Clear();
    }

    private static Geometry? CreateGeometry(
        GeoJsonGeometry? geoJson,
        SpatialReference targetSpatialReference)
    {
        if (!GeoJsonFootprintParser.TryParse(geoJson, out var polygons))
            return null;

        try
        {
            var wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326);
            var builder = new PolygonBuilderEx(wgs84);
            foreach (var polygon in polygons)
            {
                for (var ringIndex = 0; ringIndex < polygon.Rings.Count; ringIndex++)
                {
                    var ring = OrientRing(polygon.Rings[ringIndex], exterior: ringIndex == 0);
                    builder.AddPart(ring.Select(point => new Coordinate2D(point.X, point.Y)));
                }
            }

            var geometry = builder.ToGeometry();
            if (geometry.IsEmpty)
                return null;

            var simplified = GeometryEngine.Instance.SimplifyAsFeature(geometry);
            return GeometryEngine.Instance.Project(simplified, targetSpatialReference);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IReadOnlyList<FootprintPosition> OrientRing(
        IReadOnlyList<FootprintPosition> ring,
        bool exterior)
    {
        var signedArea = 0d;
        for (var index = 0; index < ring.Count - 1; index++)
            signedArea += ring[index].X * ring[index + 1].Y - ring[index + 1].X * ring[index].Y;

        var isClockwise = signedArea < 0;
        var shouldBeClockwise = exterior;
        return isClockwise == shouldBeClockwise
            ? ring
            : ring.Reverse().ToList();
    }

    private static MapView? GetActiveMapView()
    {
        try
        {
            return MapView.Active;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static CIMSymbolReference CreateOrdinarySymbol()
    {
        var outline = SymbolFactory.Instance.ConstructStroke(
            ColorFactory.Instance.CreateRGBColor(40, 130, 210, 160),
            1.2,
            SimpleLineStyle.Solid);
        return SymbolFactory.Instance.ConstructPolygonSymbol(
            ColorFactory.Instance.CreateRGBColor(40, 130, 210, 24),
            SimpleFillStyle.Solid,
            outline).MakeSymbolReference();
    }

    private static CIMSymbolReference CreateSelectedSymbol()
    {
        var outline = SymbolFactory.Instance.ConstructStroke(
            ColorFactory.Instance.CreateRGBColor(255, 190, 0),
            3,
            SimpleLineStyle.Solid);
        return SymbolFactory.Instance.ConstructPolygonSymbol(
            ColorFactory.Instance.CreateRGBColor(255, 210, 0, 70),
            SimpleFillStyle.Solid,
            outline).MakeSymbolReference();
    }
}

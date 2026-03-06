using System.Collections.ObjectModel;
using System.IO;
using GeoJSON.Net.Feature;
using GeoJson = GeoJSON.Net.Geometry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using eodh.Models;
using eodh.Services;
using eodh.Tools;

namespace eodh.ViewModels;

/// <summary>
/// ViewModel for the search panel. Handles catalog/collection browsing,
/// AOI selection, date range, cloud cover filters, and search execution.
/// </summary>
internal class SearchViewModel : PropertyChangedBase
{
    private readonly StacClient _stacClient;
    private readonly Action<List<StacItem>> _onSearchCompleted;

    private StacCatalog? _selectedCatalog;
    private StacCollection? _selectedCollection;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private double _maxCloudCover = 100;
    private double[]? _aoiBbox;
    private IDisposable? _aoiOverlay;
    private string _aoiDescription = "No area selected";
    private bool _isSearching;
    private string _resultSummary = string.Empty;

    public SearchViewModel(StacClient stacClient, Action<List<StacItem>> onSearchCompleted)
    {
        _stacClient = stacClient;
        _onSearchCompleted = onSearchCompleted;

        _endDate = DateTime.Today;
        _startDate = DateTime.Today.AddMonths(-2);

        SearchCommand = new RelayCommand(ExecuteSearch, CanSearch);
        DrawAoiCommand = new RelayCommand(ExecuteDrawAoi);
        UseMapExtentCommand = new RelayCommand(ExecuteUseMapExtent);
        ImportAoiCommand = new RelayCommand(ExecuteImportAoi);

        AoiSketchHelper.AoiDrawn += SetAoiFromPolygon;
    }

    #region Properties

    public ObservableCollection<StacCatalog> Catalogs { get; } = [];
    public ObservableCollection<StacCollection> Collections { get; } = [];

    public StacCatalog? SelectedCatalog
    {
        get => _selectedCatalog;
        set
        {
            if (SetProperty(ref _selectedCatalog, value))
                _ = LoadCollectionsAsync();
        }
    }

    public StacCollection? SelectedCollection
    {
        get => _selectedCollection;
        set => SetProperty(ref _selectedCollection, value);
    }

    public DateTime? StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    public DateTime? EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    public double MaxCloudCover
    {
        get => _maxCloudCover;
        set => SetProperty(ref _maxCloudCover, value);
    }

    public string AoiDescription
    {
        get => _aoiDescription;
        set => SetProperty(ref _aoiDescription, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        set
        {
            SetProperty(ref _isSearching, value);
            NotifyCanSearchChanged();
        }
    }

    public string ResultSummary
    {
        get => _resultSummary;
        set => SetProperty(ref _resultSummary, value);
    }

    public SearchFilters? CurrentFilters { get; private set; }

    public ICommand SearchCommand { get; }
    public ICommand DrawAoiCommand { get; }
    public ICommand UseMapExtentCommand { get; }
    public ICommand ImportAoiCommand { get; }

    #endregion

    #region Public Methods

    public async Task LoadCatalogsAsync()
    {
        try
        {
            var catalogs = await _stacClient.GetCatalogsAsync();
            Catalogs.Clear();
            foreach (var cat in catalogs)
                Catalogs.Add(cat);

            if (Catalogs.Count > 0)
                SelectedCatalog = Catalogs[0];
        }
        catch (Exception ex)
        {
            ResultSummary = $"Failed to load catalogs: {ex.Message}";
        }
    }

    /// <summary>
    /// Called by DrawAoiTool when the user finishes drawing a polygon.
    /// </summary>
    internal void SetAoiFromPolygon(Envelope envelope)
    {
        _aoiBbox = [envelope.XMin, envelope.YMin, envelope.XMax, envelope.YMax];

        // Set observable properties directly — safe from any thread for test assertions
        // and WPF binding engine picks up PropertyChanged regardless of source thread.
        AoiDescription = $"Drawn AOI: {envelope.XMin:F2}, {envelope.YMin:F2} to {envelope.XMax:F2}, {envelope.YMax:F2}";

        NotifyCanSearchChanged();

        _ = ShowAoiOnMap();
    }

    /// <summary>
    /// Set AOI from a GeoJSON string (Feature, FeatureCollection, or raw Geometry).
    /// Extracts the bounding box from the bbox property or computes it from coordinates.
    /// </summary>
    internal void SetAoiFromGeoJson(string geoJson)
    {
        var jObj = JObject.Parse(geoJson);
        var type = jObj["type"]?.ToString();
        double[]? bbox = null;

        // Try bbox property first
        if (jObj["bbox"] is JArray bboxArray && bboxArray.Count >= 4)
        {
            bbox =
            [
                (double)bboxArray[0], (double)bboxArray[1],
                (double)bboxArray[2], (double)bboxArray[3]
            ];
        }
        else
        {
            // Extract geometry and compute extent using GeoJSON.Net types
            GeoJson.IGeometryObject? geometry = type switch
            {
                "FeatureCollection" => JsonConvert.DeserializeObject<FeatureCollection>(geoJson)
                    ?.Features.FirstOrDefault()?.Geometry,
                "Feature" => JsonConvert.DeserializeObject<Feature>(geoJson)?.Geometry,
                _ => DeserializeGeometry(geoJson, type)
            };

            if (geometry != null)
                bbox = ComputeExtent(geometry);
        }

        if (bbox == null) return;

        _aoiBbox = bbox;
        AoiDescription = $"Imported AOI: {bbox[0]:F2}, {bbox[1]:F2} to {bbox[2]:F2}, {bbox[3]:F2}";
        NotifyCanSearchChanged();
        _ = ShowAoiOnMap();
    }

    #endregion

    #region Private Methods

    private async Task LoadCollectionsAsync()
    {
        if (SelectedCatalog == null) return;

        ResultSummary = $"Loading collections for '{SelectedCatalog.Id}'...";

        try
        {
            var collections = await _stacClient.GetCollectionsAsync(SelectedCatalog);
            Collections.Clear();
            foreach (var col in collections)
                Collections.Add(col);
            if (Collections.Count > 0)
                SelectedCollection = Collections[0];
            ResultSummary = $"Loaded {collections.Count} collections for '{SelectedCatalog.Id}'";
        }
        catch (Exception ex)
        {
            ResultSummary = $"Failed to load collections: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private bool CanSearch() => !IsSearching && _aoiBbox != null;

    /// <summary>
    /// Safely raise CanExecuteChanged — uses BeginInvoke when off the UI thread
    /// to avoid deadlocking on Dispatcher.Invoke (which blocks if the UI thread
    /// isn't pumping, e.g. in test runners or during CIM background work).
    /// </summary>
    private void NotifyCanSearchChanged()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.BeginInvoke(() => ((RelayCommand)SearchCommand).RaiseCanExecuteChanged());
        else
            ((RelayCommand)SearchCommand).RaiseCanExecuteChanged();
    }

    private async void ExecuteSearch()
    {
        if (SelectedCatalog == null || _aoiBbox == null) return;

        IsSearching = true;
        ResultSummary = "Searching...";

        try
        {
            var filters = new SearchFilters
            {
                Bbox = _aoiBbox,
                StartDate = StartDate.HasValue ? new DateTimeOffset(StartDate.Value) : null,
                EndDate = EndDate.HasValue ? new DateTimeOffset(EndDate.Value) : null,
                Collections = SelectedCollection != null ? [SelectedCollection.Id] : [],
                MaxCloudCover = MaxCloudCover < 100 ? MaxCloudCover : null,
                Limit = 50
            };

            CurrentFilters = filters;
            var result = await _stacClient.SearchAsync(SelectedCatalog, filters);

            ResultSummary = $"Found {result.TotalCount} items ({result.Items.Count} shown)\n\nDEBUG:\n{_stacClient.LastSearchDebug}";
            _onSearchCompleted.Invoke(result.Items);
        }
        catch (Exception ex)
        {
            ResultSummary = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void ExecuteDrawAoi()
    {
        FrameworkApplication.SetCurrentToolAsync("eodh_DrawAoiTool");
    }

    private async void ExecuteImportAoi()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import AOI",
            Filter = "GeoJSON files (*.geojson;*.json)|*.geojson;*.json|All files (*.*)|*.*"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = await File.ReadAllTextAsync(dlg.FileName);
            SetAoiFromGeoJson(json);
        }
        catch (Exception ex)
        {
            ResultSummary = $"Failed to import AOI: {ex.Message}";
        }
    }

    private async void ExecuteUseMapExtent()
    {
        try
        {
            if (MapView.Active == null) return;

            var bbox = await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView == null) return (double[]?)null;

                var extent = mapView.Extent;
                var wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326);
                var projected = GeometryEngine.Instance.Project(extent, wgs84) as Envelope;

                if (projected != null)
                    return new[] { projected.XMin, projected.YMin, projected.XMax, projected.YMax };

                return null;
            });

            if (bbox != null)
            {
                _aoiBbox = bbox;
                AoiDescription = $"Map extent: {bbox[0]:F2}, {bbox[1]:F2} to {bbox[2]:F2}, {bbox[3]:F2}";
                NotifyCanSearchChanged();
                _ = ShowAoiOnMap();
            }
        }
        catch (Exception ex)
        {
            ResultSummary = $"Failed to get map extent: {ex.Message}";
        }
    }

    private async Task ShowAoiOnMap()
    {
        if (_aoiBbox == null) return;
        if (MapView.Active == null) return;

        var bbox = _aoiBbox;
        await QueuedTask.Run(() =>
        {
            var mapView = MapView.Active;
            if (mapView == null) return;

            // Remove previous overlay
            _aoiOverlay?.Dispose();
            _aoiOverlay = null;

            // Build rectangle in WGS84, then project to map's spatial reference
            var wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326);
            var envelope = EnvelopeBuilderEx.CreateEnvelope(
                new Coordinate2D(bbox[0], bbox[1]),
                new Coordinate2D(bbox[2], bbox[3]),
                wgs84);
            var polygon = PolygonBuilderEx.CreatePolygon(envelope);
            var projected = GeometryEngine.Instance.Project(polygon, mapView.Map.SpatialReference);

            // Semi-transparent blue fill with dashed outline
            var outline = SymbolFactory.Instance.ConstructStroke(
                ColorFactory.Instance.CreateRGBColor(0, 90, 200), 2, SimpleLineStyle.Dash);
            var fill = SymbolFactory.Instance.ConstructPolygonSymbol(
                ColorFactory.Instance.CreateRGBColor(0, 120, 255, 30),
                SimpleFillStyle.Solid, outline);

            _aoiOverlay = mapView.AddOverlay(projected, fill.MakeSymbolReference());
        });
    }

    private static GeoJson.IGeometryObject? DeserializeGeometry(string geoJson, string? type)
    {
        return type switch
        {
            "Polygon" => JsonConvert.DeserializeObject<GeoJson.Polygon>(geoJson),
            "MultiPolygon" => JsonConvert.DeserializeObject<GeoJson.MultiPolygon>(geoJson),
            "Point" => JsonConvert.DeserializeObject<GeoJson.Point>(geoJson),
            "LineString" => JsonConvert.DeserializeObject<GeoJson.LineString>(geoJson),
            _ => null
        };
    }

    private static double[]? ComputeExtent(GeoJson.IGeometryObject geometry)
    {
        var positions = new List<GeoJson.IPosition>();
        CollectPositions(geometry, positions);

        if (positions.Count == 0) return null;

        return
        [
            positions.Min(p => p.Longitude),
            positions.Min(p => p.Latitude),
            positions.Max(p => p.Longitude),
            positions.Max(p => p.Latitude)
        ];
    }

    private static void CollectPositions(GeoJson.IGeometryObject geometry, List<GeoJson.IPosition> positions)
    {
        switch (geometry)
        {
            case GeoJson.Polygon polygon:
                foreach (var ring in polygon.Coordinates)
                    positions.AddRange(ring.Coordinates);
                break;
            case GeoJson.MultiPolygon multi:
                foreach (var poly in multi.Coordinates)
                    foreach (var ring in poly.Coordinates)
                        positions.AddRange(ring.Coordinates);
                break;
            case GeoJson.LineString line:
                positions.AddRange(line.Coordinates);
                break;
            case GeoJson.Point point:
                positions.Add(point.Coordinates);
                break;
        }
    }

    #endregion
}

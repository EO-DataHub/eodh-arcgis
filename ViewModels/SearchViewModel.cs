using System.Collections.ObjectModel;
using System.IO;
using ArcData = ArcGIS.Core.Data;
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

    private CatalogRoot? _selectedCatalog;
    private CatalogCollectionEntry? _selectedCollection;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private double _maxCloudCover = 100;
    private double[]? _aoiBbox;
    private bool _aoiFromCollection;
    private IDisposable? _aoiOverlay;
    private string _aoiDescription = "No area selected";
    private bool _isSearching;
    private bool _isLoadingCollections;
    private string _resultSummary = string.Empty;

    public SearchViewModel(StacClient stacClient, Action<List<StacItem>> onSearchCompleted)
    {
        _stacClient = stacClient;
        _onSearchCompleted = onSearchCompleted;

        _endDate = DateTime.Today;
        _startDate = DateTime.Today.AddMonths(-1);

        SearchCommand = new RelayCommand(ExecuteSearch, CanSearch);
        DrawAoiCommand = new RelayCommand(ExecuteDrawAoi);
        UseMapExtentCommand = new RelayCommand(ExecuteUseMapExtent);
        ImportAoiCommand = new RelayCommand(ExecuteImportAoi);
        ClearAoiCommand = new RelayCommand(ExecuteClearAoi, CanClearAoi);

        AoiSketchHelper.AoiDrawn += SetAoiFromPolygon;

        foreach (var root in _stacClient.CatalogRoots)
            Catalogs.Add(root);
    }

    #region Properties

    public ObservableCollection<CatalogRoot> Catalogs { get; } = [];
    public ObservableCollection<CatalogCollectionEntry> Collections { get; } = [];

    public CatalogRoot? SelectedCatalog
    {
        get => _selectedCatalog;
        set
        {
            if (SetProperty(ref _selectedCatalog, value))
            {
                NotifyPropertyChanged(nameof(IsCommercialCatalog));
                _ = LoadCollectionsAsync();
            }
        }
    }

    public bool IsCommercialCatalog => SelectedCatalog == CatalogRoot.Commercial;

    public CatalogCollectionEntry? SelectedCollection
    {
        get => _selectedCollection;
        set
        {
            if (SetProperty(ref _selectedCollection, value))
            {
                ApplyCollectionMetadata(value?.Collection);
                NotifyCanSearchChanged();
            }
        }
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

    public bool IsLoadingCollections
    {
        get => _isLoadingCollections;
        set
        {
            SetProperty(ref _isLoadingCollections, value);
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
    public ICommand ClearAoiCommand { get; }

    #endregion

    #region Public Methods

    public async Task LoadCatalogsAsync()
    {
        Catalogs.Clear();
        foreach (var root in _stacClient.CatalogRoots)
            Catalogs.Add(root);

        if (Catalogs.Count > 0)
            SelectedCatalog = Catalogs[0];

        await Task.CompletedTask;
    }

    /// <summary>
    /// Called by DrawAoiTool when the user finishes drawing a polygon.
    /// </summary>
    internal void SetAoiFromPolygon(Envelope envelope)
    {
        _aoiBbox = [envelope.XMin, envelope.YMin, envelope.XMax, envelope.YMax];
        _aoiFromCollection = false;

        // Set observable properties directly — safe from any thread for test assertions
        // and WPF binding engine picks up PropertyChanged regardless of source thread.
        AoiDescription = $"Drawn AOI: {envelope.XMin:F2}, {envelope.YMin:F2} to {envelope.XMax:F2}, {envelope.YMax:F2}";

        NotifyAoiCommandsChanged();

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

        SetImportedAoi(bbox);
    }

    #endregion

    #region Private Methods

    private async Task LoadCollectionsAsync()
    {
        if (SelectedCatalog == null) return;

        var selectedRoot = SelectedCatalog;
        IsLoadingCollections = true;
        ResultSummary = $"Loading {selectedRoot.DisplayName} collections...";
        Collections.Clear();
        SelectedCollection = null;

        try
        {
            var collections = await _stacClient.DiscoverCollectionsAsync(selectedRoot);
            if (SelectedCatalog != selectedRoot)
                return;

            foreach (var col in collections)
                Collections.Add(col);
            if (Collections.Count > 0)
                SelectedCollection = Collections[0];
            ResultSummary = collections.Count == 0
                ? $"No collections are available under {selectedRoot.DisplayName}."
                : $"Loaded {collections.Count} {selectedRoot.DisplayName} collections.";
        }
        catch (Exception ex)
        {
            if (SelectedCatalog == selectedRoot)
                ResultSummary = $"Failed to load {selectedRoot.DisplayName} collections: {ex.Message}";
        }
        finally
        {
            if (SelectedCatalog == selectedRoot)
                IsLoadingCollections = false;
        }
    }

    private bool CanSearch() =>
        !IsSearching && !IsLoadingCollections && _aoiBbox != null && SelectedCollection != null;

    private bool CanClearAoi() => _aoiBbox != null;

    private void ExecuteClearAoi()
    {
        _aoiOverlay?.Dispose();
        _aoiOverlay = null;
        _aoiBbox = null;
        _aoiFromCollection = false;
        AoiDescription = "No area selected";
        NotifyAoiCommandsChanged();
    }

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

    private void NotifyAoiCommandsChanged()
    {
        NotifyCanSearchChanged();
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.BeginInvoke(() => ((RelayCommand)ClearAoiCommand).RaiseCanExecuteChanged());
        else
            ((RelayCommand)ClearAoiCommand).RaiseCanExecuteChanged();
    }

    private async void ExecuteSearch()
    {
        if (SelectedCollection == null || _aoiBbox == null) return;

        IsSearching = true;
        ResultSummary = "Searching...";

        try
        {
            var filters = new SearchFilters
            {
                Bbox = _aoiBbox,
                StartDate = StartDate.HasValue ? new DateTimeOffset(StartDate.Value) : null,
                EndDate = EndDate.HasValue ? new DateTimeOffset(EndDate.Value) : null,
                Collections = [SelectedCollection.Collection.Id],
                MaxCloudCover = MaxCloudCover < 100 ? MaxCloudCover : null,
                Limit = 50
            };

            CurrentFilters = filters;
            var result = await _stacClient.SearchAsync(SelectedCollection, filters);

            ResultSummary = $"Found {result.TotalCount} items ({result.Items.Count} shown).";
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
            Filter = "Supported AOI files (*.geojson;*.json;*.shp;*.gpkg)|*.geojson;*.json;*.shp;*.gpkg|" +
                     "GeoJSON files (*.geojson;*.json)|*.geojson;*.json|" +
                     "Shapefiles (*.shp)|*.shp|GeoPackages (*.gpkg)|*.gpkg"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var extension = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            if (extension is ".geojson" or ".json")
            {
                var json = await File.ReadAllTextAsync(dlg.FileName);
                SetAoiFromGeoJson(json);
            }
            else
            {
                var bbox = await ReadVectorAoiExtentAsync(dlg.FileName);
                SetImportedAoi(bbox, Path.GetFileName(dlg.FileName));
            }
        }
        catch (Exception ex)
        {
            ResultSummary = $"Failed to import AOI: {ex.Message}";
        }
    }

    private void SetImportedAoi(double[] bbox, string? sourceName = null)
    {
        _aoiBbox = bbox;
        _aoiFromCollection = false;
        var source = string.IsNullOrWhiteSpace(sourceName) ? string.Empty : $" ({sourceName})";
        AoiDescription = $"Imported AOI{source}: {bbox[0]:F2}, {bbox[1]:F2} to {bbox[2]:F2}, {bbox[3]:F2}";
        NotifyAoiCommandsChanged();
        _ = ShowAoiOnMap();
    }

    private static Task<double[]> ReadVectorAoiExtentAsync(string path) =>
        QueuedTask.Run(() =>
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension switch
            {
                ".shp" => ReadShapefileExtent(path),
                ".gpkg" => ReadGeoPackageExtent(path),
                _ => throw new NotSupportedException($"Unsupported AOI file type: {extension}")
            };
        });

    private static double[] ReadShapefileExtent(string path)
    {
        var folder = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("The shapefile folder could not be determined.");
        var connection = new ArcData.FileSystemConnectionPath(
            new Uri(folder), ArcData.FileSystemDatastoreType.Shapefile);
        using var dataStore = new ArcData.FileSystemDatastore(connection);
        using var featureClass = dataStore.OpenDataset<ArcData.FeatureClass>(Path.GetFileName(path));
        return ProjectExtentToWgs84(featureClass.GetExtent());
    }

    private static double[] ReadGeoPackageExtent(string path)
    {
        using var database = new ArcData.Database(new ArcData.SQLiteConnectionPath(new Uri(path)));
        double[]? combined = null;

        foreach (var tableName in database.GetTableNames())
        {
            using var description = database.GetQueryDescription(tableName);
            if (!description.IsSpatialQuery())
                continue;

            using var table = database.OpenTable(description);
            if (table is not ArcData.FeatureClass featureClass)
                continue;

            database.CalculateExtent(featureClass);
            combined = CombineExtents(combined, ProjectExtentToWgs84(featureClass.GetExtent()));
        }

        return combined ?? throw new InvalidDataException(
            "The GeoPackage does not contain a spatial feature layer.");
    }

    private static double[] ProjectExtentToWgs84(Envelope extent)
    {
        var wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326);
        var projected = GeometryEngine.Instance.Project(extent, wgs84) as Envelope
            ?? throw new InvalidDataException("The AOI extent could not be projected to WGS84.");
        return [projected.XMin, projected.YMin, projected.XMax, projected.YMax];
    }

    private static double[] CombineExtents(double[]? current, double[] next) => current == null
        ? next
        :
        [
            Math.Min(current[0], next[0]), Math.Min(current[1], next[1]),
            Math.Max(current[2], next[2]), Math.Max(current[3], next[3])
        ];

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
                _aoiFromCollection = false;
                AoiDescription = $"Map extent: {bbox[0]:F2}, {bbox[1]:F2} to {bbox[2]:F2}, {bbox[3]:F2}";
                NotifyAoiCommandsChanged();
                _ = ShowAoiOnMap();
            }
        }
        catch (Exception ex)
        {
            ResultSummary = $"Failed to get map extent: {ex.Message}";
        }
    }

    private void ApplyCollectionMetadata(StacCollection? collection)
    {
        var today = DateTime.Today;
        var fallbackStart = today.AddMonths(-1);
        var intervals = collection?.Extent?.Temporal?.Interval;
        var starts = intervals?
            .Select(interval => ParseDate(interval.ElementAtOrDefault(0)))
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .ToList() ?? [];
        var ends = intervals?
            .Select(interval => ParseDate(interval.ElementAtOrDefault(1)))
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .ToList() ?? [];

        StartDate = starts.Count > 0 ? starts.Min() : fallbackStart;
        EndDate = ends.Count > 0 ? ends.Max() : today;

        var collectionBbox = CombineCollectionBboxes(collection?.Extent?.Spatial?.Bbox);
        if (collectionBbox != null)
        {
            _aoiBbox = collectionBbox;
            _aoiFromCollection = true;
            AoiDescription =
                $"Collection extent: {collectionBbox[0]:F2}, {collectionBbox[1]:F2} to " +
                $"{collectionBbox[2]:F2}, {collectionBbox[3]:F2}";
            NotifyAoiCommandsChanged();
            _ = ShowAoiOnMap();
        }
        else if (_aoiFromCollection)
        {
            _aoiOverlay?.Dispose();
            _aoiOverlay = null;
            _aoiBbox = null;
            _aoiFromCollection = false;
            AoiDescription = "No area selected";
            NotifyAoiCommandsChanged();
        }
    }

    private static DateTime? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed.Date : null;

    private static double[]? CombineCollectionBboxes(List<List<double>>? bboxes)
    {
        double[]? combined = null;
        foreach (var bbox in bboxes ?? [])
        {
            double[]? extent = bbox.Count switch
            {
                >= 6 => [bbox[0], bbox[1], bbox[3], bbox[4]],
                >= 4 => [bbox[0], bbox[1], bbox[2], bbox[3]],
                _ => null
            };
            if (extent == null || extent.Any(value => !double.IsFinite(value)) ||
                extent[0] > extent[2] || extent[1] > extent[3])
                continue;

            combined = CombineExtents(combined, extent);
        }

        return combined;
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

            // Selected AOI: thick blue border with a 5% opacity fill.
            var outline = SymbolFactory.Instance.ConstructStroke(
                ColorFactory.Instance.CreateRGBColor(0, 90, 200), 3, SimpleLineStyle.Solid);
            var fill = SymbolFactory.Instance.ConstructPolygonSymbol(
                ColorFactory.Instance.CreateRGBColor(0, 120, 255, 13),
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

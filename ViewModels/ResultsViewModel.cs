using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using eodh.Models;
using eodh.Services;
using eodh.Tools;

namespace eodh.ViewModels;

/// <summary>
/// ViewModel for the search results display.
/// Shows results with thumbnails, metadata, and double-click-to-load.
/// </summary>
internal class ResultsViewModel : PropertyChangedBase
{
    private readonly StacClient _stacClient;
    private readonly LayerService _layerService;
    private readonly ThumbnailCache _thumbnailCache;
    private readonly CommercialOrderService? _commercialOrderService;
    private readonly FootprintOverlayService _footprintOverlayService;

    private ResultItemViewModel? _selectedItem;
    private bool _isLoading;
    private bool _hasResults;
    private string _statusMessage = string.Empty;
    private string _footprintStatus = string.Empty;
    private bool _showFootprints = true;

    public ResultsViewModel(StacClient stacClient, LayerService layerService,
        ThumbnailCache thumbnailCache, CommercialOrderService? commercialOrderService = null,
        FootprintOverlayService? footprintOverlayService = null)
    {
        _stacClient = stacClient;
        _layerService = layerService;
        _thumbnailCache = thumbnailCache;
        _commercialOrderService = commercialOrderService;
        _footprintOverlayService = footprintOverlayService ?? new FootprintOverlayService();

        LoadSelectedCommand = new RelayCommand(ExecuteLoadSelected, () => SelectedItem != null);
    }

    #region Properties

    public ObservableCollection<ResultItemViewModel> Results { get; } = [];

    public ResultItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                ((RelayCommand)LoadSelectedCommand).RaiseCanExecuteChanged();
                _ = _footprintOverlayService.HighlightAsync(value?.Item.Id);
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool HasResults
    {
        get => _hasResults;
        set => SetProperty(ref _hasResults, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string FootprintStatus
    {
        get => _footprintStatus;
        set => SetProperty(ref _footprintStatus, value);
    }

    public bool ShowFootprints
    {
        get => _showFootprints;
        set
        {
            if (SetProperty(ref _showFootprints, value))
                _ = RenderFootprintsAsync();
        }
    }

    public ICommand LoadSelectedCommand { get; }
    #endregion

    #region Public Methods

    public void LoadResults(List<StacItem> items, SearchFilters? filters, string? collectionLicense = null)
    {
        SelectedItem = null;
        Results.Clear();

        var filtered = 0;
        foreach (var item in items)
        {
            if (filters?.Bbox != null)
            {
                var overlap = BboxMath.CalculateOverlapPercent(item.Bbox, filters.Bbox);
                if (overlap is 0.0)
                {
                    filtered++;
                    continue;
                }
            }

            var vm = new ResultItemViewModel(
                item, _thumbnailCache, _layerService,
                filters?.Bbox, collectionLicense, _commercialOrderService);
            Results.Add(vm);
        }

        StatusMessage = filtered > 0
            ? $"{Results.Count} results ({filtered} outside AOI excluded)"
            : $"{items.Count} results";

        HasResults = Results.Count > 0;
        _ = RenderFootprintsAsync();
    }

    public void SelectByItemId(string itemId)
    {
        SelectedItem = Results.FirstOrDefault(r => r.Item.Id == itemId);
    }

    public void ClearResults()
    {
        SelectedItem = null;
        Results.Clear();
        HasResults = false;
        StatusMessage = string.Empty;
        FootprintStatus = string.Empty;
        _ = _footprintOverlayService.ClearAsync();
    }

    #endregion

    #region Commands

    private async void ExecuteLoadSelected()
    {
        if (SelectedItem == null) return;
        await SelectedItem.LoadIntoMapAsync();
    }

    private async Task RenderFootprintsAsync()
    {
        try
        {
            var result = await _footprintOverlayService.ReplaceAsync(
                Results.Select(result => result.Item),
                SelectedItem?.Item.Id,
                ShowFootprints);

            FootprintStatus = !ShowFootprints || Results.Count == 0
                ? string.Empty
                : result.SkippedCount > 0
                    ? $"Footprints: {result.RenderedCount} shown, {result.SkippedCount} unavailable"
                    : $"Footprints: {result.RenderedCount} shown";
        }
        catch (Exception)
        {
            FootprintStatus = "Footprints could not be displayed on the active map.";
        }
    }

    #endregion
}

/// <summary>
/// ViewModel for a single search result item.
/// </summary>
internal class ResultItemViewModel : PropertyChangedBase
{
    private readonly ThumbnailCache _thumbnailCache;
    private readonly LayerService _layerService;
    private readonly CommercialOrderService? _commercialOrderService;
    private readonly double[]? _aoiBbox;
    private BitmapImage? _thumbnail;
    private bool _isLoadingLayer;

    // Asset popup state
    private bool _isAssetsPopupOpen;

    // Purchase state
    private bool _isQuoting;
    private bool _isOrdering;
    private QuoteResponse? _currentQuote;
    private CommercialQuoteContext? _quotedContext;
    private string? _selectedLicence;
    private string? _endUserCountry;
    private string? _selectedProductBundle;
    private string? _selectedOrbit;
    private string? _selectedResolutionVariant;
    private string? _selectedProjection;
    private string? _purchaseStatus;
    private string? _purchaseError;

    public ResultItemViewModel(StacItem item, ThumbnailCache thumbnailCache,
        LayerService layerService, double[]? aoiBbox = null,
        string? collectionLicense = null, CommercialOrderService? commercialOrderService = null)
    {
        Item = item;
        _thumbnailCache = thumbnailCache;
        _layerService = layerService;
        _commercialOrderService = commercialOrderService;
        _aoiBbox = aoiBbox;

        AoiOverlap = ResultFormat.FormatOverlap(BboxMath.CalculateOverlapPercent(item.Bbox, aoiBbox));
        LicenseInfo = ResultFormat.FormatLicense(collectionLicense);

        // Commercial data detection
        IsCommercial = CommercialHelper.IsCommercialItem(item);
        Provider = CommercialHelper.DetectProvider(item);
        Capabilities = CommercialHelper.GetCapabilities(Provider);
        LicenceOptions = Capabilities.LicenceOptions;
        ProductBundleOptions = Capabilities.ProductBundles;
        OrbitOptions = Capabilities.OrbitOptions;
        ResolutionVariantOptions = Capabilities.ResolutionVariantOptions;
        ProjectionOptions = Capabilities.ProjectionOptions;

        if (IsCommercial)
        {
            _selectedLicence = LicenceOptions.FirstOrDefault();
            _endUserCountry = Capabilities.RequiresEndUserCountry ? "GB" : null;
            _selectedProductBundle = ProductBundleOptions.FirstOrDefault();
            _selectedOrbit = OrbitOptions.FirstOrDefault();
            _selectedResolutionVariant = ResolutionVariantOptions.FirstOrDefault();
            _selectedProjection = ProjectionOptions.FirstOrDefault();
        }

        // Build asset detail list
        AllAssets = (Item.Assets ?? new Dictionary<string, StacAsset>())
            .Select(kv => new AssetDetailViewModel
            {
                Key = kv.Key,
                DisplayName = kv.Value.Title ?? kv.Key,
                FileType = kv.Value.FileType,
                IsLoadable = kv.Value.IsLoadable,
                IsSelected = kv.Value.IsLoadable
            })
            .ToList();

        LoadCommand = new RelayCommand(() => _ = LoadIntoMapAsync());
        ToggleAssetsPopupCommand = new RelayCommand(() => IsAssetsPopupOpen = !IsAssetsPopupOpen);
        GetQuoteCommand = new RelayCommand(ExecuteGetQuote, CanGetQuote);
        PlaceOrderCommand = new RelayCommand(ExecutePlaceOrder, CanPlaceOrder);

        _ = LoadThumbnailAsync();
    }

    #region Properties

    public StacItem Item { get; }

    public string ItemId => Item.Id;
    public string? Collection => Item.Collection;
    public string? AcquisitionDate => Item.Properties?.ParsedDateTime?.ToString("yyyy-MM-dd HH:mm");
    public string? Resolution => Item.Properties?.Gsd != null ? $"{Item.Properties.Gsd:F1} m" : null;
    public string? CloudCover => Item.Properties?.CloudCover != null ? $"{Item.Properties.CloudCover:F1}%" : null;
    public string AssetCount
    {
        get
        {
            var loadable = AssetSelector.GetLoadableAssets(Item.Assets).Count;
            var total = Item.Assets?.Count ?? 0;
            return $"{loadable} loadable / {total} total assets";
        }
    }
    public List<AssetDetailViewModel> AllAssets { get; }
    public string? AoiOverlap { get; }
    public string? LocationalAccuracy => ResultFormat.FormatAccuracy(Item.Properties?.GeometricRmse);
    public string? LicenseInfo { get; }

    public BitmapImage? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    public bool IsLoadingLayer
    {
        get => _isLoadingLayer;
        set => SetProperty(ref _isLoadingLayer, value);
    }

    public ICommand LoadCommand { get; }

    // Asset popup
    public bool IsAssetsPopupOpen
    {
        get => _isAssetsPopupOpen;
        set => SetProperty(ref _isAssetsPopupOpen, value);
    }

    public ICommand ToggleAssetsPopupCommand { get; }

    // Purchase properties
    public bool IsCommercial { get; }
    public CommercialProvider Provider { get; }
    public CommercialProviderCapabilities Capabilities { get; }
    public IReadOnlyList<string> LicenceOptions { get; }
    public IReadOnlyList<string> ProductBundleOptions { get; }
    public IReadOnlyList<string> OrbitOptions { get; }
    public IReadOnlyList<string> ResolutionVariantOptions { get; }
    public IReadOnlyList<string> ProjectionOptions { get; }
    public bool HasLicenceOptions => LicenceOptions.Count > 0;
    public bool HasProductBundleOptions => ProductBundleOptions.Count > 0;
    public bool RequiresEndUserCountry => Capabilities.RequiresEndUserCountry;
    public bool HasRadarOptions => Capabilities.HasRadarOptions;
    public bool RequiresResolutionVariant =>
        Capabilities.RequiresResolutionVariant(SelectedProductBundle);
    public bool RequiresProjection =>
        Capabilities.RequiresProjection(SelectedProductBundle);

    public string? SelectedLicence
    {
        get => _selectedLicence;
        set => SetCommercialInput(ref _selectedLicence, value);
    }

    public string? EndUserCountry
    {
        get => _endUserCountry;
        set => SetCommercialInput(ref _endUserCountry, value);
    }

    public string? SelectedProductBundle
    {
        get => _selectedProductBundle;
        set
        {
            if (SetCommercialInput(ref _selectedProductBundle, value))
            {
                NotifyPropertyChanged(nameof(RequiresResolutionVariant));
                NotifyPropertyChanged(nameof(RequiresProjection));
            }
        }
    }

    public string? SelectedOrbit
    {
        get => _selectedOrbit;
        set => SetCommercialInput(ref _selectedOrbit, value);
    }

    public string? SelectedResolutionVariant
    {
        get => _selectedResolutionVariant;
        set => SetCommercialInput(ref _selectedResolutionVariant, value);
    }

    public string? SelectedProjection
    {
        get => _selectedProjection;
        set => SetCommercialInput(ref _selectedProjection, value);
    }

    public bool IsQuoting
    {
        get => _isQuoting;
        set
        {
            SetProperty(ref _isQuoting, value);
            RaisePurchaseCanExecuteChanged();
        }
    }

    public bool IsOrdering
    {
        get => _isOrdering;
        set
        {
            SetProperty(ref _isOrdering, value);
            RaisePurchaseCanExecuteChanged();
        }
    }

    public QuoteResponse? CurrentQuote
    {
        get => _currentQuote;
        set
        {
            SetProperty(ref _currentQuote, value);
            NotifyPropertyChanged(nameof(QuoteDisplay));
            NotifyPropertyChanged(nameof(HasQuote));
            NotifyPropertyChanged(nameof(QuoteMessage));
            RaisePurchaseCanExecuteChanged();
        }
    }

    public bool HasQuote => _currentQuote != null;

    public string? QuoteDisplay => _currentQuote != null
        ? $"{_currentQuote.Value:N2} {_currentQuote.Units}"
        : null;

    public string? QuoteMessage => _currentQuote?.Message;

    public string? PurchaseStatus
    {
        get => _purchaseStatus;
        set => SetProperty(ref _purchaseStatus, value);
    }

    public string? PurchaseError
    {
        get => _purchaseError;
        set => SetProperty(ref _purchaseError, value);
    }

    public ICommand GetQuoteCommand { get; }
    public ICommand PlaceOrderCommand { get; }

    #endregion

    #region Public Methods

    public async Task LoadIntoMapAsync()
    {
        if (Item.Assets == null) return;

        IsLoadingLayer = true;
        try
        {
            await _layerService.SetMapToOsgbAsync();
            var selected = AllAssets
                .Where(a => a.IsSelected)
                .Select(a => (a.Key, Asset: Item.Assets[a.Key]))
                .ToList();

            foreach (var (key, asset) in selected)
            {
                try
                {
                    await _layerService.LoadAssetAsync(Item, asset, key);
                }
                catch (Exception ex)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        $"Failed to load asset '{key}' ({asset.FileType}).\n\n" +
                        $"Href: {asset.Href}\n\n" +
                        $"Error: {ex.Message}",
                        "EODH — Asset Load Error");
                }
            }
        }
        finally
        {
            IsLoadingLayer = false;
        }
    }

    #endregion

    #region Private Methods

    private bool CanGetQuote() =>
        IsCommercial && !_isQuoting && !_isOrdering && Item.SelfLink != null &&
        _commercialOrderService != null && HasValidCommercialInputs();

    private bool CanPlaceOrder() =>
        CanGetQuote() && _currentQuote != null &&
        _quotedContext == CreateQuoteContext();

    private async void ExecuteGetQuote()
    {
        if (Item.SelfLink == null || _commercialOrderService == null || !HasValidCommercialInputs())
            return;

        IsQuoting = true;
        PurchaseError = null;
        try
        {
            var coordinates = Capabilities.SupportsCoordinates
                ? CommercialHelper.BboxToCoordinateRing(_aoiBbox)
                : null;
            var licence = Capabilities.RequiresLicence ? _selectedLicence : null;
            var request = new QuoteRequest(coordinates, licence, _selectedProductBundle);
            var quote = await _commercialOrderService.GetQuoteAsync(Item.SelfLink, request);
            _quotedContext = CreateQuoteContext();
            CurrentQuote = quote;
        }
        catch (Exception ex)
        {
            InvalidateQuote();
            PurchaseError = ex.Message;
        }
        finally
        {
            IsQuoting = false;
        }
    }

    private async void ExecutePlaceOrder()
    {
        if (Item.SelfLink == null || _commercialOrderService == null || !CanPlaceOrder())
            return;

        var confirm = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
            $"You are about to order this item for {QuoteDisplay}.\n\n" +
            "This action is irreversible. Do you want to proceed?",
            "EODH — Confirm Order",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        IsOrdering = true;
        PurchaseError = null;
        try
        {
            var coordinates = Capabilities.SupportsCoordinates
                ? CommercialHelper.BboxToCoordinateRing(_aoiBbox)
                : null;
            var licence = Capabilities.RequiresLicence ? _selectedLicence : null;
            var endUserCountry = Capabilities.RequiresEndUserCountry ? _endUserCountry : null;
            var request = new OrderRequest(
                Capabilities.RequiresProductBundle ? _selectedProductBundle : null,
                coordinates,
                endUserCountry,
                licence,
                CreateRadarOptions());
            var result = await _commercialOrderService.PlaceOrderAsync(Item.SelfLink, request);

            if (result.Success)
                PurchaseStatus = "Ordered — check workspace for delivery status";
            else
                PurchaseError = result.ErrorMessage ?? "Order failed.";
        }
        catch (Exception ex)
        {
            PurchaseError = ex.Message;
        }
        finally
        {
            IsOrdering = false;
        }
    }

    private bool HasValidCommercialInputs()
    {
        if (Provider == CommercialProvider.Unknown)
            return false;

        if (Capabilities.RequiresProductBundle &&
            (string.IsNullOrWhiteSpace(_selectedProductBundle) ||
             !ProductBundleOptions.Contains(_selectedProductBundle)))
            return false;

        if (Capabilities.RequiresLicence &&
            (string.IsNullOrWhiteSpace(_selectedLicence) || !LicenceOptions.Contains(_selectedLicence)))
            return false;

        if (Capabilities.RequiresEndUserCountry && string.IsNullOrWhiteSpace(_endUserCountry))
            return false;

        if (Capabilities.HasRadarOptions &&
            (string.IsNullOrWhiteSpace(_selectedOrbit) || !OrbitOptions.Contains(_selectedOrbit)))
            return false;

        if (RequiresResolutionVariant &&
            (string.IsNullOrWhiteSpace(_selectedResolutionVariant) ||
             !ResolutionVariantOptions.Contains(_selectedResolutionVariant)))
            return false;

        return !RequiresProjection ||
            (!string.IsNullOrWhiteSpace(_selectedProjection) &&
             ProjectionOptions.Contains(_selectedProjection));
    }

    private RadarOptions? CreateRadarOptions()
    {
        if (!Capabilities.HasRadarOptions)
            return null;

        return new RadarOptions(
            _selectedOrbit!,
            RequiresResolutionVariant ? _selectedResolutionVariant : null,
            RequiresProjection ? _selectedProjection : null);
    }

    private CommercialQuoteContext CreateQuoteContext() => new(
        Item.Id,
        _aoiBbox == null ? null : string.Join(",", _aoiBbox.Select(value => value.ToString("R"))),
        Provider,
        Capabilities.RequiresLicence ? _selectedLicence : null,
        _selectedProductBundle,
        Capabilities.RequiresEndUserCountry ? _endUserCountry : null,
        Capabilities.HasRadarOptions ? _selectedOrbit : null,
        RequiresResolutionVariant ? _selectedResolutionVariant : null,
        RequiresProjection ? _selectedProjection : null);

    private bool SetCommercialInput(ref string? field, string? value)
    {
        if (!SetProperty(ref field, value))
            return false;

        InvalidateQuote();
        RaisePurchaseCanExecuteChanged();
        return true;
    }

    private void InvalidateQuote()
    {
        _quotedContext = null;
        CurrentQuote = null;
        PurchaseStatus = null;
    }

    private void RaisePurchaseCanExecuteChanged()
    {
        ((RelayCommand)GetQuoteCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PlaceOrderCommand).RaiseCanExecuteChanged();
    }

    private async Task LoadThumbnailAsync()
    {
        if (Item.Assets == null) return;

        var thumbnailAsset = Item.Assets.Values
            .FirstOrDefault(a => a.IsThumbnail);

        if (thumbnailAsset != null)
        {
            Thumbnail = await _thumbnailCache.GetThumbnailAsync(thumbnailAsset.Href);
        }
    }

    #endregion
}

internal sealed record CommercialQuoteContext(
    string ItemId,
    string? Coordinates,
    CommercialProvider Provider,
    string? Licence,
    string? ProductBundle,
    string? EndUserCountry,
    string? Orbit,
    string? ResolutionVariant,
    string? Projection);

/// <summary>
/// Detail view model for a single asset in the expandable list.
/// </summary>
internal class AssetDetailViewModel : PropertyChangedBase
{
    private bool _isSelected;

    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string FileType { get; init; }
    public required bool IsLoadable { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

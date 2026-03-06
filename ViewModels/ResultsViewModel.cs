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
    private readonly WorkspaceService? _workspaceService;

    private ResultItemViewModel? _selectedItem;
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public ResultsViewModel(StacClient stacClient, LayerService layerService,
        ThumbnailCache thumbnailCache, WorkspaceService? workspaceService = null)
    {
        _stacClient = stacClient;
        _layerService = layerService;
        _thumbnailCache = thumbnailCache;
        _workspaceService = workspaceService;

        LoadSelectedCommand = new RelayCommand(ExecuteLoadSelected, () => SelectedItem != null);
    }

    #region Properties

    public ObservableCollection<ResultItemViewModel> Results { get; } = [];

    public ResultItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            SetProperty(ref _selectedItem, value);
            ((RelayCommand)LoadSelectedCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand LoadSelectedCommand { get; }
    #endregion

    #region Public Methods

    public void LoadResults(List<StacItem> items, SearchFilters? filters, string? collectionLicense = null)
    {
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
                filters?.Bbox, collectionLicense, _workspaceService);
            Results.Add(vm);
        }

        StatusMessage = filtered > 0
            ? $"{Results.Count} results ({filtered} outside AOI excluded)"
            : $"{items.Count} results";
    }

    public void SelectByItemId(string itemId)
    {
        SelectedItem = Results.FirstOrDefault(r => r.Item.Id == itemId);
    }

    #endregion

    #region Commands

    private async void ExecuteLoadSelected()
    {
        if (SelectedItem == null) return;
        await SelectedItem.LoadIntoMapAsync();
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
    private readonly WorkspaceService? _workspaceService;
    private readonly double[]? _aoiBbox;
    private BitmapImage? _thumbnail;
    private bool _isLoadingLayer;

    // Asset popup state
    private bool _isAssetsPopupOpen;

    // Purchase state
    private bool _isQuoting;
    private bool _isOrdering;
    private QuoteResponse? _currentQuote;
    private string? _selectedLicence;
    private string? _endUserCountry;
    private string? _selectedProductBundle;
    private string? _purchaseStatus;
    private string? _purchaseError;

    public ResultItemViewModel(StacItem item, ThumbnailCache thumbnailCache,
        LayerService layerService, double[]? aoiBbox = null,
        string? collectionLicense = null, WorkspaceService? workspaceService = null)
    {
        Item = item;
        _thumbnailCache = thumbnailCache;
        _layerService = layerService;
        _workspaceService = workspaceService;
        _aoiBbox = aoiBbox;

        AoiOverlap = ResultFormat.FormatOverlap(BboxMath.CalculateOverlapPercent(item.Bbox, aoiBbox));
        LicenseInfo = ResultFormat.FormatLicense(collectionLicense);

        // Commercial data detection
        IsCommercial = CommercialHelper.IsCommercialItem(item);
        Provider = CommercialHelper.DetectProvider(item);

        if (IsCommercial)
        {
            var licences = CommercialHelper.GetLicenceOptions(Provider);
            LicenceOptions = licences.ToList();
            _selectedLicence = licences.FirstOrDefault();
            _endUserCountry = "GB";
            _selectedProductBundle = "General Use";
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
    public List<string>? LicenceOptions { get; }
    public bool RequiresEndUserCountry => CommercialHelper.RequiresEndUserCountry(Provider);

    public string? SelectedLicence
    {
        get => _selectedLicence;
        set => SetProperty(ref _selectedLicence, value);
    }

    public string? EndUserCountry
    {
        get => _endUserCountry;
        set => SetProperty(ref _endUserCountry, value);
    }

    public string? SelectedProductBundle
    {
        get => _selectedProductBundle;
        set => SetProperty(ref _selectedProductBundle, value);
    }

    public bool IsQuoting
    {
        get => _isQuoting;
        set => SetProperty(ref _isQuoting, value);
    }

    public bool IsOrdering
    {
        get => _isOrdering;
        set => SetProperty(ref _isOrdering, value);
    }

    public QuoteResponse? CurrentQuote
    {
        get => _currentQuote;
        set
        {
            SetProperty(ref _currentQuote, value);
            NotifyPropertyChanged(nameof(QuoteDisplay));
            NotifyPropertyChanged(nameof(HasQuote));
            ((RelayCommand)PlaceOrderCommand).RaiseCanExecuteChanged();
        }
    }

    public bool HasQuote => _currentQuote != null;

    public string? QuoteDisplay => _currentQuote != null
        ? $"{_currentQuote.Price:N2} {_currentQuote.Currency}"
        : null;

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
        IsCommercial && !_isQuoting && Item.SelfLink != null && _workspaceService != null;

    private bool CanPlaceOrder() =>
        IsCommercial && !_isOrdering && _currentQuote != null && Item.SelfLink != null && _workspaceService != null;

    private async void ExecuteGetQuote()
    {
        if (Item.SelfLink == null || _workspaceService == null) return;

        IsQuoting = true;
        PurchaseError = null;
        try
        {
            var coordinates = CommercialHelper.SupportsCoordinates(Provider)
                ? CommercialHelper.BboxToCoordinateRing(_aoiBbox)
                : null;

            var licence = CommercialHelper.RequiresLicence(Provider) ? _selectedLicence : null;

            var request = new QuoteRequest(licence, coordinates);
            CurrentQuote = await _workspaceService.GetQuoteAsync(Item.SelfLink, request);
        }
        catch (Exception ex)
        {
            PurchaseError = $"Quote error: {ex.Message}";
        }
        finally
        {
            IsQuoting = false;
        }
    }

    private async void ExecutePlaceOrder()
    {
        if (Item.SelfLink == null || _workspaceService == null) return;

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
            var coordinates = CommercialHelper.SupportsCoordinates(Provider)
                ? CommercialHelper.BboxToCoordinateRing(_aoiBbox)
                : null;

            var licence = CommercialHelper.RequiresLicence(Provider) ? _selectedLicence : null;
            var endUserCountry = CommercialHelper.RequiresEndUserCountry(Provider) ? _endUserCountry : null;

            var request = new OrderRequest(licence, endUserCountry, _selectedProductBundle, coordinates);
            var result = await _workspaceService.PlaceOrderAsync(Item.SelfLink, request);

            if (result.Success)
                PurchaseStatus = "Ordered — check workspace for delivery status";
            else
                PurchaseError = result.ErrorMessage ?? "Order failed.";
        }
        catch (Exception ex)
        {
            PurchaseError = $"Order error: {ex.Message}";
        }
        finally
        {
            IsOrdering = false;
        }
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

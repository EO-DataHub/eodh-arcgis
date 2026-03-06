using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using eodh.Models;
using eodh.Services;

namespace eodh.ViewModels;

/// <summary>
/// ViewModel for the timeline strip — a scrollable date selector
/// showing acquisitions in temporal order with thumbnails.
/// </summary>
internal class TimelineViewModel : PropertyChangedBase
{
    private readonly StacClient _stacClient;
    private readonly LayerService _layerService;
    private readonly ThumbnailCache _thumbnailCache;
    private int _selectedIndex = -1;
    private Action<StacItem>? _onItemSelected;

    public TimelineViewModel(StacClient stacClient, LayerService layerService, ThumbnailCache thumbnailCache)
    {
        _stacClient = stacClient;
        _layerService = layerService;
        _thumbnailCache = thumbnailCache;

        PreviousCommand = new RelayCommand(ExecutePrevious, () => _selectedIndex > 0);
        NextCommand = new RelayCommand(ExecuteNext, () => _selectedIndex < TimelineEntries.Count - 1);
    }

    #region Properties

    public ObservableCollection<TimelineEntryViewModel> TimelineEntries { get; } = [];

    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }

    public event Action<int>? ScrollRequested;

    #endregion

    #region Public Methods

    public void SetSelectionCallback(Action<StacItem> callback)
    {
        _onItemSelected = callback;
    }

    public void SelectByItemId(string itemId)
    {
        for (var i = 0; i < TimelineEntries.Count; i++)
        {
            if (TimelineEntries[i].Item.Id == itemId)
            {
                SelectEntry(i);
                return;
            }
        }
    }

    public void LoadResults(List<StacItem> items)
    {
        TimelineEntries.Clear();
        _selectedIndex = -1;

        var sorted = items
            .Where(i => i.Properties?.ParsedDateTime != null)
            .OrderBy(i => i.Properties!.ParsedDateTime)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var entry = new TimelineEntryViewModel(sorted[i], _thumbnailCache, _layerService, i, SelectEntry);
            TimelineEntries.Add(entry);
        }

        if (TimelineEntries.Count > 0)
            SelectEntry(TimelineEntries.Count - 1);
    }

    #endregion

    #region Private Methods

    private void SelectEntry(int index)
    {
        if (index < 0 || index >= TimelineEntries.Count) return;

        if (_selectedIndex >= 0 && _selectedIndex < TimelineEntries.Count)
            TimelineEntries[_selectedIndex].IsSelected = false;

        _selectedIndex = index;
        TimelineEntries[_selectedIndex].IsSelected = true;

        ((RelayCommand)PreviousCommand).RaiseCanExecuteChanged();
        ((RelayCommand)NextCommand).RaiseCanExecuteChanged();

        _onItemSelected?.Invoke(TimelineEntries[_selectedIndex].Item);
        ScrollRequested?.Invoke(_selectedIndex);
    }

    private void ExecutePrevious() => SelectEntry(_selectedIndex - 1);
    private void ExecuteNext() => SelectEntry(_selectedIndex + 1);

    #endregion
}

/// <summary>
/// ViewModel for a single entry in the timeline strip.
/// </summary>
internal class TimelineEntryViewModel : PropertyChangedBase
{
    private readonly ThumbnailCache _thumbnailCache;
    private readonly LayerService _layerService;
    private readonly Action<int> _onSelect;
    private readonly int _index;
    private BitmapImage? _thumbnail;
    private bool _isSelected;

    public TimelineEntryViewModel(
        StacItem item, ThumbnailCache thumbnailCache, LayerService layerService,
        int index, Action<int> onSelect)
    {
        Item = item;
        _thumbnailCache = thumbnailCache;
        _layerService = layerService;
        _index = index;
        _onSelect = onSelect;

        SelectCommand = new RelayCommand(() => _onSelect(_index));

        _ = LoadThumbnailAsync();
    }

    public StacItem Item { get; }

    public string DateLabel => Item.Properties?.ParsedDateTime?.ToString("MM/dd") ?? "N/A";

    public BitmapImage? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            SetProperty(ref _isSelected, value);
            NotifyPropertyChanged(nameof(Background));
            NotifyPropertyChanged(nameof(BorderBrush));
            NotifyPropertyChanged(nameof(BorderThickness));
        }
    }

    public Brush Background => IsSelected
        ? new SolidColorBrush(Color.FromArgb(40, 0, 120, 215))
        : Brushes.Transparent;

    public Brush BorderBrush => IsSelected
        ? new SolidColorBrush(Color.FromRgb(0, 120, 215))
        : Brushes.Transparent;

    public Thickness BorderThickness => IsSelected
        ? new Thickness(2) : new Thickness(0);

    public ICommand SelectCommand { get; }

    private async Task LoadThumbnailAsync()
    {
        var thumbnailAsset = Item.Assets?.Values.FirstOrDefault(a => a.IsThumbnail);
        if (thumbnailAsset != null)
            Thumbnail = await _thumbnailCache.GetThumbnailAsync(thumbnailAsset.Href);
    }
}

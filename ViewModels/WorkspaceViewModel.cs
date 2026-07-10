using System.Collections.ObjectModel;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using eodh.Models;
using eodh.Services;

namespace eodh.ViewModels;

internal sealed class WorkspaceViewModel : PropertyChangedBase
{
    private const string AllFilter = "All";

    private readonly AuthService _authService;
    private readonly WorkspaceService _workspaceService;
    private readonly LayerService _layerService;
    private readonly List<WorkspaceRecordViewModel> _allRecords = [];
    private string _selectedProvider = AllFilter;
    private string _selectedStatus = AllFilter;
    private bool _isLoading;
    private bool _hasError;
    private bool _isAuthenticationError;
    private string _errorMessage = string.Empty;

    public WorkspaceViewModel(
        AuthService authService,
        WorkspaceService? workspaceService = null,
        LayerService? layerService = null)
    {
        _authService = authService;
        _workspaceService = workspaceService ?? new WorkspaceService(authService);
        _layerService = layerService ?? new LayerService(authService);
        ProviderOptions.Add(AllFilter);
        StatusOptions.Add(AllFilter);
        RefreshCommand = new RelayCommand(ExecuteRefresh, () => !IsLoading);
    }

    public string WorkspaceName => _authService.Username ?? string.Empty;
    public ObservableCollection<WorkspaceRecordViewModel> Records { get; } = [];
    public ObservableCollection<string> ProviderOptions { get; } = [];
    public ObservableCollection<string> StatusOptions { get; } = [];

    public string SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value))
                ApplyFilters();
        }
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
                ApplyFilters();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                ((RelayCommand)RefreshCommand).RaiseCanExecuteChanged();
                NotifyStateChanged();
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (SetProperty(ref _hasError, value))
                NotifyStateChanged();
        }
    }

    public bool IsAuthenticationError
    {
        get => _isAuthenticationError;
        private set => SetProperty(ref _isAuthenticationError, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool HasRecords => Records.Count > 0;
    public bool IsEmpty => !IsLoading && !HasError && Records.Count == 0;
    public ICommand RefreshCommand { get; }

    public async Task InitializeAsync()
    {
        NotifyPropertyChanged(nameof(WorkspaceName));
        await LoadRecordsAsync();
    }

    public void Clear()
    {
        _allRecords.Clear();
        Records.Clear();
        HasError = false;
        IsAuthenticationError = false;
        ErrorMessage = string.Empty;
        NotifyPropertyChanged(nameof(WorkspaceName));
        NotifyStateChanged();
    }

    private async Task LoadRecordsAsync()
    {
        if (string.IsNullOrWhiteSpace(WorkspaceName))
        {
            HasError = true;
            ErrorMessage = "Sign in to a workspace to view commercial data.";
            return;
        }

        IsLoading = true;
        HasError = false;
        IsAuthenticationError = false;
        ErrorMessage = string.Empty;
        try
        {
            var records = await _workspaceService.GetCommercialRecordsAsync(WorkspaceName);
            _allRecords.Clear();
            _allRecords.AddRange(records.Select(record =>
                new WorkspaceRecordViewModel(record, _layerService)));
            RebuildFilterOptions();
            ApplyFilters();
        }
        catch (ApiException ex)
        {
            _allRecords.Clear();
            Records.Clear();
            IsAuthenticationError = ex.Category == ApiErrorCategory.Authentication;
            ErrorMessage = ex.Message;
            HasError = true;
        }
        catch (Exception ex)
        {
            _allRecords.Clear();
            Records.Clear();
            ErrorMessage = $"Commercial workspace data could not be loaded: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    private void RebuildFilterOptions()
    {
        RebuildOptions(ProviderOptions, _allRecords.Select(record => record.Provider));
        RebuildOptions(StatusOptions, _allRecords.Select(record => record.Status));
        if (!ProviderOptions.Contains(SelectedProvider))
            SelectedProvider = AllFilter;
        if (!StatusOptions.Contains(SelectedStatus))
            SelectedStatus = AllFilter;
    }

    private static void RebuildOptions(
        ObservableCollection<string> target,
        IEnumerable<string> values)
    {
        target.Clear();
        target.Add(AllFilter);
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            target.Add(value);
    }

    private void ApplyFilters()
    {
        var filtered = _allRecords.Where(record =>
            (SelectedProvider == AllFilter ||
             record.Provider.Equals(SelectedProvider, StringComparison.OrdinalIgnoreCase)) &&
            (SelectedStatus == AllFilter ||
             record.Status.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase)));

        Records.Clear();
        foreach (var record in filtered)
            Records.Add(record);
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        NotifyPropertyChanged(nameof(HasRecords));
        NotifyPropertyChanged(nameof(IsEmpty));
    }

    private async void ExecuteRefresh() => await LoadRecordsAsync();
}

internal sealed class WorkspaceRecordViewModel : PropertyChangedBase
{
    private readonly LayerService _layerService;
    private bool _isLoading;

    public WorkspaceRecordViewModel(WorkspaceCommercialRecord record, LayerService layerService)
    {
        Record = record;
        _layerService = layerService;
        Assets = (record.Item.Assets ?? [])
            .Select(asset => new AssetDetailViewModel
            {
                Key = asset.Key,
                DisplayName = asset.Value.Title ?? asset.Key,
                FileType = asset.Value.FileType,
                IsLoadable = asset.Value.IsLoadable,
                IsSelected = asset.Value.IsLoadable
            })
            .ToList();
        LoadCommand = new RelayCommand(ExecuteLoad, () => CanLoadIntoMap && !IsLoading);
    }

    public WorkspaceCommercialRecord Record { get; }
    public string Provider => Record.ProviderLabel;
    public string Collection => Record.Collection.DisplayName;
    public string ItemId => Record.Item.Id;
    public string Status => Record.Status;
    public string? Message => Record.Message;
    public string? OrderId => Record.OrderId;
    public string? Created => FormatDate(Record.Created ?? Record.OrderDate);
    public string? Updated => FormatDate(Record.Updated);
    public List<AssetDetailViewModel> Assets { get; }
    public bool CanLoadIntoMap => Record.IsCompleted && Assets.Any(asset => asset.IsLoadable);

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            SetProperty(ref _isLoading, value);
            ((RelayCommand)LoadCommand).RaiseCanExecuteChanged();
        }
    }

    public ICommand LoadCommand { get; }

    private async void ExecuteLoad()
    {
        if (!CanLoadIntoMap || Record.Item.Assets == null)
            return;

        IsLoading = true;
        try
        {
            await _layerService.SetMapToOsgbAsync();
            foreach (var asset in Assets.Where(asset => asset.IsLoadable && asset.IsSelected))
                await _layerService.LoadAssetAsync(Record.Item, Record.Item.Assets[asset.Key], asset.Key);
        }
        catch (Exception ex)
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                $"The commercial asset could not be loaded.\n\n{ex.Message}",
                "EODH — Asset Load Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string? FormatDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed)
            ? parsed.ToString("yyyy-MM-dd HH:mm")
            : value;
}

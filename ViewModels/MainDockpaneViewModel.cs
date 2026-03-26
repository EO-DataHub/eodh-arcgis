using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace eodh.ViewModels;

/// <summary>
/// ViewModel for the main EODH dockpane. Hosts tab navigation between
/// Login, Search, Results, Workspace, and Workflows views.
/// </summary>
internal class MainDockpaneViewModel : DockPane
{
    private const string DockPaneId = "eodh_MainDockpane";

    private readonly LoginViewModel _loginViewModel;
    private readonly SearchViewModel _searchViewModel;
    private readonly ResultsViewModel _resultsViewModel;
    private readonly TimelineViewModel _timelineViewModel;
    private readonly WorkspaceViewModel _workspaceViewModel;
    private readonly WorkflowsViewModel _workflowsViewModel;
    private readonly Services.AuthService _authService;

    private bool _isLoggedIn;
    private bool _syncing;
    private int _selectedTabIndex;
    private string _loggedInUsername = string.Empty;
    private string _loggedInEnvironment = string.Empty;

    public MainDockpaneViewModel()
    {
        var authService = new Services.AuthService();
        _authService = authService;
        var stacClient = new Services.StacClient(authService);
        var thumbnailCache = new Services.ThumbnailCache();
        var layerService = new Services.LayerService(authService);
        var workspaceService = new Services.WorkspaceService(authService);

        _loginViewModel = new LoginViewModel(authService, OnLoginSuccess);
        _searchViewModel = new SearchViewModel(stacClient, OnSearchCompleted);
        _resultsViewModel = new ResultsViewModel(stacClient, layerService, thumbnailCache, workspaceService);
        _timelineViewModel = new TimelineViewModel(stacClient, layerService, thumbnailCache);
        _workspaceViewModel = new WorkspaceViewModel(authService);
        _workflowsViewModel = new WorkflowsViewModel();

        _timelineViewModel.SetSelectionCallback(item =>
        {
            if (_syncing) return;
            _syncing = true;
            _resultsViewModel.SelectByItemId(item.Id);
            _syncing = false;
        });

        _resultsViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ResultsViewModel.SelectedItem) && !_syncing)
            {
                _syncing = true;
                if (_resultsViewModel.SelectedItem != null)
                    _timelineViewModel.SelectByItemId(_resultsViewModel.SelectedItem.Item.Id);
                _syncing = false;
            }
        };
    }

    #region Properties

    public LoginViewModel LoginVM => _loginViewModel;
    public SearchViewModel SearchVM => _searchViewModel;
    public ResultsViewModel ResultsVM => _resultsViewModel;
    public TimelineViewModel TimelineVM => _timelineViewModel;
    public WorkspaceViewModel WorkspaceVM => _workspaceViewModel;
    public WorkflowsViewModel WorkflowsVM => _workflowsViewModel;

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set => SetProperty(ref _isLoggedIn, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string LoggedInUsername
    {
        get => _loggedInUsername;
        set => SetProperty(ref _loggedInUsername, value);
    }

    public string LoggedInEnvironment
    {
        get => _loggedInEnvironment;
        set => SetProperty(ref _loggedInEnvironment, value);
    }

    public ICommand LogoutCommand => new RelayCommand(ExecuteLogout);

    #endregion

    #region Static Show

    internal static void Show()
    {
        var pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
        pane?.Activate();
    }

    #endregion

    #region Event Handlers

    private void OnLoginSuccess()
    {
        LoggedInUsername = _authService.Username ?? string.Empty;
        LoggedInEnvironment = _authService.Environment;
        IsLoggedIn = true;
        SelectedTabIndex = 0;
        _ = SearchVM.LoadCatalogsAsync();
    }

    private void ExecuteLogout()
    {
        _authService.ClearCredentials();
        IsLoggedIn = false;
        LoggedInUsername = string.Empty;
        LoggedInEnvironment = string.Empty;
    }

    private void OnSearchCompleted(List<Models.StacItem> results)
    {
        ResultsVM.LoadResults(results, SearchVM.CurrentFilters, SearchVM.SelectedCollection?.License);
        TimelineVM.LoadResults(results);
        SelectedTabIndex = 1;
    }

    #endregion
}

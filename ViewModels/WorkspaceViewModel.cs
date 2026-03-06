using System.Collections.ObjectModel;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using eodh.Models;
using eodh.Services;

namespace eodh.ViewModels;

/// <summary>
/// ViewModel for the workspace view — displays organisational workspaces,
/// shared assets, members, and purchase controls.
/// </summary>
internal class WorkspaceViewModel : PropertyChangedBase
{
    private readonly AuthService _authService;
    private WorkspaceService? _workspaceService;
    private WorkspaceInfo? _selectedWorkspace;

    public WorkspaceViewModel(AuthService authService)
    {
        _authService = authService;
        RefreshCommand = new RelayCommand(ExecuteRefresh);
    }

    #region Properties

    public ObservableCollection<WorkspaceInfo> Workspaces { get; } = [];
    public ObservableCollection<WorkspaceMember> Members { get; } = [];
    public ObservableCollection<WorkspaceAsset> Assets { get; } = [];

    public WorkspaceInfo? SelectedWorkspace
    {
        get => _selectedWorkspace;
        set
        {
            if (SetProperty(ref _selectedWorkspace, value))
                OnWorkspaceSelected();
        }
    }

    public ICommand RefreshCommand { get; }

    #endregion

    #region Public Methods

    public async Task InitializeAsync()
    {
        _workspaceService = new WorkspaceService(_authService);
        await LoadWorkspacesAsync();
    }

    #endregion

    #region Private Methods

    private async Task LoadWorkspacesAsync()
    {
        if (_workspaceService == null) return;

        try
        {
            var workspaces = await _workspaceService.GetWorkspacesAsync();
            Workspaces.Clear();
            foreach (var ws in workspaces)
                Workspaces.Add(ws);

            if (Workspaces.Count > 0)
                SelectedWorkspace = Workspaces[0];
        }
        catch
        {
            // Workspace API may not be available in all environments
        }
    }

    private void OnWorkspaceSelected()
    {
        Members.Clear();
        Assets.Clear();

        if (_selectedWorkspace == null) return;

        foreach (var member in _selectedWorkspace.Members)
            Members.Add(member);

        foreach (var asset in _selectedWorkspace.Assets)
            Assets.Add(asset);
    }

    private async void ExecuteRefresh()
    {
        await LoadWorkspacesAsync();
    }

    #endregion
}

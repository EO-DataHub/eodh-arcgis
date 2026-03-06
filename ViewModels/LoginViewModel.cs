using System.Net.Http;
using System.Windows.Controls;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using eodh.Services;

namespace eodh.ViewModels;

/// <summary>
/// ViewModel for the login/authentication view.
/// Handles credential input and EODH API authentication.
/// </summary>
internal class LoginViewModel : PropertyChangedBase
{
    private readonly AuthService _authService;
    private readonly Action _onLoginSuccess;
    private PasswordBox? _passwordBox;

    private string _username = string.Empty;
    private string _selectedEnvironment = "production";
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private bool _isLoading;

    public LoginViewModel(AuthService authService, Action onLoginSuccess)
    {
        _authService = authService;
        _onLoginSuccess = onLoginSuccess;
        LoginCommand = new RelayCommand(ExecuteLogin, CanLogin);
        _ = TryAutoLoginAsync();
    }

    /// <summary>
    /// Attempts to restore saved credentials and validate them.
    /// If valid, skips the login form entirely.
    /// </summary>
    private async Task TryAutoLoginAsync()
    {
        if (!_authService.TryLoadSavedCredentials()) return;

        Username = _authService.Username ?? string.Empty;
        IsLoading = true;
        try
        {
            var stacClient = new StacClient(_authService);
            var catalogs = await stacClient.GetCatalogsAsync();

            if (catalogs.Count > 0)
            {
                _onLoginSuccess.Invoke();
                return;
            }

            _authService.ClearCredentials();
        }
        catch
        {
            // Token expired or invalid — fall through to show login form
            _authService.ClearCredentials();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Called from code-behind to provide the PasswordBox reference.
    /// </summary>
    public void SetPasswordBox(PasswordBox pb) => _passwordBox = pb;

    #region Properties

    public string Username
    {
        get => _username;
        set
        {
            SetProperty(ref _username, value);
            ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
        }
    }

    public string SelectedEnvironment
    {
        get => _selectedEnvironment;
        set => SetProperty(ref _selectedEnvironment, value);
    }

    public List<string> Environments { get; } = ["production", "staging", "test"];

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            SetProperty(ref _isLoading, value);
            ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
        }
    }

    public ICommand LoginCommand { get; }

    #endregion

    #region Commands

    private bool CanLogin() => !string.IsNullOrWhiteSpace(Username) && !IsLoading;

    private async void ExecuteLogin()
    {
        var apiToken = _passwordBox?.Password;
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            ErrorMessage = "Please enter your API token.";
            HasError = true;
            return;
        }

        IsLoading = true;
        HasError = false;

        try
        {
            _authService.SetCredentials(Username.Trim(), apiToken.Trim(), SelectedEnvironment);

            var stacClient = new StacClient(_authService);
            var catalogs = await stacClient.GetCatalogsAsync();

            if (catalogs.Count == 0)
            {
                ErrorMessage = "Connected but no catalogs found. Check your credentials.";
                HasError = true;
                return;
            }

            _onLoginSuccess.Invoke();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Authentication failed: {ex.Message}";
            HasError = true;
            _authService.ClearCredentials();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
            HasError = true;
            _authService.ClearCredentials();
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion
}

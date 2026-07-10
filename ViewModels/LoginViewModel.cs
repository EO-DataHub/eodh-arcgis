using System.Windows.Controls;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using eodh.Services;

namespace eodh.ViewModels;

/// <summary>
/// Handles single-workspace credential input and validation.
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

    private async Task TryAutoLoginAsync()
    {
        if (!_authService.TryLoadSavedCredentials())
            return;

        Username = _authService.Username ?? string.Empty;
        IsLoading = true;
        try
        {
            var stacClient = new StacClient(_authService);
            await stacClient.ValidateCredentialsAsync();
            _onLoginSuccess.Invoke();
        }
        catch (ApiException ex) when (ex.Category == ApiErrorCategory.Authentication)
        {
            _authService.ClearCredentials();
            ErrorMessage = ex.Message;
            HasError = true;
        }
        catch (Exception ex)
        {
            _authService.ClearCredentials();
            ErrorMessage = $"Saved credentials could not be validated: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SetPasswordBox(PasswordBox passwordBox) => _passwordBox = passwordBox;

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

    private bool CanLogin() => !string.IsNullOrWhiteSpace(Username) && !IsLoading;

    private async void ExecuteLogin()
    {
        var apiKey = _passwordBox?.Password;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ErrorMessage = "Please enter your workspace API key (not the Token ID).";
            HasError = true;
            return;
        }

        IsLoading = true;
        HasError = false;
        try
        {
            _authService.SetCredentials(Username.Trim(), apiKey.Trim(), SelectedEnvironment);
            var stacClient = new StacClient(_authService);
            await stacClient.ValidateCredentialsAsync();
            _onLoginSuccess.Invoke();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
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
}

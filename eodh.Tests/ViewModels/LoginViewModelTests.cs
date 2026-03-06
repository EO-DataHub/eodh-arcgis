using System.IO;
using System.Net;
using System.Windows.Controls;
using eodh.Services;
using eodh.Tests.Helpers;
using eodh.ViewModels;
using Xunit;

namespace eodh.Tests.ViewModels;

/// <summary>
/// Req 1: Authentication — documents expected LoginViewModel behaviour:
/// login flow, environment selection, credential validation, and error handling.
/// All tests require ArcGIS Pro SDK runtime (PropertyChangedBase, RelayCommand).
/// </summary>
[Collection("ArcGIS-SDK")]
[Trait("Category", "RequiresArcGIS")]
public class LoginViewModelTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>
    /// Runs an action on a fresh STA thread. PasswordBox requires STA affinity
    /// and this avoids depending on Application.Current.Dispatcher which may
    /// not exist or may belong to the VS test runner.
    /// </summary>
    private static void RunOnSta(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { caught = ex; }
        });
        thread.TrySetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (caught != null)
            throw new AggregateException(caught);
    }

    private static LoginViewModel CreateVm(
        AuthService? auth = null, Action? onSuccess = null)
    {
        // Use an empty temp credential store so TryAutoLoginAsync (fired in the
        // constructor) finds no saved credentials and returns immediately.
        auth ??= new AuthService(new CredentialStore(
            Path.Combine(Path.GetTempPath(), "eodh-test-" + Guid.NewGuid().ToString("N"))));
        return new LoginViewModel(auth, onSuccess ?? (() => { }));
    }

    [Fact]
    public void LoginCommand_CanExecute_WhenUsernameProvided()
    {
        var vm = CreateVm();
        vm.Username = "testuser";
        Assert.True(vm.LoginCommand.CanExecute(null));
    }

    [Fact]
    public void LoginCommand_CannotExecute_WhenUsernameEmpty()
    {
        var vm = CreateVm();
        vm.Username = "";
        Assert.False(vm.LoginCommand.CanExecute(null));
    }

    [Fact]
    public void LoginCommand_CannotExecute_WhileLoading()
    {
        var vm = CreateVm();
        vm.Username = "testuser";
        vm.IsLoading = true;
        Assert.False(vm.LoginCommand.CanExecute(null));
    }

    [Fact]
    public void Environments_ContainsThreeOptions()
    {
        var vm = CreateVm();
        Assert.Equal(3, vm.Environments.Count);
        Assert.Contains("production", vm.Environments);
        Assert.Contains("staging", vm.Environments);
        Assert.Contains("test", vm.Environments);
    }

    [Fact]
    public void SelectedEnvironment_DefaultsToProduction()
    {
        var vm = CreateVm();
        Assert.Equal("production", vm.SelectedEnvironment);
    }

    [Fact]
    public void ExecuteLogin_SetsCredentials_OnAuthService()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterJson("token=", """{"catalogs":[],"collections":[],"links":[]}""");
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));
        var auth = new TestAuthService(handler);
        var vm = new LoginViewModel(auth, () => { });
        vm.Username = "testuser";

        RunOnSta(() =>
        {
            var pb = new PasswordBox { Password = "test-token" };
            vm.SetPasswordBox(pb);
            vm.LoginCommand.Execute(null);
        });

        Assert.True(auth.IsAuthenticated);
    }

    [Fact]
    public void ExecuteLogin_ValidatesWithCatalogFetch()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterJson("token=", """{"catalogs":[],"collections":[],"links":[]}""");
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));
        var auth = new TestAuthService(handler);
        var vm = new LoginViewModel(auth, () => { });
        vm.Username = "testuser";

        RunOnSta(() =>
        {
            var pb = new PasswordBox { Password = "test-token" };
            vm.SetPasswordBox(pb);
            vm.LoginCommand.Execute(null);
        });

        Assert.False(vm.HasError);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void ExecuteLogin_ClearsCredentials_OnHttpError()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterStatus("/api/catalogue/stac/catalogs", HttpStatusCode.Unauthorized);
        var auth = new TestAuthService(handler);
        var vm = new LoginViewModel(auth, () => { });
        vm.Username = "testuser";

        RunOnSta(() =>
        {
            var pb = new PasswordBox { Password = "test-token" };
            vm.SetPasswordBox(pb);
            vm.LoginCommand.Execute(null);
        });

        Assert.True(vm.HasError);
        Assert.False(auth.IsAuthenticated);
    }

    [Fact]
    public void ExecuteLogin_ShowsError_WhenTokenEmpty()
    {
        var vm = CreateVm();
        vm.Username = "testuser";
        // No PasswordBox set — _passwordBox?.Password is null — treated as empty
        vm.LoginCommand.Execute(null);

        Assert.True(vm.HasError);
        Assert.Equal("Please enter your API token.", vm.ErrorMessage);
    }

    [Fact]
    public void ExecuteLogin_CallsOnLoginSuccess_WhenCatalogsReturned()
    {
        var handler = new FixtureHttpHandler();
        handler.RegisterJson("token=", """{"catalogs":[],"collections":[],"links":[]}""");
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));
        var auth = new TestAuthService(handler);
        var loginSuccessCalled = false;
        var vm = new LoginViewModel(auth, () => loginSuccessCalled = true);
        vm.Username = "testuser";

        RunOnSta(() =>
        {
            var pb = new PasswordBox { Password = "test-token" };
            vm.SetPasswordBox(pb);
            vm.LoginCommand.Execute(null);
        });

        Assert.True(loginSuccessCalled);
    }

    [Fact]
    public async Task AutoLogin_SetsIsLoadingFalse_AfterSuccess()
    {
        // Simulate saved credentials: TestAuthService calls SetCredentials in
        // its constructor, which persists to the temp CredentialStore.
        // TryAutoLoginAsync will find them and auto-login.
        var handler = new FixtureHttpHandler();
        handler.RegisterJson("token=", """{"catalogs":[],"collections":[],"links":[]}""");
        handler.Register("/api/catalogue/stac/catalogs", FixturePath("catalogs.json"));
        var auth = new TestAuthService(handler);
        var loginSuccessCalled = false;

        var vm = new LoginViewModel(auth, () => loginSuccessCalled = true);

        // Give TryAutoLoginAsync time to complete
        await Task.Delay(500);

        Assert.True(loginSuccessCalled);
        // After successful auto-login, IsLoading must be false so the user
        // can sign in again after signing out.
        Assert.False(vm.IsLoading);
    }
}

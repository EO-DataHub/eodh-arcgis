using System.Reflection;
using System.Windows.Markup;
using ArcGIS.Core;
using ArcGIS.Desktop.Core;
using Xunit;

namespace eodh.Tests.Helpers;

/// <summary>
/// xUnit collection fixture that boots ArcGIS Pro in test mode.
/// Adapted from Esri's ProGuide Regression Testing (MSTest) for xUnit.
/// Pro runs on a background STA thread for the lifetime of the test collection.
///
/// Note: TestModeInitialize is internal to ArcGIS.Desktop.Core, so we call it
/// via reflection (same pattern used by community add-in test projects).
/// </summary>
public class ArcGISProFixture : IAsyncLifetime
{
    private ProApp? _application;

    static ArcGISProFixture()
    {
        TestResolver.Install("ArcGISPro");
    }

    public System.Windows.Threading.Dispatcher? Dispatcher { get; private set; }

    public Task InitializeAsync()
    {
        var tcs = new TaskCompletionSource();

        var uiThread = new Thread(() =>
        {
            // Capture this STA thread's Dispatcher so tests can marshal work to it
            Dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;

            try
            {
                // ProApp extends System.Windows.Application — only one can exist
                // per AppDomain. The VS test runner (or WPF test host) may have
                // already created an Application, so catch that case gracefully.
                if (System.Windows.Application.Current == null)
                {
                    _application = new ProApp();

                    var method = typeof(ProApp).GetMethod("TestModeInitialize",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    method?.Invoke(_application, null);
                }

                tcs.SetResult();
            }
            catch (InvalidOperationException)
            {
                // "Cannot create more than one Application" — race between the
                // null-check and new ProApp(). Swallow and continue; SDK types
                // (RelayCommand, PropertyChangedBase) still work, and integration
                // tests guard with MapView.Active null-checks.
                tcs.SetResult();
            }
            catch (XamlParseException)
            {
                tcs.SetException(new FatalArcGISException("Pro is not licensed"));
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            // Keep this STA thread alive — needed for QueuedTask.Run(),
            // Dispatcher.Invoke(), and other Pro/WPF operations.
            System.Windows.Threading.Dispatcher.Run();
        });

        uiThread.TrySetApartmentState(ApartmentState.STA);
        uiThread.Name = "Test UI Thread";
        uiThread.IsBackground = true;
        uiThread.Start();

        tcs.Task.Wait();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try
        {
            _application?.Shutdown();
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.Print(
                "Application.Shutdown threw an exception that was ignored. message: {0}", e.Message);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// xUnit collection definition for tests that need ArcGIS Pro runtime.
/// Apply [Collection("ArcGIS")] to test classes that use Pro APIs
/// (QueuedTask.Run, MapView, Geoprocessing, etc.).
/// </summary>
[CollectionDefinition("ArcGIS")]
public class ArcGISProCollection : ICollectionFixture<ArcGISProFixture>
{
}

/// <summary>
/// Lightweight fixture that only loads ArcGIS SDK assemblies (via TestResolver)
/// without starting Pro or a WPF Dispatcher. Use for tests that need SDK types
/// (PropertyChangedBase, RelayCommand, geometry builders) but not the full runtime.
/// </summary>
public class ArcGIsSdkFixture
{
    static ArcGIsSdkFixture()
    {
        TestResolver.Install("ArcGISPro");
    }
}

/// <summary>
/// xUnit collection for tests that need ArcGIS SDK types loaded but
/// do NOT need the Pro runtime. No STA thread, no Dispatcher, no ProApp.
/// </summary>
[CollectionDefinition("ArcGIS-SDK")]
public class ArcGIsSdkCollection : ICollectionFixture<ArcGIsSdkFixture>
{
}

/// <summary>
/// Separate collection for integration tests so they run in parallel
/// with the ArcGIS-SDK ViewModel tests, not sequentially after them.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<ArcGIsSdkFixture>
{
}

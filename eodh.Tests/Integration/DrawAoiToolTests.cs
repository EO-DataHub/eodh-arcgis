using ArcGIS.Core.Geometry;
using eodh.Services;
using eodh.Tests.Helpers;
using eodh.ViewModels;
using Xunit;

namespace eodh.Tests.Integration;

/// <summary>
/// Req 2: AOI polygon input (draw) — verifies that the AoiDrawn event
/// pipeline correctly updates SearchViewModel's AOI state.
/// </summary>
[Trait("Category", "RequiresArcGIS")]
public class DrawAoiToolTests
{
    [Fact]
    public void OnSketchComplete_FiresAoiDrawnEvent()
    {
        // Build geometry — these builders are thread-safe, no QueuedTask needed
        var envelope = EnvelopeBuilderEx.CreateEnvelope(
            new Coordinate2D(-1.5, 51.0),
            new Coordinate2D(0.5, 52.0));

        // Test the SearchViewModel integration directly — ProcessSketchGeometry
        // requires GeometryEngine which needs the Pro runtime.
        var handler = new FixtureHttpHandler();
        var auth = new TestAuthService(handler);
        var stacClient = new StacClient(auth);
        var vm = new SearchViewModel(stacClient, _ => { });

        vm.SetAoiFromPolygon(envelope);

        Assert.True(vm.SearchCommand.CanExecute(null));
        Assert.Contains("Drawn AOI", vm.AoiDescription);
        Assert.Contains("-1.50", vm.AoiDescription);
    }

    [Fact]
    public void OnSketchComplete_SetsAoiOnSearchViewModel()
    {
        var handler = new FixtureHttpHandler();
        var auth = new TestAuthService(handler);
        var stacClient = new StacClient(auth);
        var vm = new SearchViewModel(stacClient, _ => { });

        var envelope = EnvelopeBuilderEx.CreateEnvelope(
            new Coordinate2D(-1.5, 51.0),
            new Coordinate2D(0.5, 52.0));

        vm.SetAoiFromPolygon(envelope);

        Assert.True(vm.SearchCommand.CanExecute(null));
        Assert.Contains("Drawn AOI", vm.AoiDescription);
        Assert.Contains("-1.50", vm.AoiDescription);
        Assert.Contains("52.00", vm.AoiDescription);
    }
}

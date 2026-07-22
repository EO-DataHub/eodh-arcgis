using System.IO;
using Xunit;

namespace eodh.Tests.Services;

public class MapProjectionPreservationTests
{
    [Fact]
    public void AssetLoading_DoesNotForceTheActiveMapProjection()
    {
        var projectRoot = FindProjectRoot();
        var layerService = File.ReadAllText(
            Path.Combine(projectRoot, "Services", "LayerService.cs"));
        var resultsViewModel = File.ReadAllText(
            Path.Combine(projectRoot, "ViewModels", "ResultsViewModel.cs"));
        var workspaceViewModel = File.ReadAllText(
            Path.Combine(projectRoot, "ViewModels", "WorkspaceViewModel.cs"));

        Assert.DoesNotContain("SetSpatialReference(", layerService, StringComparison.Ordinal);
        Assert.DoesNotContain("SetMapToOsgbAsync", layerService, StringComparison.Ordinal);
        Assert.DoesNotContain("SetMapToOsgbAsync", resultsViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("SetMapToOsgbAsync", workspaceViewModel, StringComparison.Ordinal);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null &&
               !File.Exists(Path.Combine(directory.FullName, "eodh.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}

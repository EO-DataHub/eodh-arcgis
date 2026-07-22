using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace eodh.Tests.Views;

public class MainDockpaneViewTests
{
    [Fact]
    public void AssetLoadStatusBindings_AreExplicitlyOneWay()
    {
        var xaml = ReadMainDockpaneXaml();
        var bindings = Regex.Matches(
            xaml,
            @"\{Binding AssetLoadStatus\.[^}]+\}");

        Assert.NotEmpty(bindings);
        Assert.All(bindings.Cast<Match>(), binding =>
            Assert.Contains("Mode=OneWay", binding.Value, StringComparison.Ordinal));
    }

    [Fact]
    public void AssetLoadStatus_IsPresentedAsNeutralStatusBar()
    {
        var xaml = ReadMainDockpaneXaml();

        Assert.Contains("Text=\"STATUS\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"#1C808080\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AssetLoadStatus.ProgressText, Mode=OneWay", xaml, StringComparison.Ordinal);
        Assert.Contains("IsIndeterminate=\"{Binding AssetLoadStatus.HasNonReadyStatus, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding AssetLoadStatus.HasNonReadyStatus, Mode=OneWay, Converter={StaticResource BoolToVis}}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"3\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<RowDefinition Height=\"3\" />", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ProgressBar.Style>", xaml, StringComparison.Ordinal);
    }

    private static string ReadMainDockpaneXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null &&
               !File.Exists(Path.Combine(directory.FullName, "eodh.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return File.ReadAllText(
            Path.Combine(directory!.FullName, "Views", "MainDockpaneView.xaml"));
    }
}

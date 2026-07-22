using System.IO;
using System.Text.RegularExpressions;
using eodh.Services;
using Xunit;

namespace eodh.Tests.Services;

public class FootprintOverlayStyleTests
{
    [Fact]
    public void Footprints_AreHollowWithThinOutlines()
    {
        Assert.Equal(0.75, FootprintOverlayService.OrdinaryOutlineWidth);
        Assert.Equal(1.5, FootprintOverlayService.SelectedOutlineWidth);

        var source = File.ReadAllText(
            Path.Combine(FindProjectRoot(), "Services", "FootprintOverlayService.cs"));
        Assert.Equal(2, Regex.Matches(source, @"SimpleFillStyle\.Null").Count);
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

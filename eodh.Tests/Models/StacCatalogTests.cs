using eodh.Models;
using Xunit;

namespace eodh.Tests.Models;

public class StacCatalogTests
{
    [Fact]
    public void DisplayName_ReturnsTitle_WhenTitleExists()
    {
        var catalog = new StacCatalog("my-id", "My Title", "My Description", null);
        Assert.Equal("My Title", catalog.DisplayName);
    }

    [Fact]
    public void DisplayName_FallsBackToDescription_WhenTitleIsNull()
    {
        var catalog = new StacCatalog("airbus", null, "Airbus Datasets", null);
        Assert.Equal("Airbus Datasets", catalog.DisplayName);
    }

    [Fact]
    public void DisplayName_FallsBackToId_WhenTitleAndDescriptionAreNull()
    {
        var catalog = new StacCatalog("some-catalog-id", null, null, null);
        Assert.Equal("some-catalog-id", catalog.DisplayName);
    }

    [Fact]
    public void DisplayName_SkipsEmptyTitle_FallsBackToDescription()
    {
        var catalog = new StacCatalog("my-id", "", "Airbus Datasets", null);
        Assert.Equal("Airbus Datasets", catalog.DisplayName);
    }

    [Fact]
    public void DisplayName_SkipsEmptyTitleAndDescription_FallsBackToId()
    {
        var catalog = new StacCatalog("my-id", "", "", null);
        Assert.Equal("my-id", catalog.DisplayName);
    }
}

public class StacCollectionTests
{
    [Fact]
    public void DisplayName_ReturnsTitle_WhenTitleExists()
    {
        var collection = new StacCollection("col-id", "Sentinel-2 L2A", "Description", null, null, null, null);
        Assert.Equal("Sentinel-2 L2A", collection.DisplayName);
    }

    [Fact]
    public void DisplayName_FallsBackToDescription_WhenTitleIsNull()
    {
        var collection = new StacCollection("col-id", null, "Some description", null, null, null, null);
        Assert.Equal("Some description", collection.DisplayName);
    }

    [Fact]
    public void DisplayName_FallsBackToId_WhenTitleAndDescriptionAreNull()
    {
        var collection = new StacCollection("col-id", null, null, null, null, null, null);
        Assert.Equal("col-id", collection.DisplayName);
    }

    [Fact]
    public void DisplayName_SkipsEmptyTitle_FallsBackToDescription()
    {
        var collection = new StacCollection("col-id", "", "Some description", null, null, null, null);
        Assert.Equal("Some description", collection.DisplayName);
    }

    [Fact]
    public void DisplayName_SkipsEmptyTitleAndDescription_FallsBackToId()
    {
        var collection = new StacCollection("col-id", "", "", null, null, null, null);
        Assert.Equal("col-id", collection.DisplayName);
    }
}

public class StacItemPropertiesTests
{
    [Fact]
    public void GeometricRmse_ParsedFromJson()
    {
        var json = """{"datetime":"2026-01-15T10:00:00Z","accuracy:geometric_rmse":12.5}""";
        var props = System.Text.Json.JsonSerializer.Deserialize<StacItemProperties>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(props);
        Assert.Equal(12.5, props!.GeometricRmse);
    }

    [Fact]
    public void GeometricRmse_NullWhenAbsent()
    {
        var json = """{"datetime":"2026-01-15T10:00:00Z"}""";
        var props = System.Text.Json.JsonSerializer.Deserialize<StacItemProperties>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(props);
        Assert.Null(props!.GeometricRmse);
    }
}

using System.IO;
using System.Text.Json.Serialization;

namespace eodh.Models;

/// <summary>
/// STAC Catalog metadata.
/// </summary>
public record StacCatalog(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("links")] List<StacLink>? Links
)
{
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Title) ? Title :
        !string.IsNullOrWhiteSpace(Description) ? Description : Id;
}

/// <summary>
/// STAC Collection metadata.
/// </summary>
public record StacCollection(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("license")] string? License,
    [property: JsonPropertyName("keywords")] List<string>? Keywords,
    [property: JsonPropertyName("extent")] StacExtent? Extent,
    [property: JsonPropertyName("links")] List<StacLink>? Links
)
{
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Title) ? Title :
        !string.IsNullOrWhiteSpace(Description) ? Description : Id;
}

/// <summary>
/// STAC spatial and temporal extent.
/// </summary>
public record StacExtent(
    [property: JsonPropertyName("spatial")] StacSpatialExtent? Spatial,
    [property: JsonPropertyName("temporal")] StacTemporalExtent? Temporal
);

public record StacSpatialExtent(
    [property: JsonPropertyName("bbox")] List<List<double>>? Bbox
);

public record StacTemporalExtent(
    [property: JsonPropertyName("interval")] List<List<string?>>? Interval
);

/// <summary>
/// STAC Item -- a single dataset/scene with geometry, properties, and assets.
/// </summary>
public record StacItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("collection")] string? Collection,
    [property: JsonPropertyName("geometry")] GeoJsonGeometry? Geometry,
    [property: JsonPropertyName("bbox")] List<double>? Bbox,
    [property: JsonPropertyName("properties")] StacItemProperties? Properties,
    [property: JsonPropertyName("assets")] Dictionary<string, StacAsset>? Assets,
    [property: JsonPropertyName("links")] List<StacLink>? Links
)
{
    /// <summary>
    /// Get the self link URL for this item.
    /// </summary>
    public string? SelfLink => Links?.FirstOrDefault(l => l.Rel == "self")?.Href;
}

/// <summary>
/// STAC Item properties -- common fields extracted for display.
/// </summary>
public record StacItemProperties(
    [property: JsonPropertyName("datetime")] string? DateTime,
    [property: JsonPropertyName("eo:cloud_cover")] double? CloudCover,
    [property: JsonPropertyName("gsd")] double? Gsd,
    [property: JsonPropertyName("proj:epsg")] int? ProjEpsg,
    [property: JsonPropertyName("sat:relative_orbit")] int? RelativeOrbit,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("created")] string? Created,
    [property: JsonPropertyName("updated")] string? Updated,
    [property: JsonPropertyName("accuracy:geometric_rmse")] double? GeometricRmse
)
{
    /// <summary>
    /// Parse the datetime string to a DateTimeOffset.
    /// </summary>
    public DateTimeOffset? ParsedDateTime
    {
        get
        {
            if (string.IsNullOrEmpty(DateTime)) return null;
            return DateTimeOffset.TryParse(DateTime, out var dt) ? dt : null;
        }
    }
}

/// <summary>
/// STAC Asset -- a file/resource within an item.
/// </summary>
public record StacAsset(
    [property: JsonPropertyName("href")] string Href,
    [property: JsonPropertyName("type")] string? MediaType,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("roles")] List<string>? Roles,
    [property: JsonPropertyName("proj:epsg")] int? ProjEpsg
)
{
    /// <summary>
    /// Determine the human-readable file type from the media type or href extension.
    /// </summary>
    public string FileType => MediaType switch
    {
        "image/tiff; application=geotiff; profile=cloud-optimized" => "COG",
        "image/tiff; application=geotiff" => "GeoTIFF",
        "image/tiff" => "GeoTIFF",
        "application/x-netcdf" or "application/netcdf" => "NetCDF",
        "image/png" => "PNG",
        "image/jpeg" => "JPEG",
        "application/json" => "JSON",
        "application/geo+json" or "application/geojson" => "GeoJSON",
        "application/xml" or "text/xml" => "XML",
        "text/html" => "HTML",
        "text/plain" => "Text",
        "application/pdf" => "PDF",
        "application/geopackage+sqlite3" => "GPKG",
        _ when Href.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) => "GeoTIFF",
        _ when Href.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase) => "GeoTIFF",
        _ when Href.EndsWith(".nc", StringComparison.OrdinalIgnoreCase) => "NetCDF",
        _ when Href.EndsWith(".json", StringComparison.OrdinalIgnoreCase) => "JSON",
        _ when Href.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase) => "GeoJSON",
        _ when Href.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) => "XML",
        _ when Href.EndsWith(".png", StringComparison.OrdinalIgnoreCase) => "PNG",
        _ when Href.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) => "JPEG",
        _ when Href.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) => "JPEG",
        _ when MediaType != null => MediaType.Split('/')[^1].Split(';')[0].Trim().ToUpperInvariant(),
        _ => Path.GetExtension(Href).TrimStart('.').ToUpperInvariant() is { Length: > 0 } ext ? ext : "File"
    };

    /// <summary>
    /// Check if this asset is a loadable raster type (COG, GeoTIFF, or NetCDF).
    /// </summary>
    public bool IsLoadable => FileType is "COG" or "GeoTIFF" or "NetCDF";

    /// <summary>
    /// Check if this asset is a thumbnail.
    /// </summary>
    public bool IsThumbnail => Roles?.Contains("thumbnail") == true;

    /// <summary>
    /// Check if this asset is a data asset.
    /// </summary>
    public bool IsData => Roles?.Contains("data") == true || Roles?.Contains("visual") == true;
}

/// <summary>
/// GeoJSON geometry for item footprints.
/// </summary>
public record GeoJsonGeometry(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("coordinates")] System.Text.Json.JsonElement Coordinates
);

/// <summary>
/// STAC Link -- navigation link within the catalog.
/// </summary>
public record StacLink(
    [property: JsonPropertyName("rel")] string Rel,
    [property: JsonPropertyName("href")] string Href,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("title")] string? Title
);

/// <summary>
/// STAC search response wrapper (ItemCollection).
/// </summary>
public record StacItemCollection(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("features")] List<StacItem> Features,
    [property: JsonPropertyName("links")] List<StacLink>? Links,
    [property: JsonPropertyName("numMatched")] int? NumberMatched,
    [property: JsonPropertyName("numReturned")] int? NumberReturned,
    [property: JsonPropertyName("context")] StacSearchContext? Context
);

/// <summary>
/// STAC search context with pagination info.
/// </summary>
public record StacSearchContext(
    [property: JsonPropertyName("matched")] int? Matched,
    [property: JsonPropertyName("returned")] int? Returned,
    [property: JsonPropertyName("limit")] int? Limit
);

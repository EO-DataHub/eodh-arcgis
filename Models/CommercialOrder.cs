using System.Text.Json.Serialization;

namespace eodh.Models;

/// <summary>
/// Identifies a commercial data provider and its capabilities.
/// </summary>
public enum CommercialProvider
{
    Unknown,
    AirbusOptical,  // PHR, Pleiades Neo — requires licence + endUserCountry
    AirbusSar,      // TerraSAR-X etc. — requires licence, no coordinates
    Planet          // No licence needed, supports coordinates
}

/// <summary>
/// Request body for POST {item_self_href}/quote.
/// Coordinates uses GeoJSON Polygon ring structure: [[[lon,lat], ...]].
/// </summary>
public record QuoteRequest(
    [property: JsonPropertyName("licence")] string? Licence,
    [property: JsonPropertyName("coordinates")] double[][][]? Coordinates
);

/// <summary>
/// Response from POST {item_self_href}/quote.
/// </summary>
public record QuoteResponse(
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("area")] double? Area,
    [property: JsonPropertyName("areaUnit")] string? AreaUnit
);

/// <summary>
/// Request body for POST {item_self_href}/order.
/// Coordinates uses GeoJSON Polygon ring structure: [[[lon,lat], ...]].
/// </summary>
public record OrderRequest(
    [property: JsonPropertyName("licence")] string? Licence,
    [property: JsonPropertyName("endUserCountry")] string? EndUserCountry,
    [property: JsonPropertyName("productBundle")] string? ProductBundle,
    [property: JsonPropertyName("coordinates")] double[][][]? Coordinates
);

/// <summary>
/// Result of placing an order. The Location header URL is where the
/// ordered item will appear in the workspace once fulfilled.
/// </summary>
public record OrderResult(
    bool Success,
    string? LocationUrl,
    string? ErrorMessage
);

using System.Text.Json.Serialization;

namespace eodh.Models;

public enum CommercialProvider
{
    Unknown,
    AirbusOptical,
    AirbusSar,
    Planet
}

/// <summary>
/// The complete UI and request capability contract for one provider mode.
/// </summary>
public sealed record CommercialProviderCapabilities(
    CommercialProvider Provider,
    bool SupportsCoordinates,
    bool RequiresEndUserCountry,
    IReadOnlyList<string> LicenceOptions,
    IReadOnlyList<string> ProductBundles,
    IReadOnlyList<string> OrbitOptions,
    IReadOnlyList<string> ResolutionVariantOptions,
    IReadOnlyList<string> ProjectionOptions)
{
    public bool RequiresLicence => LicenceOptions.Count > 0;
    public bool HasRadarOptions => OrbitOptions.Count > 0;

    public bool RequiresResolutionVariant(string? bundle) =>
        HasRadarOptions && !string.Equals(bundle, "SSC", StringComparison.OrdinalIgnoreCase);

    public bool RequiresProjection(string? bundle) =>
        HasRadarOptions &&
        !string.Equals(bundle, "SSC", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(bundle, "MGD", StringComparison.OrdinalIgnoreCase);
}

public sealed record QuoteRequest(
    [property: JsonPropertyName("coordinates")] double[][][]? Coordinates,
    [property: JsonPropertyName("licence")] string? Licence,
    [property: JsonPropertyName("productBundle")] string? ProductBundle);

public sealed record QuoteResponse(
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("units")] string Units,
    [property: JsonPropertyName("message")] string? Message);

public sealed record RadarOptions(
    [property: JsonPropertyName("orbit")] string Orbit,
    [property: JsonPropertyName("resolutionVariant")] string? ResolutionVariant,
    [property: JsonPropertyName("projection")] string? Projection);

public sealed record OrderRequest(
    [property: JsonPropertyName("productBundle")] string ProductBundle,
    [property: JsonPropertyName("coordinates")] double[][][]? Coordinates,
    [property: JsonPropertyName("endUserCountry")] string? EndUserCountry,
    [property: JsonPropertyName("licence")] string? Licence,
    [property: JsonPropertyName("radarOptions")] RadarOptions? RadarOptions);

public sealed record OrderResult(bool Success, string? LocationUrl, string? ErrorMessage);

using eodh.Models;

namespace eodh.Tools;

/// <summary>
/// Builds TiTiler XYZ URLs for lightweight previews of STAC raster assets.
/// </summary>
internal static class TitilerXyzUrlBuilder
{
    private const string DefaultTitle = "Natural Color";
    private const string DefaultColorFormula =
        "Gamma RGB 6 Saturation 0.8 Sigmoidal RGB 25 0.35";

    public static bool CanBuild(StacItem item, string assetKey)
    {
        return !string.IsNullOrWhiteSpace(item.SelfLink) &&
               item.Assets?.TryGetValue(assetKey, out var asset) == true &&
               asset.FileType is "COG" or "GeoTIFF";
    }

    public static string? Build(string hubBaseUrl, StacItem item, string assetKey)
    {
        if (!CanBuild(item, assetKey))
            return null;

        var query = string.Join("&",
        [
            $"url={Encode(item.SelfLink!)}",
            $"title={Encode(DefaultTitle)}",
            $"assets={Encode(assetKey)}",
            "bidx=3",
            "bidx=2",
            "bidx=1",
            "nodata=0",
            $"color_formula={Encode(DefaultColorFormula)}",
            $"id={Encode(DefaultTitle)}"
        ]);

        return $"{hubBaseUrl.TrimEnd('/')}/titiler/core/stac/tiles/" +
               $"WebMercatorQuad/{{z}}/{{x}}/{{y}}@1x?{query}";
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);
}

using eodh.Models;

namespace eodh.Tools;

/// <summary>
/// Pure selection logic for determining which STAC assets are loadable.
/// </summary>
internal static class AssetSelector
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultAssetKeys =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["s2ard"] = ["cog"],
            ["sentinel2ard"] = ["cog"],
            ["s1ard"] = ["data"],
            ["sentinel1ard"] = ["data"],
            ["eocischuklai"] = ["data"],
            ["eocischukfpar"] = ["data"],
            ["eocischuklandcover"] = ["data"],
            ["eocischuklandclass"] = ["data_lccs_class"],
            ["eocischukelevation"] = ["data"]
        };

    public static List<(string Key, StacAsset Asset)> GetLoadableAssets(
        Dictionary<string, StacAsset>? assets)
    {
        if (assets == null) return [];

        return assets
            .Where(kv => kv.Value.IsLoadable)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }

    public static IReadOnlyList<string> GetDefaultAssetKeys(string? collection)
    {
        var normalized = NormalizeCollection(collection);
        return DefaultAssetKeys.TryGetValue(normalized, out var keys) ? keys : [];
    }

    private static string NormalizeCollection(string? collection) =>
        new((collection ?? string.Empty).Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant).ToArray());
}

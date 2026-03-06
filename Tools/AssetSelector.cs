using eodh.Models;

namespace eodh.Tools;

/// <summary>
/// Pure selection logic for determining which STAC assets are loadable.
/// </summary>
internal static class AssetSelector
{
    public static List<(string Key, StacAsset Asset)> GetLoadableAssets(
        Dictionary<string, StacAsset>? assets)
    {
        if (assets == null) return [];

        return assets
            .Where(kv => kv.Value.IsLoadable)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }
}

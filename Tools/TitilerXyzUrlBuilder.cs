using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using eodh.Models;

namespace eodh.Tools;

/// <summary>
/// Builds TiTiler XYZ URLs from the same collection render configuration used by
/// the EODH web integration tools.
/// </summary>
internal static class TitilerXyzUrlBuilder
{
    private const string RenderConfigResourceName =
        "eodh.Resources.configFromServer.json";

    private static readonly Lazy<IReadOnlyDictionary<string, CollectionConfig>>
        CollectionRenderingConfig = new(LoadCollectionRenderingConfig);

    public static bool CanBuild(
        StacItem item,
        string assetKey,
        string? renderId = null)
    {
        var configuredRender = FindConfiguredRender(item.Collection, renderId);
        if (configuredRender == null ||
            !string.IsNullOrWhiteSpace(configuredRender.Value.Render.QuicklookGeoreference))
        {
            return false;
        }

        var firstRenderAsset = configuredRender.Value.Render.Assets?.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(firstRenderAsset) &&
               string.Equals(firstRenderAsset, assetKey, StringComparison.Ordinal) &&
               item.Assets?.ContainsKey(assetKey) == true &&
               ResolveSourceUrl(item, configuredRender.Value.Render) != null;
    }

    public static string? Build(
        string hubBaseUrl,
        StacItem item,
        string assetKey,
        string? renderId = null)
    {
        var configuredRender = FindConfiguredRender(item.Collection, renderId);
        if (configuredRender == null ||
            !CanBuild(item, assetKey, configuredRender.Value.Id))
        {
            return null;
        }

        var render = configuredRender.Value.Render;
        var sourceUrl = ResolveSourceUrl(item, render);
        if (sourceUrl == null)
            return null;

        var parameters = new List<(string Key, string Value)>
        {
            ("url", sourceUrl),
            ("title", render.Title ?? configuredRender.Value.Id)
        };

        if (render.Assets != null)
        {
            parameters.AddRange(render.Assets.Select(asset => ("assets", asset)));
        }

        if (render.BandIndexes != null)
        {
            parameters.AddRange(render.BandIndexes.Select(
                bandIndex => ("bidx", bandIndex.ToString(CultureInfo.InvariantCulture))));
        }

        parameters.AddRange(EnumerateRescales(render.Rescale)
            .Select(rescale => ("rescale", rescale)));

        var nodata = GetScalarValue(render.Nodata);
        if (nodata != null)
            parameters.Add(("nodata", nodata));
        if (render.Colormap != null)
            parameters.Add(("colormap", JsonSerializer.Serialize(render.Colormap)));
        if (!string.IsNullOrWhiteSpace(render.Variable))
            parameters.Add(("variable", render.Variable));
        if (!string.IsNullOrWhiteSpace(render.ColormapName))
            parameters.Add(("colormap_name", render.ColormapName));
        if (!string.IsNullOrWhiteSpace(render.ColorFormula))
            parameters.Add(("color_formula", render.ColorFormula));
        if (!string.IsNullOrWhiteSpace(render.Expression))
            parameters.Add(("expression", render.Expression));
        if (render.Reference.HasValue)
            parameters.Add(("reference", render.Reference.Value ? "true" : "false"));

        parameters.Add(("id", configuredRender.Value.Id));

        var endpoint = string.IsNullOrWhiteSpace(render.Variable)
            ? "core/stac/tiles/WebMercatorQuad/{z}/{x}/{y}@1x"
            : "xarray/tiles/{z}/{x}/{y}@1x";
        var query = string.Join("&", parameters.Select(parameter =>
            $"{parameter.Key}={Encode(parameter.Value)}"));

        return $"{hubBaseUrl.TrimEnd('/')}/titiler/{endpoint}?{query}";
    }

    private static (string Id, RenderConfig Render)? FindConfiguredRender(
        string? collection,
        string? renderId)
    {
        if (string.IsNullOrWhiteSpace(collection) ||
            !CollectionRenderingConfig.Value.TryGetValue(collection, out var collectionConfig) ||
            collectionConfig.Renders == null ||
            collectionConfig.Renders.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(renderId) &&
            collectionConfig.Renders.TryGetValue(renderId, out var selectedRender))
        {
            return (renderId, selectedRender);
        }

        var firstRender = collectionConfig.Renders.First();
        return (firstRender.Key, firstRender.Value);
    }

    private static string? ResolveSourceUrl(StacItem item, RenderConfig render)
    {
        if (string.IsNullOrWhiteSpace(render.Variable))
            return string.IsNullOrWhiteSpace(item.SelfLink) ? null : item.SelfLink;

        var firstAsset = render.Assets?.FirstOrDefault();
        return firstAsset != null &&
               item.Assets?.TryGetValue(firstAsset, out var asset) == true &&
               !string.IsNullOrWhiteSpace(asset.Href)
            ? asset.Href
            : null;
    }

    private static IEnumerable<string> EnumerateRescales(JsonElement? rescale)
    {
        if (!rescale.HasValue || rescale.Value.ValueKind != JsonValueKind.Array)
            yield break;

        var values = rescale.Value.EnumerateArray().ToList();
        if (values.Count == 0)
            yield break;

        if (values[0].ValueKind == JsonValueKind.Array)
        {
            foreach (var pairElement in values)
            {
                var pair = pairElement.EnumerateArray().ToList();
                if (pair.Count >= 2)
                    yield return $"{GetNumber(pair[0])},{GetNumber(pair[1])}";
            }
        }
        else if (values.Count >= 2)
        {
            yield return $"{GetNumber(values[0])},{GetNumber(values[1])}";
        }
    }

    private static string? GetScalarValue(JsonElement? element)
    {
        if (!element.HasValue)
            return null;

        return element.Value.ValueKind switch
        {
            JsonValueKind.String => element.Value.GetString(),
            JsonValueKind.Number => element.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static string GetNumber(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number
            ? element.GetRawText()
            : Convert.ToDouble(element.GetString(), CultureInfo.InvariantCulture)
                .ToString("0.################", CultureInfo.InvariantCulture);

    private static IReadOnlyDictionary<string, CollectionConfig>
        LoadCollectionRenderingConfig()
    {
        using var stream = typeof(TitilerXyzUrlBuilder).Assembly
            .GetManifestResourceStream(RenderConfigResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded TiTiler render configuration '{RenderConfigResourceName}' was not found.");

        return JsonSerializer.Deserialize<Dictionary<string, CollectionConfig>>(stream)
               ?? new Dictionary<string, CollectionConfig>();
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);

    private sealed record CollectionConfig(
        [property: JsonPropertyName("renders")]
        Dictionary<string, RenderConfig>? Renders);

    private sealed record RenderConfig(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("assets")] List<string>? Assets,
        [property: JsonPropertyName("bidx")] List<int>? BandIndexes,
        [property: JsonPropertyName("rescale")] JsonElement? Rescale,
        [property: JsonPropertyName("nodata")] JsonElement? Nodata,
        [property: JsonPropertyName("colormap")]
        Dictionary<string, string>? Colormap,
        [property: JsonPropertyName("variable")] string? Variable,
        [property: JsonPropertyName("colormap_name")] string? ColormapName,
        [property: JsonPropertyName("color_formula")] string? ColorFormula,
        [property: JsonPropertyName("expression")] string? Expression,
        [property: JsonPropertyName("reference")] bool? Reference,
        [property: JsonPropertyName("quicklook_georeference")]
        string? QuicklookGeoreference,
        [property: JsonPropertyName("auth")] bool? Auth);
}

namespace eodh.Models;

/// <summary>
/// A curated top-level catalogue exposed by the Search UI.
/// </summary>
public sealed record CatalogRoot(string Kind, string DisplayName, string Path)
{
    public static CatalogRoot Public { get; } = new(
        "public", "Public", "/api/catalogue/stac/catalogs/public");

    public static CatalogRoot Commercial { get; } = new(
        "commercial", "Commercial", "/api/catalogue/stac/catalogs/commercial");

    public static IReadOnlyList<CatalogRoot> All { get; } = [Public, Commercial];
}

/// <summary>
/// A collection flattened from a curated catalogue tree while retaining the
/// owning catalogue and search endpoint required for item search.
/// </summary>
public sealed record CatalogCollectionEntry(
    CatalogRoot Root,
    string ProviderLabel,
    string CatalogueUrl,
    string SearchUrl,
    StacCollection Collection)
{
    public string Identity => $"{CatalogueUrl.TrimEnd('/')}::{Collection.Id}";

    public string DisplayName => $"{ProviderLabel} — {Collection.DisplayName}";
}

namespace eodh.Models;

/// <summary>
/// Search filter parameters for STAC queries.
/// Mirrors the QGIS plugin's SearchFilters dataclass.
/// </summary>
public class SearchFilters
{
    /// <summary>Bounding box as (west, south, east, north) in WGS84.</summary>
    public double[]? Bbox { get; set; }

    /// <summary>Start of date range filter.</summary>
    public DateTimeOffset? StartDate { get; set; }

    /// <summary>End of date range filter.</summary>
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>List of collection IDs to search.</summary>
    public List<string> Collections { get; set; } = [];

    /// <summary>Maximum cloud cover percentage (0-100). Null means no filter.</summary>
    public double? MaxCloudCover { get; set; }

    /// <summary>Maximum number of results per page.</summary>
    public int Limit { get; set; } = 50;

    /// <summary>
    /// Convert to a dictionary suitable for the STAC search API POST body.
    /// Mirrors the QGIS plugin's SearchFilters.to_search_params().
    /// </summary>
    public Dictionary<string, object> ToSearchParams()
    {
        var parameters = new Dictionary<string, object>
        {
            ["limit"] = Limit
        };

        if (Bbox is { Length: 4 })
        {
            parameters["bbox"] = Bbox;
        }

        if (StartDate.HasValue || EndDate.HasValue)
        {
            var start = StartDate?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "..";
            var end = EndDate?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "..";
            parameters["datetime"] = $"{start}/{end}";
        }

        if (Collections.Count > 0)
        {
            parameters["collections"] = Collections;
        }

        // Cloud cover filter via CQL2 if supported
        if (MaxCloudCover.HasValue)
        {
            parameters["filter"] = new Dictionary<string, object>
            {
                ["op"] = "<=",
                ["args"] = new object[]
                {
                    new Dictionary<string, string> { ["property"] = "eo:cloud_cover" },
                    MaxCloudCover.Value
                }
            };
            parameters["filter-lang"] = "cql2-json";
        }

        return parameters;
    }
}

namespace eodh.Tools;

/// <summary>
/// Pure formatting utilities for result item display strings.
/// </summary>
internal static class ResultFormat
{
    public static string? FormatOverlap(double? overlapPercent)
    {
        if (!overlapPercent.HasValue) return null;

        var value = overlapPercent.Value;
        if (value > 0 && value < 1.0)
            return "< 1% overlap";

        return $"{value:F0}% overlap";
    }

    public static string? FormatLicense(string? license) =>
        !string.IsNullOrWhiteSpace(license) ? $"License: {license}" : null;

    public static string? FormatAccuracy(double? geometricRmse) =>
        geometricRmse.HasValue ? $"RMSE: {geometricRmse.Value:F1} m" : null;
}

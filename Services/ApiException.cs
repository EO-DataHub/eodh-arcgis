using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace eodh.Services;

public enum ApiErrorCategory
{
    Authentication,
    Authorization,
    LinkedProviderMissing,
    Validation,
    RateLimit,
    Server,
    Unexpected
}

/// <summary>
/// Safe, structured representation of an EODH HTTP failure.
/// Request credentials and request bodies are deliberately never retained.
/// </summary>
public sealed class ApiException : HttpRequestException
{
    public ApiException(
        string operation,
        HttpStatusCode status,
        ApiErrorCategory category,
        string message,
        string? correlationId = null)
        : base(message, null, status)
    {
        Operation = operation;
        Status = status;
        Category = category;
        CorrelationId = correlationId;
    }

    public string Operation { get; }
    public HttpStatusCode Status { get; }
    public ApiErrorCategory Category { get; }
    public string? CorrelationId { get; }
}

internal static class ApiResponse
{
    public static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken ct,
        string? sensitiveValue = null)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        var backendMessage = ExtractBackendMessage(body);
        var status = response.StatusCode;
        var category = Categorize(status, backendMessage);
        var message = CreateSafeMessage(status, category, backendMessage);
        if (!string.IsNullOrEmpty(sensitiveValue))
            message = message.Replace(sensitiveValue, "[redacted]", StringComparison.Ordinal);
        var correlationId = GetCorrelationId(response);

        if (!string.IsNullOrWhiteSpace(correlationId))
            message += $" (reference: {correlationId})";

        throw new ApiException(operation, status, category, message, correlationId);
    }

    private static string? ExtractBackendMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            foreach (var name in new[] { "detail", "message", "error" })
            {
                if (!root.TryGetProperty(name, out var value))
                    continue;

                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Array => string.Join("; ", value.EnumerateArray()
                        .Select(FormatValidationEntry)
                        .Where(text => !string.IsNullOrWhiteSpace(text))),
                    JsonValueKind.Object => FormatValidationEntry(value),
                    _ => value.ToString()
                };
            }
        }
        catch (JsonException)
        {
            // Non-JSON bodies are intentionally not reflected into the UI.
        }

        return null;
    }

    private static string FormatValidationEntry(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
            return value.GetString() ?? string.Empty;

        if (value.ValueKind != JsonValueKind.Object)
            return value.ToString();

        var location = value.TryGetProperty("loc", out var loc)
            ? string.Join(".", loc.EnumerateArray().Select(part => part.ToString()))
            : null;
        var message = value.TryGetProperty("msg", out var msg)
            ? msg.GetString()
            : value.TryGetProperty("message", out var nestedMessage)
                ? nestedMessage.GetString()
                : null;

        return string.IsNullOrWhiteSpace(location)
            ? message ?? "Invalid request."
            : $"{location}: {message ?? "invalid value"}";
    }

    private static ApiErrorCategory Categorize(HttpStatusCode status, string? message)
    {
        if (status == HttpStatusCode.Unauthorized)
            return ApiErrorCategory.Authentication;

        if (status == HttpStatusCode.Forbidden &&
            message?.Contains("provider", StringComparison.OrdinalIgnoreCase) == true &&
            (message.Contains("link", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("credential", StringComparison.OrdinalIgnoreCase)))
            return ApiErrorCategory.LinkedProviderMissing;

        if (status == HttpStatusCode.Forbidden)
            return ApiErrorCategory.Authorization;

        if (status is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            return ApiErrorCategory.Validation;

        if ((int)status == 429)
            return ApiErrorCategory.RateLimit;

        if ((int)status >= 500)
            return ApiErrorCategory.Server;

        return ApiErrorCategory.Unexpected;
    }

    private static string CreateSafeMessage(
        HttpStatusCode status,
        ApiErrorCategory category,
        string? backendMessage)
    {
        return category switch
        {
            ApiErrorCategory.Authentication =>
                "The workspace API key is invalid or expired. Create or copy a current API key from your EODH workspace credentials page, then sign in again.",
            ApiErrorCategory.LinkedProviderMissing =>
                backendMessage ?? "Link your commercial provider account in EODH Workspace settings before requesting a quote.",
            ApiErrorCategory.Authorization =>
                backendMessage ?? "This workspace is not authorized to perform that operation.",
            ApiErrorCategory.Validation =>
                backendMessage ?? "The request contains an invalid or missing field.",
            ApiErrorCategory.RateLimit =>
                "EODH is receiving too many requests. Wait briefly and retry.",
            ApiErrorCategory.Server =>
                $"EODH could not complete the request (HTTP {(int)status}). Please retry.",
            _ => backendMessage ?? $"EODH returned HTTP {(int)status}."
        };
    }

    private static string? GetCorrelationId(HttpResponseMessage response)
    {
        foreach (var name in new[] { "x-correlation-id", "x-request-id", "trace-id" })
        {
            if (response.Headers.TryGetValues(name, out var values))
                return values.FirstOrDefault();
        }

        return null;
    }
}

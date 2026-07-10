using System.Text.Json;

namespace eodh.Models;

/// <summary>
/// A commercial order record discovered in the authenticated workspace STAC tree.
/// </summary>
public sealed record WorkspaceCommercialRecord(
    string ProviderLabel,
    StacCollection Collection,
    StacItem Item)
{
    public string Status =>
        FirstString(Item.Properties?.OrderStatus, "order_status", "order:state", "status")
        ?? "unknown";

    public string? OrderId =>
        FirstString(Item.Properties?.OrderId, "order_id", "orderId");

    public string? OrderDate =>
        FirstString(Item.Properties?.OrderDate, "order_date", "ordered", "ordered_at");

    public string? Message =>
        FirstString(Item.Properties?.OrderMessage, "order_message", "failure_message", "message", "detail");

    public string? Created => Item.Properties?.Created;
    public string? Updated => Item.Properties?.Updated;

    public bool IsCompleted => Status.Equals("completed", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("complete", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("succeeded", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("fulfilled", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("delivered", StringComparison.OrdinalIgnoreCase) ||
        Status.Equals("available", StringComparison.OrdinalIgnoreCase);

    private string? FirstString(string? explicitValue, params string[] extensionNames)
    {
        if (!string.IsNullOrWhiteSpace(explicitValue))
            return explicitValue;

        var extensionData = Item.Properties?.ExtensionData;
        if (extensionData == null)
            return null;

        foreach (var name in extensionNames)
        {
            if (!extensionData.TryGetValue(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString();

            if (value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                return value.ToString();
        }

        return null;
    }
}

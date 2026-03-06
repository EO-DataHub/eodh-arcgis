namespace eodh.Models;

/// <summary>
/// Organisational workspace information from EODH.
/// </summary>
public record WorkspaceInfo(
    string Id,
    string Name,
    string? Description,
    List<WorkspaceMember> Members,
    List<WorkspaceAsset> Assets
);

/// <summary>
/// A member within an organisational workspace.
/// </summary>
public record WorkspaceMember(
    string Username,
    string Role // e.g. "owner", "member", "viewer"
);

/// <summary>
/// An asset owned by or shared within a workspace.
/// </summary>
public record WorkspaceAsset(
    string Id,
    string Name,
    string? CollectionId,
    string? ItemId,
    string Status, // e.g. "available", "pending", "purchased"
    DateTimeOffset? PurchasedAt
);


namespace RevitSapSync.Core.Models;

/// <summary>
/// One per-element sync failure, carrying enough to locate the element and explain the
/// failure - unlike a flattened string, this lets a results view group/filter by SAP ID or
/// Revit element instead of just displaying free text.
/// </summary>
public sealed record SyncError
{
    public required string SapId { get; init; }
    public ElementReference? RevitElement { get; init; }
    public required string Message { get; init; }
}

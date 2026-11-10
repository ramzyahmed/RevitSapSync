namespace RevitSapSync.Core.Models;

/// <summary>Outcome of one "delete unmapped Revit elements" run.</summary>
public sealed class ElementDeletionResult
{
    public required List<ElementReference> Deleted { get; init; }
    public required List<SyncError> Errors { get; init; }
}

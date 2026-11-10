namespace RevitSapSync.Core.Models;

/// <summary>Outcome of one "create Revit elements for unmapped SAP frames" run.</summary>
public sealed class ElementCreationResult
{
    public required List<ElementReference> Created { get; init; }

    /// <summary>Frames whose required family failed upfront validation - never created, no placeholder used.</summary>
    public required List<SkippedCreation> Skipped { get; init; }

    public required List<SyncError> Errors { get; init; }
}

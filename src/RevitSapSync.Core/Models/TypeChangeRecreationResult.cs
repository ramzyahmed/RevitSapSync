namespace RevitSapSync.Core.Models;

/// <summary>
/// Outcome of one "recreate mapped elements whose category changed" run - a Revit element
/// that was, say, a Column but whose SAP counterpart's geometry now classifies it as a
/// Beam: the old instance is deleted and a new one created in the correct category, only
/// once the new instance is fully set up (never before, matching the reference's
/// recreate-then-delete ordering).
/// </summary>
public sealed class TypeChangeRecreationResult
{
    public required List<ElementReference> Created { get; init; }
    public required List<ElementReference> Deleted { get; init; }

    /// <summary>Frames whose required family failed upfront validation - the old instance is left untouched, nothing is deleted.</summary>
    public required List<SkippedCreation> Skipped { get; init; }

    public required List<SyncError> Errors { get; init; }
}

namespace RevitSapSync.Core.Models;

/// <summary>Outcome of one section-size sync run over a set of mapped pairs.</summary>
public sealed class SectionSyncResult
{
    /// <summary>Elements whose Revit FamilySymbol was actually changed.</summary>
    public required List<ElementReference> Updated { get; init; }

    /// <summary>Elements already on the correct symbol - parameters reapplied, symbol untouched.</summary>
    public required List<ElementReference> Unchanged { get; init; }

    /// <summary>
    /// SAP frames whose section shape isn't covered by the current provisioners (or whose
    /// family isn't loaded in the project) - assigned a "NotFound(...)" placeholder symbol
    /// so the sync never hard-fails on an unrecognized section, exactly as the reference
    /// does, but flagged here so the caller can surface it for review.
    /// </summary>
    public required List<ElementReference> UsingPlaceholderSymbol { get; init; }

    /// <summary>Per-element failures, continue-on-error (matches the reference's per-item try/catch/continue).</summary>
    public required List<SyncError> Errors { get; init; }

    /// <summary>SAP section name (normalized) -> the Revit FamilySymbol type name provisioned for it.</summary>
    public required Dictionary<string, string> SectionAssignments { get; init; }

    /// <summary>Why each section using a "NotFound(...)" placeholder ended up there - keyed by SAP section name.</summary>
    public required List<PlaceholderReason> PlaceholderReasons { get; init; }
}

namespace RevitSapSync.Core.Models;

/// <summary>Outcome of one geometry-sync run over a set of mapped pairs.</summary>
public sealed class GeometrySyncResult
{
    /// <summary>Elements whose Revit geometry was actually changed.</summary>
    public required List<ElementReference> Updated { get; init; }

    /// <summary>Elements already within tolerance - parameters reapplied, geometry untouched.</summary>
    public required List<ElementReference> Unchanged { get; init; }

    /// <summary>
    /// Pairs whose mapped Revit element needs to change category (Column&#8596;Beam) or
    /// column style in a way that requires a fresh family-symbol lookup - deferred until a
    /// creation-capable engine (which owns family provisioning and element creation/
    /// deletion) can act on them, rather than guessed here. Carries the full pair (not
    /// just the SAP frame) since recreating the element needs the OLD Revit element
    /// reference too, to delete it.
    /// </summary>
    public required List<SapRevitFrame> DeferredTypeChanges { get; init; }

    /// <summary>Per-element failures, continue-on-error (matches the reference's per-item try/catch/continue).</summary>
    public required List<SyncError> Errors { get; init; }
}

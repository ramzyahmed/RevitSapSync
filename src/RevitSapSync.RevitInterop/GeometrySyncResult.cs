using RevitSapSync.Core.Models;

namespace RevitSapSync.RevitInterop;

/// <summary>Outcome of one <see cref="IGeometrySyncEngine"/> run over a set of mapped pairs.</summary>
public sealed class GeometrySyncResult
{
    /// <summary>Elements whose Revit geometry was actually changed.</summary>
    public required List<ElementReference> Updated { get; init; }

    /// <summary>Elements already within tolerance - parameters reapplied, geometry untouched.</summary>
    public required List<ElementReference> Unchanged { get; init; }

    /// <summary>
    /// SAP frames whose mapped Revit element needs to change category (Column&#8596;Beam)
    /// or column style in a way that requires a fresh family-symbol lookup - deferred
    /// until the section-sync engine (which owns family provisioning) is available,
    /// rather than guessed here.
    /// </summary>
    public required List<Frame> DeferredTypeChanges { get; init; }

    /// <summary>Per-element failures, continue-on-error (matches the reference's per-item try/catch/continue).</summary>
    public required List<string> Errors { get; init; }
}

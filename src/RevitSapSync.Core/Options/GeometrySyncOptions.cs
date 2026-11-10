namespace RevitSapSync.Core.Options;

/// <summary>
/// Tolerances/toggles for the Revit-side geometry sync engine. Named equivalent of the
/// reference implementation's inline magic numbers.
/// </summary>
public sealed class GeometrySyncOptions
{
    /// <summary>
    /// "Did it move enough to bother regenerating Revit geometry", as a fraction of the
    /// Revit element's own length (reference's <c>margin = 0.001</c>, i.e. 0.1%).
    /// </summary>
    public double MovementTolerancePercent { get; set; } = 0.001;

    public int TransactionChunkSize { get; set; } = 1000;

    /// <summary>Update structural framing (beam) cross-section rotation to match SAP's Beta angle. Independent of <see cref="UpdateColumnRotation"/>.</summary>
    public bool UpdateBeamRotation { get; set; } = true;

    /// <summary>Update structural column rotation (vertical: about its own axis; slanted: cross-section rotation) to match SAP's Beta angle. Independent of <see cref="UpdateBeamRotation"/>.</summary>
    public bool UpdateColumnRotation { get; set; } = true;

    /// <summary>Update insertion-point justification (y/z Justification, Start/End Extension) for beams and columns, where the corresponding parameter exists.</summary>
    public bool UpdateJustificationReferences { get; set; }
}

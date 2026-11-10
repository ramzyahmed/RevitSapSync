namespace RevitSapSync.RevitInterop;

/// <summary>
/// Tolerances/toggles for <see cref="IGeometrySyncEngine"/>. Named equivalents of the
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
}

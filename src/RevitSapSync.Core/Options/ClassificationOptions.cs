namespace RevitSapSync.Core.Options;

/// <summary>
/// Tolerances for <see cref="Services.IMemberClassificationService"/>. Named,
/// configurable equivalents of the reference implementation's inline constants
/// (4 degrees / 0.05 degrees / 20 degrees).
/// </summary>
public sealed class ClassificationOptions
{
    public double ColumnInclinationToleranceDegrees { get; set; } = 4.0;
    public double VerticalColumnToleranceDegrees { get; set; } = 0.05;
    public double BracingInclinationToleranceDegrees { get; set; } = 20.0;
}

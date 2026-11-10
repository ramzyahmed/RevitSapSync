namespace RevitSapSync.Core.Models;

/// <summary>
/// A SAP frame for which the geometry-fallback mapping phase found more than one
/// equally plausible Revit candidate and deliberately declined to guess. Surfaced to the
/// user for manual review rather than silently picking one.
/// </summary>
public sealed class AmbiguousMappingCandidate
{
    public required Frame SapFrame { get; init; }
    public required List<Frame> Candidates { get; init; }
    public required string Reason { get; init; }
}

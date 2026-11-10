namespace RevitSapSync.Core.Models;

/// <summary>
/// A SAP frame that was NOT created in Revit because its required family failed to
/// resolve during the creation engine's upfront validation pass - creation never falls
/// back to a "NotFound" placeholder the way section-sync does, since inventing a
/// brand-new element with a wrong/stand-in profile is worse than simply not creating it.
/// </summary>
public sealed record SkippedCreation
{
    public required string SapId { get; init; }
    public required string SectionName { get; init; }
    public required string Reason { get; init; }
}

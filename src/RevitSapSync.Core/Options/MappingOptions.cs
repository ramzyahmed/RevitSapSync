namespace RevitSapSync.Core.Options;

/// <summary>
/// Tolerances and toggles for <see cref="Services.IFrameMappingEngine"/>. Named,
/// configurable equivalents of the reference implementation's inline magic numbers
/// (0.2 length-tolerance margin, 10-degree axis alignment, 5% tie-break threshold).
/// </summary>
public sealed class MappingOptions
{
    /// <summary>Allowed length difference between candidates, as a fraction of the SAP frame's length (0.2 = 20%).</summary>
    public double LengthTolerancePercent { get; set; } = 0.20;

    public double AxisAlignmentToleranceDegrees { get; set; } = 10.0;

    /// <summary>Candidates within this percentage of the best distance are treated as a near-tie and disambiguated further.</summary>
    public double TieBreakPercentThreshold { get; set; } = 5.0;

    /// <summary>
    /// When false (default), a SAP ID present on more than one Revit element only maps
    /// to the first Revit element found; the rest are reported via
    /// <see cref="Models.MappingResult.DuplicateRevitSapIds"/>. The "one SAP ID maps to
    /// many Revit elements" behavior is a later-milestone toggle, not yet implemented.
    /// </summary>
    public bool AllowOneSapIdToManyRevitElements { get; set; }

    /// <summary>
    /// When true, an ID-matched SAP frame classified Beam/HorizontalBracing/VerticalBracing
    /// whose Revit counterpart is actually a Column (or SlantedColumn) - or vice versa - is
    /// reclassified to the Revit element's real category instead of being rejected as an
    /// incompatible pair. Revit has no in-place category change, so this never alters the
    /// Revit element itself; it only makes downstream section/geometry sync treat the pair
    /// consistently as whatever the Revit element actually is (e.g. provisioning a column
    /// symbol instead of erroring when a beam symbol is rejected by an existing column
    /// instance). Off by default to preserve existing behavior - such pairs are rejected
    /// and left unmapped unless this is explicitly opted into.
    /// </summary>
    public bool ReclassifyMemberTypeOnCategoryMismatch { get; set; }

    /// <summary>
    /// When true, a genuine Beam&#8596;Column category mismatch on an ID-matched pair is
    /// let through unreconciled - the SAP frame keeps its own classified MemberType rather
    /// than adopting the Revit element's (that's what <see cref="ReclassifyMemberTypeOnCategoryMismatch"/>
    /// does) - so the pair reaches geometry sync, which already detects this exact
    /// condition and can recreate the element in the correct category (delete the old
    /// instance, create a new one). Takes priority over
    /// <see cref="ReclassifyMemberTypeOnCategoryMismatch"/> when both are true, since
    /// recreation actually fixes the model rather than working around the mismatch.
    /// Callers should set this equal to whether geometry sync (and therefore recreation)
    /// is actually going to run - letting a mismatch through with no one able to act on it
    /// would just turn a clear rejection into a silent geometry-sync error instead.
    /// </summary>
    public bool AllowCategoryChangeForRecreation { get; set; }
}

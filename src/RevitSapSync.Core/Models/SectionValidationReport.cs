namespace RevitSapSync.Core.Models;

/// <summary>
/// One row of the SAP-section vs Revit-type reconciliation table: reimplements the
/// reference project's <c>SapUtils.validation</c> per-type comparison (count, length,
/// cut length, tonnage on the Revit side; count, length, tonnage, cross-section area on
/// the SAP side). <see cref="SapSectionName"/> is "NotFound" when no SAP section maps to
/// this Revit type, matching the reference's exact sentinel.
/// </summary>
public sealed record SectionValidationRecord
{
    public required string RevitTypeName { get; init; }
    public required int RevitMemberCount { get; init; }
    public required double RevitTotalLengthMeters { get; init; }
    public required double RevitTotalCutLengthMeters { get; init; }
    public required double RevitTotalTonnage { get; init; }

    public required string SapSectionName { get; init; }
    public required int SapMemberCount { get; init; }
    public required double SapTotalLengthMeters { get; init; }
    public required double SapTotalTonnage { get; init; }
    public required double SapCrossSectionAreaMm2 { get; init; }
}

/// <summary>
/// One row of Revit elements grouped by type that have no corresponding SAP frame at all
/// (reference's <c>UnMatchingRevitBeams</c>/<c>UnMatchingRevitColumns</c> sheets) - Revit-side
/// figures only, since there is no SAP counterpart to compare against.
/// </summary>
public sealed record UnmappedRevitTypeRecord
{
    public required string RevitTypeName { get; init; }
    public required int RevitMemberCount { get; init; }
    public required double RevitTotalLengthMeters { get; init; }
    public required double RevitTotalCutLengthMeters { get; init; }
    public required double RevitTotalTonnage { get; init; }
}

/// <summary>Full validation report, one collection per reference Excel sheet.</summary>
public sealed class SectionValidationReport
{
    public required List<SectionValidationRecord> MatchingBeams { get; init; }
    public required List<SectionValidationRecord> MatchingColumns { get; init; }
    public required List<UnmappedRevitTypeRecord> UnmatchedRevitBeams { get; init; }
    public required List<UnmappedRevitTypeRecord> UnmatchedRevitColumns { get; init; }
}

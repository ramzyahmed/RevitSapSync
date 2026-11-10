namespace RevitSapSync.Core.Models;

/// <summary>
/// Full output of <see cref="Services.IFrameMappingEngine"/> - every SAP/Revit frame is
/// accounted for in exactly one of these lists.
/// </summary>
public sealed class MappingResult
{
    public required List<SapRevitFrame> MappedPairs { get; init; }
    public required List<Frame> UnmappedSapFrames { get; init; }
    public required List<Frame> UnmappedRevitFrames { get; init; }
    public required List<AmbiguousMappingCandidate> AmbiguousCandidates { get; init; }

    /// <summary>
    /// SAP IDs that were present on more than one Revit element. Only the first Revit
    /// element (in collection order) was eligible for ID matching; the rest are reported
    /// here rather than silently dropped.
    /// </summary>
    public required List<string> DuplicateRevitSapIds { get; init; }

    /// <summary>Genuine Beam&#8596;Column mismatches on ID-matched pairs that were left unmapped rather than resolved - see <see cref="CategoryMismatchWarning"/>.</summary>
    public required List<CategoryMismatchWarning> CategoryMismatchWarnings { get; init; }
}

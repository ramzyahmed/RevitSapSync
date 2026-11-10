namespace RevitSapSync.Core.Models;

/// <summary>
/// A genuine Beam&#8596;Column category mismatch between an ID-matched SAP/Revit pair that
/// was left unmapped because neither <see cref="Options.MappingOptions.AllowCategoryChangeForRecreation"/>
/// nor <see cref="Options.MappingOptions.ReclassifyMemberTypeOnCategoryMismatch"/> was
/// enabled to resolve it. Surfaced explicitly rather than the pair just silently
/// appearing in <see cref="MappingResult.UnmappedSapFrames"/>/<see cref="MappingResult.UnmappedRevitFrames"/>
/// with no explanation.
/// </summary>
public sealed record CategoryMismatchWarning
{
    public required string SapId { get; init; }
    public required MemberType SapMemberType { get; init; }
    public required MemberType RevitMemberType { get; init; }
}

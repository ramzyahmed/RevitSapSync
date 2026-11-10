namespace RevitSapSync.Sap2000Interop;

/// <summary>
/// Result of classifying one SAP2000 frame section, mirroring the reference
/// implementation's <c>SapUtils.Sections.getSectionTypes</c> +
/// <c>getSapFrames</c>'s inline Shape/IsBUS/Lib assignment (both call the same two OAPI
/// members - <c>PropFrame.GetTypeOAPI</c> and <c>PropFrame.GetNameInPropFile</c> - so this
/// consolidates them into one lookup instead of two).
/// </summary>
public sealed record SapSectionClassification(
    string Shape,
    bool IsBuiltUp,
    string Library,
    string DimensionLookupName,
    string? TaperedEndLookupName)
{
    public bool IsTapered => TaperedEndLookupName is not null;
}

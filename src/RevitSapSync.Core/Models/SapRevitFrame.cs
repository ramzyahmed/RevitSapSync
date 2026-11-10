namespace RevitSapSync.Core.Models;

/// <summary>
/// A successfully mapped SAP/Revit pair. Unlike the reference implementation's
/// equivalent type, <see cref="RevitFrame"/> is never null here - unmapped elements are
/// tracked separately in <see cref="MappingResult"/> instead of overloading this type
/// with two meanings.
/// </summary>
public sealed class SapRevitFrame
{
    public required Frame SapFrame { get; init; }
    public required Frame RevitFrame { get; init; }
    public required MappingMethod MappedBy { get; init; }
}

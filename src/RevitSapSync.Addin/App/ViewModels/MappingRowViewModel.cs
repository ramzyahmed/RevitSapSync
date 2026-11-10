using RevitSapSync.Core.Models;

namespace RevitSapSync.App.ViewModels;

/// <summary>
/// Flattens one mapped pair / unmapped frame / ambiguous candidate into DataGrid-bindable
/// columns. Rows are immutable snapshots built once per mapping run, so this is a plain
/// class rather than an <c>ObservableObject</c> - there is nothing to notify after
/// construction.
/// </summary>
public sealed class MappingRowViewModel
{
    public required string SapId { get; init; }
    public required string SapMemberType { get; init; }
    public string RevitElementId { get; init; } = string.Empty;
    public string RevitMemberType { get; init; } = string.Empty;
    public required string Status { get; init; }
    public double? SapLength { get; init; }
    public double? RevitLength { get; init; }
    public double? LengthDeltaPercent { get; init; }

    public static MappingRowViewModel FromMappedPair(SapRevitFrame pair) => new()
    {
        SapId = pair.SapFrame.Id,
        SapMemberType = pair.SapFrame.MemberType.ToString(),
        RevitElementId = pair.RevitFrame.RevitElement?.IntegerId.ToString() ?? string.Empty,
        RevitMemberType = pair.RevitFrame.MemberType.ToString(),
        Status = pair.MappedBy == MappingMethod.Id ? "Mapped (ID)" : "Mapped (Geometry)",
        SapLength = pair.SapFrame.Length,
        RevitLength = pair.RevitFrame.Length,
        LengthDeltaPercent = pair.SapFrame.Length == 0
            ? null
            : 100.0 * Math.Abs(pair.RevitFrame.Length - pair.SapFrame.Length) / pair.SapFrame.Length,
    };

    public static MappingRowViewModel FromUnmappedSap(Frame frame) => new()
    {
        SapId = frame.Id,
        SapMemberType = frame.MemberType.ToString(),
        Status = "Unmapped (SAP)",
        SapLength = frame.Length,
    };

    public static MappingRowViewModel FromUnmappedRevit(Frame frame) => new()
    {
        SapId = string.Empty,
        SapMemberType = string.Empty,
        RevitElementId = frame.RevitElement?.IntegerId.ToString() ?? string.Empty,
        RevitMemberType = frame.MemberType.ToString(),
        Status = "Unmapped (Revit)",
        RevitLength = frame.Length,
    };

    public static MappingRowViewModel FromAmbiguous(AmbiguousMappingCandidate candidate) => new()
    {
        SapId = candidate.SapFrame.Id,
        SapMemberType = candidate.SapFrame.MemberType.ToString(),
        Status = $"Ambiguous ({candidate.Candidates.Count} candidates)",
        SapLength = candidate.SapFrame.Length,
    };
}

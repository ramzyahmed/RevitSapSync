using Autodesk.Revit.DB;
using RevitSapSync.Core.Models;
using Frame = RevitSapSync.Core.Models.Frame;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Shared post-sync reporting step used by both <c>TestSyncCommand</c> and
/// <c>MainViewModel</c>'s sync commands, so this orchestration isn't duplicated between
/// them: updates the Revit Selection Filters and (for a section sync) builds the
/// SAP-section/Revit-type validation reconciliation report.
/// </summary>
public sealed class SyncResultsPresenter
{
    private readonly SelectionFilterService _selectionFilters;
    private readonly SectionValidationReportBuilder _reportBuilder;

    public SyncResultsPresenter(SelectionFilterService selectionFilters, SectionValidationReportBuilder reportBuilder)
    {
        _selectionFilters = selectionFilters;
        _reportBuilder = reportBuilder;
    }

    /// <summary>Updates the "mapped"/"unmapped" filters - meaningful after either a geometry or a section sync.</summary>
    public void UpdateMappedUnmappedFilters(Document document, MappingResult mapping)
    {
        var mappedIds = mapping.MappedPairs.Select(p => new ElementId(p.RevitFrame.RevitElement!.IntegerId)).ToList();
        var unmappedIds = mapping.UnmappedRevitFrames.Select(f => new ElementId(f.RevitElement!.IntegerId)).ToList();

        _selectionFilters.CreateOrUpdate(document, unmappedIds, SelectionFilterService.UnmappedRevitFilterName);
        _selectionFilters.CreateOrUpdate(document, mappedIds, SelectionFilterService.MappedFilterName);
    }

    /// <summary>
    /// Updates the "sections changed"/"NotFound section" filters and builds the full
    /// validation reconciliation report - only meaningful after a section sync, since both
    /// depend on the SAP-section-to-Revit-type assignment a section sync produces.
    /// </summary>
    public SectionValidationReport PresentSectionSync(
        Document document, List<Frame> sapFrames, MappingResult mapping, SectionSyncResult beamResult, SectionSyncResult columnResult)
    {
        UpdateMappedUnmappedFilters(document, mapping);

        var sectionsChangedIds = beamResult.Updated.Concat(columnResult.Updated).Select(r => new ElementId(r.IntegerId)).ToList();
        var notFoundIds = beamResult.UsingPlaceholderSymbol.Concat(columnResult.UsingPlaceholderSymbol).Select(r => new ElementId(r.IntegerId)).ToList();

        _selectionFilters.CreateOrUpdate(document, sectionsChangedIds, SelectionFilterService.SectionsChangedFilterName);
        _selectionFilters.CreateOrUpdate(document, notFoundIds, SelectionFilterService.NotFoundSectionFilterName);

        var mappedRevitBeamIds = mapping.MappedPairs
            .Where(p => p.SapFrame.MemberType is MemberType.Beam or MemberType.HorizontalBracing or MemberType.VerticalBracing)
            .Select(p => new ElementId(p.RevitFrame.RevitElement!.IntegerId))
            .ToList();
        var mappedRevitColumnIds = mapping.MappedPairs
            .Where(p => p.SapFrame.MemberType is MemberType.Column or MemberType.SlantedColumn)
            .Select(p => new ElementId(p.RevitFrame.RevitElement!.IntegerId))
            .ToList();
        var unmappedRevitBeamIds = mapping.UnmappedRevitFrames
            .Where(f => f.MemberType is MemberType.Beam or MemberType.HorizontalBracing or MemberType.VerticalBracing)
            .Select(f => new ElementId(f.RevitElement!.IntegerId))
            .ToList();
        var unmappedRevitColumnIds = mapping.UnmappedRevitFrames
            .Where(f => f.MemberType is MemberType.Column or MemberType.SlantedColumn)
            .Select(f => new ElementId(f.RevitElement!.IntegerId))
            .ToList();

        return _reportBuilder.Build(
            document, sapFrames, mappedRevitBeamIds, mappedRevitColumnIds, unmappedRevitBeamIds, unmappedRevitColumnIds,
            beamResult.SectionAssignments, columnResult.SectionAssignments);
    }
}

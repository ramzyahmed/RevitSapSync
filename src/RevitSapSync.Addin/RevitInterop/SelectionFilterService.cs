using Autodesk.Revit.DB;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Reimplements the reference project's <c>RevitUtils.CreateOrUpdateSelectionFilter</c>:
/// find-or-create a named <see cref="SelectionFilterElement"/> and set its element-id set,
/// so users can pick "Unmapped", "Mapped", "Sections Changed", or "Not Found Section" from
/// Revit's own Selection Filter dropdown after a sync run. The reference's view-filter
/// graphic-override step exists only as commented-out dead code there, so it is
/// deliberately not ported here - this only populates the named filter itself.
/// </summary>
public sealed class SelectionFilterService
{
    /// <summary>Reused verbatim from the reference project's HardCodedData.MembersInRevitNotInSapView.</summary>
    public const string UnmappedRevitFilterName = "MembersInRevitNotInSap";

    /// <summary>Reused verbatim from the reference project's HardCodedData.UpdatedRevitSectionsView.</summary>
    public const string SectionsChangedFilterName = "MembersSectionsChanged";

    /// <summary>New filter (not present in the reference) covering Revit elements mapped to a SAP frame.</summary>
    public const string MappedFilterName = "MembersMappedToSap";

    /// <summary>New filter (not present in the reference) covering elements assigned a "NotFound(...)" placeholder section.</summary>
    public const string NotFoundSectionFilterName = "MembersNotFoundSection";

    public void CreateOrUpdate(Document document, ICollection<ElementId> elementIds, string filterName)
    {
        using var transaction = new Transaction(document, "RevitSapSync: Create/Update Selection Filter");
        transaction.Start();

        var filterElement = new FilteredElementCollector(document)
            .OfClass(typeof(SelectionFilterElement))
            .Cast<SelectionFilterElement>()
            .FirstOrDefault(f => f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));

        filterElement ??= SelectionFilterElement.Create(document, filterName);
        filterElement.SetElementIds(elementIds);

        transaction.Commit();
    }
}

using Autodesk.Revit.DB;
using RevitSapSync.Core.Models;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Assigns SAP-derived section sizes onto already-mapped Revit elements. Reimplements
/// <c>Beam.updateRevitSectionsForBeams2</c>/<c>Column.updateRevitSectionsForColumns2</c>,
/// including the "NotFound" placeholder fallback for unrecognized sections. Must only be
/// called from inside a valid Revit API context, since it opens its own transactions.
/// </summary>
public interface ISectionSyncEngine
{
    /// <param name="previewOnly">
    /// When true, runs the full sync (including provisioning any FamilySymbols a
    /// placeholder needs) but rolls back the whole transaction group instead of
    /// assimilating it, so the returned counts reflect what would happen without
    /// anything actually persisting to the document.
    /// </param>
    SectionSyncResult SyncBeamSections(Document document, List<SapRevitFrame> mappedBeams, bool previewOnly = false);

    SectionSyncResult SyncColumnSections(Document document, List<SapRevitFrame> mappedColumns, bool previewOnly = false);
}

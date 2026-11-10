using Autodesk.Revit.DB;
using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Writes SAP-derived geometry (start/end points, rotation) onto already-mapped Revit
/// elements, including the slanted-column Column-Style dance. Must only be called from
/// inside a valid Revit API context (a command's <c>Execute</c>, or through a dispatcher
/// analogous to <see cref="RevitApiDispatcher"/>) since it opens its own transactions.
/// </summary>
public interface IGeometrySyncEngine
{
    /// <param name="previewOnly">When true, runs the full sync but rolls back the transaction group instead of assimilating it.</param>
    GeometrySyncResult SyncBeamGeometry(Document document, List<SapRevitFrame> mappedBeams, GeometrySyncOptions options, bool previewOnly = false);

    GeometrySyncResult SyncColumnGeometry(Document document, List<SapRevitFrame> mappedColumns, GeometrySyncOptions options, bool previewOnly = false);
}

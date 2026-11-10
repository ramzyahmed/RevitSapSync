using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;

namespace RevitSapSync.Core.Services;

/// <summary>
/// Thread-safe entry point ViewModels call for geometry-sync writes. Concrete
/// implementation lives in RevitInterop and marshals onto Revit's main thread via
/// <c>ExternalEvent</c> - see <see cref="IRevitFrameReadDispatcher"/> for the same pattern
/// and why it's required for a modeless WPF window.
/// </summary>
public interface IGeometrySyncDispatcher
{
    Task<GeometrySyncResult> SyncBeamGeometryAsync(List<SapRevitFrame> mappedBeams, GeometrySyncOptions options, CancellationToken cancellationToken);

    Task<GeometrySyncResult> SyncColumnGeometryAsync(List<SapRevitFrame> mappedColumns, GeometrySyncOptions options, CancellationToken cancellationToken);
}

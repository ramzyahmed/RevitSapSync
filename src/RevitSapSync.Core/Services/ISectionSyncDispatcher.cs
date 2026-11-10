using RevitSapSync.Core.Models;

namespace RevitSapSync.Core.Services;

/// <summary>
/// Thread-safe entry point ViewModels call for section-size sync writes. Concrete
/// implementation lives in RevitInterop and marshals onto Revit's main thread via
/// <c>ExternalEvent</c> - see <see cref="IGeometrySyncDispatcher"/> for the same pattern.
/// </summary>
public interface ISectionSyncDispatcher
{
    Task<SectionSyncResult> SyncBeamSectionsAsync(List<SapRevitFrame> mappedBeams, CancellationToken cancellationToken);

    Task<SectionSyncResult> SyncColumnSectionsAsync(List<SapRevitFrame> mappedColumns, CancellationToken cancellationToken);
}

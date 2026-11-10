using System.Collections.Concurrent;
using Autodesk.Revit.UI;
using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;
using RevitSapSync.Core.Services;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Must be constructed once, on Revit's main thread, during <c>IExternalApplication.OnStartup</c>
/// - see <see cref="RevitApiDispatcher"/> for the same requirement and rationale.
/// </summary>
public sealed class GeometrySyncDispatcher : IGeometrySyncDispatcher, IExternalEventHandler
{
    private enum SyncKind { Beams, Columns }

    private sealed record PendingRequest(
        SyncKind Kind,
        List<SapRevitFrame> Pairs,
        GeometrySyncOptions Options,
        CancellationToken CancellationToken,
        TaskCompletionSource<GeometrySyncResult> CompletionSource);

    private readonly ExternalEvent _externalEvent;
    private readonly ConcurrentQueue<PendingRequest> _pending = new();
    private readonly IGeometrySyncEngine _engine;
    private readonly IRevitContextAccessor _contextAccessor;

    public GeometrySyncDispatcher(IGeometrySyncEngine engine, IRevitContextAccessor contextAccessor)
    {
        _engine = engine;
        _contextAccessor = contextAccessor;
        _externalEvent = ExternalEvent.Create(this);
    }

    public Task<GeometrySyncResult> SyncBeamGeometryAsync(List<SapRevitFrame> mappedBeams, GeometrySyncOptions options, CancellationToken cancellationToken) =>
        Enqueue(SyncKind.Beams, mappedBeams, options, cancellationToken);

    public Task<GeometrySyncResult> SyncColumnGeometryAsync(List<SapRevitFrame> mappedColumns, GeometrySyncOptions options, CancellationToken cancellationToken) =>
        Enqueue(SyncKind.Columns, mappedColumns, options, cancellationToken);

    private Task<GeometrySyncResult> Enqueue(SyncKind kind, List<SapRevitFrame> pairs, GeometrySyncOptions options, CancellationToken cancellationToken)
    {
        var request = new PendingRequest(kind, pairs, options, cancellationToken,
            new TaskCompletionSource<GeometrySyncResult>(TaskCreationOptions.RunContinuationsAsynchronously));
        _pending.Enqueue(request);
        _externalEvent.Raise();
        return request.CompletionSource.Task;
    }

    public void Execute(UIApplication app)
    {
        var document = _contextAccessor.CurrentDocument;
        while (_pending.TryDequeue(out var request))
        {
            if (request.CancellationToken.IsCancellationRequested)
            {
                request.CompletionSource.TrySetCanceled(request.CancellationToken);
                continue;
            }

            try
            {
                if (document is null)
                    throw new InvalidOperationException("No active Revit document. Open a document before syncing geometry.");

                var result = request.Kind == SyncKind.Beams
                    ? _engine.SyncBeamGeometry(document, request.Pairs, request.Options)
                    : _engine.SyncColumnGeometry(document, request.Pairs, request.Options);

                request.CompletionSource.TrySetResult(result);
            }
            catch (Exception ex)
            {
                request.CompletionSource.TrySetException(ex);
            }
        }
    }

    public string GetName() => "RevitSapSync Geometry Sync";
}

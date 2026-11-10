using System.Collections.Concurrent;
using Autodesk.Revit.UI;
using RevitSapSync.Core.Models;
using RevitSapSync.Core.Services;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Must be constructed once, on Revit's main thread, during <c>IExternalApplication.OnStartup</c>
/// - see <see cref="RevitApiDispatcher"/> for the same requirement and rationale.
/// </summary>
public sealed class SectionSyncDispatcher : ISectionSyncDispatcher, IExternalEventHandler
{
    private enum SyncKind { Beams, Columns }

    private sealed record PendingRequest(
        SyncKind Kind,
        List<SapRevitFrame> Pairs,
        CancellationToken CancellationToken,
        TaskCompletionSource<SectionSyncResult> CompletionSource);

    private readonly ExternalEvent _externalEvent;
    private readonly ConcurrentQueue<PendingRequest> _pending = new();
    private readonly ISectionSyncEngine _engine;
    private readonly IRevitContextAccessor _contextAccessor;

    public SectionSyncDispatcher(ISectionSyncEngine engine, IRevitContextAccessor contextAccessor)
    {
        _engine = engine;
        _contextAccessor = contextAccessor;
        _externalEvent = ExternalEvent.Create(this);
    }

    public Task<SectionSyncResult> SyncBeamSectionsAsync(List<SapRevitFrame> mappedBeams, CancellationToken cancellationToken) =>
        Enqueue(SyncKind.Beams, mappedBeams, cancellationToken);

    public Task<SectionSyncResult> SyncColumnSectionsAsync(List<SapRevitFrame> mappedColumns, CancellationToken cancellationToken) =>
        Enqueue(SyncKind.Columns, mappedColumns, cancellationToken);

    private Task<SectionSyncResult> Enqueue(SyncKind kind, List<SapRevitFrame> pairs, CancellationToken cancellationToken)
    {
        var request = new PendingRequest(kind, pairs, cancellationToken,
            new TaskCompletionSource<SectionSyncResult>(TaskCreationOptions.RunContinuationsAsynchronously));
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
                    throw new InvalidOperationException("No active Revit document. Open a document before syncing sections.");

                var result = request.Kind == SyncKind.Beams
                    ? _engine.SyncBeamSections(document, request.Pairs)
                    : _engine.SyncColumnSections(document, request.Pairs);

                request.CompletionSource.TrySetResult(result);
            }
            catch (Exception ex)
            {
                request.CompletionSource.TrySetException(ex);
            }
        }
    }

    public string GetName() => "RevitSapSync Section Sync";
}

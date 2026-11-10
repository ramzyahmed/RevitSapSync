using System.Collections.Concurrent;
using Autodesk.Revit.UI;
using RevitSapSync.Core.Models;
using RevitSapSync.Core.Services;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// The thread-safe entry point ViewModels call for Revit reads. The WPF window is
/// modeless (opened by a command that has already returned), so every Revit API touch
/// triggered from a button click must be marshaled onto Revit's main thread via an
/// <see cref="ExternalEvent"/> - calling <see cref="IRevitFrameReader"/> directly from a
/// UI-thread event handler would throw.
/// <para>
/// Must be constructed once, on Revit's main thread, during <c>IExternalApplication.OnStartup</c>
/// (an <see cref="ExternalEvent"/> is only valid if created there) and registered as a DI
/// singleton resolved eagerly at that point - never lazily from a background thread.
/// </para>
/// </summary>
public sealed class RevitApiDispatcher : IRevitFrameReadDispatcher, IExternalEventHandler
{
    private readonly ExternalEvent _externalEvent;
    private readonly ConcurrentQueue<PendingReadRequest> _pending = new();
    private readonly IRevitFrameReader _frameReader;
    private readonly IRevitContextAccessor _contextAccessor;

    public RevitApiDispatcher(IRevitFrameReader frameReader, IRevitContextAccessor contextAccessor)
    {
        _frameReader = frameReader;
        _contextAccessor = contextAccessor;
        _externalEvent = ExternalEvent.Create(this);
    }

    public Task<List<Frame>> ReadFramesAsync(FrameReadOptions options, IProgress<ReadProgress>? progress, CancellationToken cancellationToken)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        var request = new PendingReadRequest(
            options, progress, cancellationToken,
            new TaskCompletionSource<List<Frame>>(TaskCreationOptions.RunContinuationsAsynchronously));

        _pending.Enqueue(request);
        _externalEvent.Raise();
        return request.CompletionSource.Task;
    }

    public void Execute(UIApplication app)
    {
        // Refreshed on every dispatched call (not just once at command-open time), since
        // the window is modeless - selection state can change between opening it and
        // clicking "Read Revit Model" later, and FrameReadOptions.SelectedOnly needs the
        // live selection at read time, not whatever it was when the window first opened.
        _contextAccessor.CurrentUIDocument = app.ActiveUIDocument;

        while (_pending.TryDequeue(out var request))
        {
            if (request.CancellationToken.IsCancellationRequested)
            {
                request.CompletionSource.TrySetCanceled(request.CancellationToken);
                continue;
            }

            try
            {
                var frames = _frameReader.ReadFrames(request.Options, request.Progress, request.CancellationToken);
                request.CompletionSource.TrySetResult(frames);
            }
            catch (Exception ex)
            {
                request.CompletionSource.TrySetException(ex);
            }
        }
    }

    public string GetName() => "RevitSapSync Frame Reader";

    private sealed record PendingReadRequest(
        FrameReadOptions Options,
        IProgress<ReadProgress>? Progress,
        CancellationToken CancellationToken,
        TaskCompletionSource<List<Frame>> CompletionSource);
}

using RevitSapSync.Core.Models;

namespace RevitSapSync.Core.Services;

/// <summary>
/// Reads structural framing/column elements from the active Revit document as unified
/// <see cref="Frame"/> models. Must only be called from inside a valid Revit API
/// context (a command's <c>Execute</c>, or an <see cref="IRevitFrameReadDispatcher"/>
/// callback) - never directly from a ViewModel or other UI-thread code.
/// </summary>
public interface IRevitFrameReader
{
    List<Frame> ReadFrames(FrameReadOptions options, IProgress<ReadProgress>? progress, CancellationToken cancellationToken);
}

/// <summary>
/// What ViewModels actually depend on for Revit reads: safe to call from any thread,
/// marshals internally onto Revit's main thread.
/// </summary>
public interface IRevitFrameReadDispatcher
{
    Task<List<Frame>> ReadFramesAsync(FrameReadOptions options, IProgress<ReadProgress>? progress, CancellationToken cancellationToken);
}

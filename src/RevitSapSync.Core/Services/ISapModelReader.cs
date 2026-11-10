using RevitSapSync.Core.Models;

namespace RevitSapSync.Core.Services;

/// <summary>Reads all frame objects from a running SAP2000 instance as unified <see cref="Frame"/> models.</summary>
public interface ISapModelReader
{
    Task<List<Frame>> ReadFramesAsync(FrameReadOptions options, IProgress<ReadProgress>? progress, CancellationToken cancellationToken);
}

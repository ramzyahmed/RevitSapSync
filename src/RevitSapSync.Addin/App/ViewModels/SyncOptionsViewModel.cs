using RevitSapSync.Core.Options;

namespace RevitSapSync.App.ViewModels;

/// <summary>
/// Backs the pre-flight options window: exposes the same <see cref="SyncSessionOptions"/>
/// instance the caller will go on to use, so edits made in the window are visible to the
/// caller directly - no separate copy-back step needed.
/// </summary>
public sealed class SyncOptionsViewModel
{
    public SyncSessionOptions Options { get; }

    public SyncOptionsViewModel(SyncSessionOptions options)
    {
        Options = options;
    }
}

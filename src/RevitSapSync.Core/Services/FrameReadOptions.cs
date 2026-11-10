namespace RevitSapSync.Core.Services;

/// <summary>Options shared by <see cref="ISapModelReader"/> and <see cref="IRevitFrameReader"/>.</summary>
public sealed class FrameReadOptions
{
    /// <summary>Specific frame/element IDs to read. Empty means "read everything".</summary>
    public List<string> FrameIds { get; init; } = new();

    /// <summary>
    /// When true, read only whatever is currently selected in SAP2000 (frame objects) or
    /// Revit (structural framing/column elements) at the moment the read runs - takes
    /// priority over <see cref="FrameIds"/>. Lets a user work on just the members they've
    /// picked in each application instead of the whole model.
    /// </summary>
    public bool SelectedOnly { get; init; }
}

public sealed record ReadProgress(int Completed, int Total, string CurrentItem);

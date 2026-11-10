using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Bridges the active Revit <see cref="Document"/>/<see cref="UIDocument"/> into
/// constructor-injected services. <see cref="CurrentDocument"/> is set once per command
/// invocation (e.g. in <c>ShowSyncWindowCommand.Execute</c>) rather than
/// constructor-injecting a <see cref="Document"/> directly, since Revit hands the document
/// to the command, not to the DI container. <see cref="CurrentUIDocument"/> is refreshed
/// on every dispatched read too (see <c>RevitApiDispatcher.Execute</c>), since it backs
/// live selection state (<see cref="FrameReadOptions.SelectedOnly"/>) that can change
/// between the modeless window opening and a later button click.
/// </summary>
public interface IRevitContextAccessor
{
    Document? CurrentDocument { get; set; }
    UIDocument? CurrentUIDocument { get; set; }
}

public sealed class RevitContextAccessor : IRevitContextAccessor
{
    public Document? CurrentDocument { get; set; }
    public UIDocument? CurrentUIDocument { get; set; }
}

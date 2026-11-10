using System.Reflection;
using Autodesk.Revit.UI;

namespace RevitSapSync.Addin;

/// <summary>
/// Composition root. Revit instantiates this via reflection (no DI hook available), so
/// the hand-wired <see cref="RevitSapSyncServices"/> is exposed through a single static
/// write-once property - a deliberate, scoped exception to "no service locators",
/// used in exactly one other place (<see cref="Commands.ShowSyncWindowCommand"/>).
/// </summary>
public sealed class RevitSapSyncApplication : IExternalApplication
{
    internal static RevitSapSyncServices? Services { get; private set; }

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            Services = ServiceComposition.Build();

            CreateRibbonPanel(application);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("RevitSapSync", $"Failed to start RevitSapSync: {ex.Message}");
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        Services = null;
        return Result.Succeeded;
    }

    private static void CreateRibbonPanel(UIControlledApplication application)
    {
        const string tabName = "RevitSapSync";
        try
        {
            application.CreateRibbonTab(tabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // Tab already exists - e.g. the add-in was reloaded within the same session.
        }

        var panel = application.CreateRibbonPanel(tabName, "Sync");
        string assemblyPath = Assembly.GetExecutingAssembly().Location;

        var buttonData = new PushButtonData("ShowSyncWindow", "Sync\nWindow", assemblyPath, typeof(Commands.ShowSyncWindowCommand).FullName);
        panel.AddItem(buttonData);
    }
}

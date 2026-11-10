using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitSapSync.App.ViewModels;
using RevitSapSync.App.Views;
using RevitSapSync.Core.Options;

namespace RevitSapSync.Addin.Commands;

/// <summary>
/// Shows the pre-flight options dialog (tolerances, the Beam/Column reclassification
/// toggle), then - only if the user clicks Start rather than Cancel - opens the (modeless)
/// sync window. Read-only for Milestone 1 - no model changes are made from this command
/// itself, so <see cref="TransactionMode.ReadOnly"/> is accurate; later milestones that
/// write to the model will need their own transactional commands.
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
public sealed class ShowSyncWindowCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var services = RevitSapSyncApplication.Services
            ?? throw new InvalidOperationException("RevitSapSync did not start correctly.");

        var sessionOptions = new SyncSessionOptions();
        var optionsViewModel = new SyncOptionsViewModel(sessionOptions);
        bool? optionsResult = new SyncOptionsWindow(optionsViewModel).ShowDialog();
        if (optionsResult != true)
            return Result.Cancelled;

        services.RevitContextAccessor.CurrentDocument = commandData.Application.ActiveUIDocument.Document;
        services.RevitContextAccessor.CurrentUIDocument = commandData.Application.ActiveUIDocument;

        var viewModel = new MainViewModel(
            services.SapModelReader,
            services.RevitFrameReadDispatcher,
            services.MappingEngine,
            services.GeometrySyncDispatcher,
            services.SectionSyncDispatcher,
            services.RevitContextAccessor,
            services.SyncResultsPresenter,
            services.ValidationReportExcelWriter,
            sessionOptions);

        var window = new MainWindow(viewModel);
        window.Show();

        return Result.Succeeded;
    }
}

using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitSapSync.App.ViewModels;
using RevitSapSync.App.Views;
using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;
using RevitSapSync.Core.Services;

namespace RevitSapSync.Addin.Commands;

/// <summary>
/// Ad-hoc, Add-in-Manager-friendly test command: shows the same pre-flight
/// <see cref="RevitSapSync.App.Views.SyncOptionsWindow"/> the real ribbon flow uses, then
/// runs every enabled step (section sync, geometry sync, creation, deletion) strictly
/// gated by its own checkbox, in one synchronous <see cref="Execute"/> call, reporting
/// results via <see cref="RevitSapSync.App.Views.SyncResultsWindow"/>. Bypasses
/// <see cref="RevitSapSyncApplication"/>/the ExternalEvent dispatchers entirely - safe here
/// because Add-in Manager already runs commands on Revit's main thread in a valid API
/// context, so no thread-marshaling is needed, and each engine is called directly rather
/// than through a dispatcher. Not wired into the ribbon; intended purely for direct
/// Add-in-Manager invocation during development.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class TestSyncCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var sessionOptions = new SyncSessionOptions();
        var optionsViewModel = new SyncOptionsViewModel(sessionOptions);
        bool? optionsResult = new SyncOptionsWindow(optionsViewModel).ShowDialog();
        if (optionsResult != true)
            return Result.Cancelled;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var document = commandData.Application.ActiveUIDocument.Document;
            var services = ServiceComposition.Build();
            services.RevitContextAccessor.CurrentDocument = document;
            services.RevitContextAccessor.CurrentUIDocument = commandData.Application.ActiveUIDocument;

            var sapReadOptions = new FrameReadOptions { SelectedOnly = sessionOptions.UseSelectedSapMembers };
            var sapFrames = services.SapModelReader.ReadFramesAsync(sapReadOptions, progress: null, CancellationToken.None).GetAwaiter().GetResult();
            if (sessionOptions.UseSelectedSapMembers && sapFrames.Count == 0)
            {
                TaskDialog.Show("RevitSapSync", "No SAP members are selected.");
                return Result.Cancelled;
            }

            var revitReadOptions = new FrameReadOptions { SelectedOnly = sessionOptions.UseSelectedRevitElements };
            var revitFrames = services.RevitFrameReader.ReadFrames(revitReadOptions, progress: null, CancellationToken.None);
            if (sessionOptions.UseSelectedRevitElements && revitFrames.Count == 0)
            {
                TaskDialog.Show("RevitSapSync", "No Revit elements are selected.");
                return Result.Cancelled;
            }

            sessionOptions.MappingOptions.AllowCategoryChangeForRecreation = sessionOptions.UpdateGeometry;
            var mapping = services.MappingEngine.Map(sapFrames, revitFrames, sessionOptions.MappingOptions);

            var mappedBeams = mapping.MappedPairs
                .Where(pair => pair.SapFrame.MemberType is MemberType.Beam or MemberType.HorizontalBracing or MemberType.VerticalBracing)
                .ToList();
            var mappedColumns = mapping.MappedPairs
                .Where(pair => pair.SapFrame.MemberType is MemberType.Column or MemberType.SlantedColumn)
                .ToList();
            var unmappedSapBeams = mapping.UnmappedSapFrames
                .Where(f => f.MemberType is MemberType.Beam or MemberType.HorizontalBracing or MemberType.VerticalBracing)
                .ToList();
            var unmappedSapColumns = mapping.UnmappedSapFrames
                .Where(f => f.MemberType is MemberType.Column or MemberType.SlantedColumn)
                .ToList();

            bool previewOnly = sessionOptions.PreviewOnly;

            SectionSyncResult? beamSectionResult = null;
            SectionSyncResult? columnSectionResult = null;
            SectionValidationReport? validationReport = null;
            var placeholderReasons = new List<PlaceholderReason>();

            if (sessionOptions.UpdateSections)
            {
                beamSectionResult = services.SectionSyncEngine.SyncBeamSections(document, mappedBeams, previewOnly);
                columnSectionResult = services.SectionSyncEngine.SyncColumnSections(document, mappedColumns, previewOnly);
                validationReport = services.SyncResultsPresenter.PresentSectionSync(document, sapFrames, mapping, beamSectionResult, columnSectionResult);
                placeholderReasons = beamSectionResult.PlaceholderReasons.Concat(columnSectionResult.PlaceholderReasons).ToList();
            }

            GeometrySyncResult? beamGeometryResult = null;
            GeometrySyncResult? columnGeometryResult = null;
            TypeChangeRecreationResult? categoryChangeResult = null;
            if (sessionOptions.UpdateGeometry)
            {
                beamGeometryResult = services.GeometrySyncEngine.SyncBeamGeometry(document, mappedBeams, sessionOptions.GeometrySyncOptions, previewOnly);
                columnGeometryResult = services.GeometrySyncEngine.SyncColumnGeometry(document, mappedColumns, sessionOptions.GeometrySyncOptions, previewOnly);

                var deferredTypeChanges = beamGeometryResult.DeferredTypeChanges.Concat(columnGeometryResult.DeferredTypeChanges).ToList();
                if (deferredTypeChanges.Count > 0)
                    categoryChangeResult = services.RevitElementCreationEngine.RecreateForCategoryChange(document, deferredTypeChanges, sessionOptions.GeometrySyncOptions, previewOnly);
            }

            ElementCreationResult? beamCreationResult = null;
            ElementCreationResult? columnCreationResult = null;
            if (sessionOptions.CreateBeams)
                beamCreationResult = services.RevitElementCreationEngine.CreateBeams(document, unmappedSapBeams, sessionOptions.GeometrySyncOptions, previewOnly);
            if (sessionOptions.CreateColumns)
                columnCreationResult = services.RevitElementCreationEngine.CreateColumns(document, unmappedSapColumns, sessionOptions.GeometrySyncOptions, previewOnly);

            ElementDeletionResult? deletionResult = null;
            if (sessionOptions.DeleteUnmappedRevitMembers)
                deletionResult = services.RevitElementDeletionEngine.Delete(document, mapping.UnmappedRevitFrames, previewOnly);

            stopwatch.Stop();

            int updatedGeometry = (beamGeometryResult?.Updated.Count ?? 0) + (columnGeometryResult?.Updated.Count ?? 0);
            int updatedSections = (beamSectionResult?.Updated.Count ?? 0) + (columnSectionResult?.Updated.Count ?? 0);
            int createdCount = (beamCreationResult?.Created.Count ?? 0) + (columnCreationResult?.Created.Count ?? 0) + (categoryChangeResult?.Created.Count ?? 0);
            int deletedCount = (deletionResult?.Deleted.Count ?? 0) + (categoryChangeResult?.Deleted.Count ?? 0);
            int skippedCount = (beamCreationResult?.Skipped.Count ?? 0) + (columnCreationResult?.Skipped.Count ?? 0) + (categoryChangeResult?.Skipped.Count ?? 0);

            var allErrors = new List<SyncError>();
            AddIfNotNull(allErrors, beamSectionResult?.Errors);
            AddIfNotNull(allErrors, columnSectionResult?.Errors);
            AddIfNotNull(allErrors, beamGeometryResult?.Errors);
            AddIfNotNull(allErrors, columnGeometryResult?.Errors);
            AddIfNotNull(allErrors, beamCreationResult?.Errors);
            AddIfNotNull(allErrors, columnCreationResult?.Errors);
            AddIfNotNull(allErrors, deletionResult?.Errors);
            AddIfNotNull(allErrors, categoryChangeResult?.Errors);

            var allCreated = new List<ElementReference>();
            AddIfNotNull(allCreated, beamCreationResult?.Created);
            AddIfNotNull(allCreated, columnCreationResult?.Created);
            AddIfNotNull(allCreated, categoryChangeResult?.Created);

            var allSkipped = new List<SkippedCreation>();
            AddIfNotNull(allSkipped, beamCreationResult?.Skipped);
            AddIfNotNull(allSkipped, columnCreationResult?.Skipped);
            AddIfNotNull(allSkipped, categoryChangeResult?.Skipped);

            var allDeleted = new List<ElementReference>();
            AddIfNotNull(allDeleted, deletionResult?.Deleted);
            AddIfNotNull(allDeleted, categoryChangeResult?.Deleted);

            string header = previewOnly ? "PREVIEW - no changes were made" : "Synchronization Finished";
            string summary =
                $"{header}\n" +
                $"SAP frames read: {sapFrames.Count} | Revit elements read: {revitFrames.Count}\n" +
                $"Mapped: {mapping.MappedPairs.Count} | Unmapped SAP: {mapping.UnmappedSapFrames.Count} | " +
                $"Unmapped Revit: {mapping.UnmappedRevitFrames.Count} | Ambiguous: {mapping.AmbiguousCandidates.Count}\n\n" +
                $"Updated Geometry : {updatedGeometry}\n" +
                $"Updated Sections : {updatedSections}\n" +
                $"Created          : {createdCount}\n" +
                $"Deleted          : {deletedCount}\n" +
                $"Skipped          : {skippedCount}\n" +
                $"Warnings         : {mapping.CategoryMismatchWarnings.Count}\n" +
                $"Errors           : {allErrors.Count}\n" +
                $"Elapsed Time     : {stopwatch.Elapsed.TotalSeconds:F2} sec";

            var resultsViewModel = new SyncResultsViewModel(services.ValidationReportExcelWriter);
            resultsViewModel.Load(summary, allErrors, validationReport, placeholderReasons, allCreated, allSkipped, allDeleted, mapping.CategoryMismatchWarnings);
            new SyncResultsWindow(resultsViewModel).Show();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("RevitSapSync Test - FAILED", ex.ToString());
            return Result.Failed;
        }
    }

    private static void AddIfNotNull<T>(List<T> target, List<T>? source)
    {
        if (source is not null)
            target.AddRange(source);
    }
}

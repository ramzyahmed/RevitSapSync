using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RevitSapSync.Core.Models;
using RevitSapSync.RevitInterop;

namespace RevitSapSync.App.ViewModels;

/// <summary>
/// Backs the results window shown after a section (and/or geometry) sync run: the summary
/// text, the full structured error list (not truncated/flattened like the old TaskDialog),
/// and the SAP-section/Revit-type validation reconciliation table, with an Export to Excel
/// command that reuses the exact same <see cref="SectionValidationReport"/> already built.
/// </summary>
public sealed partial class SyncResultsViewModel : ObservableObject
{
    private readonly ValidationReportExcelWriter _excelWriter;
    private SectionValidationReport? _report;

    [ObservableProperty]
    private string summaryText = string.Empty;

    public ObservableCollection<SyncError> Errors { get; } = new();
    public ObservableCollection<PlaceholderReason> PlaceholderReasons { get; } = new();
    public ObservableCollection<SectionValidationRecord> MatchingBeams { get; } = new();
    public ObservableCollection<SectionValidationRecord> MatchingColumns { get; } = new();
    public ObservableCollection<UnmappedRevitTypeRecord> UnmatchedRevitBeams { get; } = new();
    public ObservableCollection<UnmappedRevitTypeRecord> UnmatchedRevitColumns { get; } = new();
    public ObservableCollection<ElementReference> Created { get; } = new();
    public ObservableCollection<SkippedCreation> Skipped { get; } = new();
    public ObservableCollection<ElementReference> Deleted { get; } = new();
    public ObservableCollection<CategoryMismatchWarning> Warnings { get; } = new();

    public SyncResultsViewModel(ValidationReportExcelWriter excelWriter)
    {
        _excelWriter = excelWriter;
    }

    /// <summary>
    /// <paramref name="report"/> is null for a geometry-only sync run: the SAP-section/
    /// Revit-type reconciliation depends on a section sync's symbol assignments, so the
    /// matching/unmatched grids and Export to Excel simply stay empty/disabled in that case.
    /// </summary>
    public void Load(
        string summary, List<SyncError> errors, SectionValidationReport? report,
        List<PlaceholderReason>? placeholderReasons = null,
        List<ElementReference>? created = null, List<SkippedCreation>? skipped = null, List<ElementReference>? deleted = null,
        List<CategoryMismatchWarning>? warnings = null)
    {
        SummaryText = summary;
        _report = report;

        Errors.Clear();
        foreach (var error in errors)
            Errors.Add(error);

        PlaceholderReasons.Clear();
        if (placeholderReasons is not null)
            foreach (var reason in placeholderReasons)
                PlaceholderReasons.Add(reason);

        Created.Clear();
        if (created is not null)
            foreach (var reference in created)
                Created.Add(reference);

        Skipped.Clear();
        if (skipped is not null)
            foreach (var skip in skipped)
                Skipped.Add(skip);

        Deleted.Clear();
        if (deleted is not null)
            foreach (var reference in deleted)
                Deleted.Add(reference);

        Warnings.Clear();
        if (warnings is not null)
            foreach (var warning in warnings)
                Warnings.Add(warning);

        MatchingBeams.Clear();
        MatchingColumns.Clear();
        UnmatchedRevitBeams.Clear();
        UnmatchedRevitColumns.Clear();

        if (report is not null)
        {
            foreach (var record in report.MatchingBeams)
                MatchingBeams.Add(record);
            foreach (var record in report.MatchingColumns)
                MatchingColumns.Add(record);
            foreach (var record in report.UnmatchedRevitBeams)
                UnmatchedRevitBeams.Add(record);
            foreach (var record in report.UnmatchedRevitColumns)
                UnmatchedRevitColumns.Add(record);
        }

        ExportToExcelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanExportToExcel))]
    private void ExportToExcel() => _excelWriter.Export(_report!);

    private bool CanExportToExcel() => _report is not null;
}

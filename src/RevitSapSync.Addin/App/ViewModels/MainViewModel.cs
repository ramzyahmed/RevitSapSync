using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RevitSapSync.App.Views;
using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;
using RevitSapSync.Core.Services;
using RevitSapSync.RevitInterop;

namespace RevitSapSync.App.ViewModels;

/// <summary>
/// Milestone 1 view model: read SAP frames, read Revit frames, run the mapping engine,
/// display a summary. No writes to the Revit model happen here.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ISapModelReader _sapReader;
    private readonly IRevitFrameReadDispatcher _revitDispatcher;
    private readonly IFrameMappingEngine _mappingEngine;
    private readonly IGeometrySyncDispatcher _geometrySyncDispatcher;
    private readonly ISectionSyncDispatcher _sectionSyncDispatcher;
    private readonly IRevitContextAccessor _revitContextAccessor;
    private readonly SyncResultsPresenter _resultsPresenter;
    private readonly ValidationReportExcelWriter _excelWriter;
    private readonly SyncSessionOptions _sessionOptions;

    private CancellationTokenSource? _cancellationTokenSource;
    private MappingResult? _lastMappingResult;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = "Ready.";

    [ObservableProperty]
    private double progressPercent;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunMappingCommand))]
    private List<Frame> sapFrames = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunMappingCommand))]
    private List<Frame> revitFrames = new();

    [ObservableProperty]
    private ObservableCollection<MappingRowViewModel> mappingRows = new();

    [ObservableProperty]
    private int mappedCount;

    [ObservableProperty]
    private int unmappedSapCount;

    [ObservableProperty]
    private int unmappedRevitCount;

    [ObservableProperty]
    private int ambiguousCount;

    public MainViewModel(
        ISapModelReader sapReader,
        IRevitFrameReadDispatcher revitDispatcher,
        IFrameMappingEngine mappingEngine,
        IGeometrySyncDispatcher geometrySyncDispatcher,
        ISectionSyncDispatcher sectionSyncDispatcher,
        IRevitContextAccessor revitContextAccessor,
        SyncResultsPresenter resultsPresenter,
        ValidationReportExcelWriter excelWriter,
        SyncSessionOptions sessionOptions)
    {
        _sapReader = sapReader;
        _revitDispatcher = revitDispatcher;
        _mappingEngine = mappingEngine;
        _geometrySyncDispatcher = geometrySyncDispatcher;
        _sectionSyncDispatcher = sectionSyncDispatcher;
        _revitContextAccessor = revitContextAccessor;
        _resultsPresenter = resultsPresenter;
        _excelWriter = excelWriter;
        _sessionOptions = sessionOptions;
    }

    [RelayCommand]
    private async Task ReadSapModelAsync()
    {
        IsBusy = true;
        StatusText = "Reading SAP2000 model...";
        ProgressPercent = 0;
        _cancellationTokenSource = new CancellationTokenSource();
        var progress = new Progress<ReadProgress>(ReportProgress);

        try
        {
            var readOptions = new FrameReadOptions { SelectedOnly = _sessionOptions.UseSelectedSapMembers };
            SapFrames = await _sapReader.ReadFramesAsync(readOptions, progress, _cancellationTokenSource.Token);
            StatusText = $"Read {SapFrames.Count} SAP frame(s).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "SAP2000 read cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to read SAP2000 model: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private async Task ReadRevitModelAsync()
    {
        IsBusy = true;
        StatusText = "Reading Revit model...";
        ProgressPercent = 0;
        _cancellationTokenSource = new CancellationTokenSource();
        var progress = new Progress<ReadProgress>(ReportProgress);

        try
        {
            var readOptions = new FrameReadOptions { SelectedOnly = _sessionOptions.UseSelectedRevitElements };
            RevitFrames = await _revitDispatcher.ReadFramesAsync(readOptions, progress, _cancellationTokenSource.Token);
            StatusText = $"Read {RevitFrames.Count} Revit element(s).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Revit read cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to read Revit model: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunMapping))]
    private void RunMapping()
    {
        var result = _mappingEngine.Map(SapFrames, RevitFrames, _sessionOptions.MappingOptions);
        _lastMappingResult = result;

        var rows = new ObservableCollection<MappingRowViewModel>();
        foreach (var pair in result.MappedPairs)
            rows.Add(MappingRowViewModel.FromMappedPair(pair));
        foreach (var frame in result.UnmappedSapFrames)
            rows.Add(MappingRowViewModel.FromUnmappedSap(frame));
        foreach (var frame in result.UnmappedRevitFrames)
            rows.Add(MappingRowViewModel.FromUnmappedRevit(frame));
        foreach (var candidate in result.AmbiguousCandidates)
            rows.Add(MappingRowViewModel.FromAmbiguous(candidate));

        MappingRows = rows;
        MappedCount = result.MappedPairs.Count;
        UnmappedSapCount = result.UnmappedSapFrames.Count;
        UnmappedRevitCount = result.UnmappedRevitFrames.Count;
        AmbiguousCount = result.AmbiguousCandidates.Count;
        SyncGeometryCommand.NotifyCanExecuteChanged();
        SyncSectionsCommand.NotifyCanExecuteChanged();

        StatusText = $"Mapping complete: {MappedCount} mapped, {UnmappedSapCount} unmapped SAP, " +
                     $"{UnmappedRevitCount} unmapped Revit, {AmbiguousCount} ambiguous.";
    }

    private bool CanRunMapping() => SapFrames.Count > 0 && RevitFrames.Count > 0;

    [RelayCommand(CanExecute = nameof(CanSyncGeometry))]
    private async Task SyncGeometryAsync()
    {
        if (_lastMappingResult is null)
            return;

        IsBusy = true;
        StatusText = "Syncing geometry (beams and columns)...";
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var mappedBeams = _lastMappingResult.MappedPairs
                .Where(pair => pair.SapFrame.MemberType is MemberType.Beam or MemberType.HorizontalBracing or MemberType.VerticalBracing)
                .ToList();
            var mappedColumns = _lastMappingResult.MappedPairs
                .Where(pair => pair.SapFrame.MemberType is MemberType.Column or MemberType.SlantedColumn)
                .ToList();

            var options = _sessionOptions.GeometrySyncOptions;
            var beamResult = await _geometrySyncDispatcher.SyncBeamGeometryAsync(mappedBeams, options, _cancellationTokenSource.Token);
            var columnResult = await _geometrySyncDispatcher.SyncColumnGeometryAsync(mappedColumns, options, _cancellationTokenSource.Token);

            int updated = beamResult.Updated.Count + columnResult.Updated.Count;
            int unchanged = beamResult.Unchanged.Count + columnResult.Unchanged.Count;
            int deferred = beamResult.DeferredTypeChanges.Count + columnResult.DeferredTypeChanges.Count;
            int errors = beamResult.Errors.Count + columnResult.Errors.Count;

            StatusText = $"Geometry sync complete: {updated} updated, {unchanged} unchanged, " +
                         $"{deferred} deferred (need section sync first), {errors} error(s).";

            var document = _revitContextAccessor.CurrentDocument;
            if (document is not null)
            {
                _resultsPresenter.UpdateMappedUnmappedFilters(document, _lastMappingResult);
                ShowResultsWindow(StatusText, beamResult.Errors.Concat(columnResult.Errors).ToList(), report: null);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Geometry sync cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to sync geometry: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cancellationTokenSource = null;
        }
    }

    private bool CanSyncGeometry() => _lastMappingResult is { MappedPairs.Count: > 0 };

    [RelayCommand(CanExecute = nameof(CanSyncSections))]
    private async Task SyncSectionsAsync()
    {
        if (_lastMappingResult is null)
            return;

        IsBusy = true;
        StatusText = "Syncing sections (beams and columns)...";
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var mappedBeams = _lastMappingResult.MappedPairs
                .Where(pair => pair.SapFrame.MemberType is MemberType.Beam or MemberType.HorizontalBracing or MemberType.VerticalBracing)
                .ToList();
            var mappedColumns = _lastMappingResult.MappedPairs
                .Where(pair => pair.SapFrame.MemberType is MemberType.Column or MemberType.SlantedColumn)
                .ToList();

            var beamResult = await _sectionSyncDispatcher.SyncBeamSectionsAsync(mappedBeams, _cancellationTokenSource.Token);
            var columnResult = await _sectionSyncDispatcher.SyncColumnSectionsAsync(mappedColumns, _cancellationTokenSource.Token);

            int updated = beamResult.Updated.Count + columnResult.Updated.Count;
            int unchanged = beamResult.Unchanged.Count + columnResult.Unchanged.Count;
            int placeholders = beamResult.UsingPlaceholderSymbol.Count + columnResult.UsingPlaceholderSymbol.Count;
            int errors = beamResult.Errors.Count + columnResult.Errors.Count;

            StatusText = $"Section sync complete: {updated} updated, {unchanged} unchanged, " +
                         $"{placeholders} using NotFound placeholder, {errors} error(s).";

            var document = _revitContextAccessor.CurrentDocument;
            if (document is not null)
            {
                var validationReport = _resultsPresenter.PresentSectionSync(document, SapFrames, _lastMappingResult, beamResult, columnResult);
                var placeholderReasons = beamResult.PlaceholderReasons.Concat(columnResult.PlaceholderReasons).ToList();
                ShowResultsWindow(StatusText, beamResult.Errors.Concat(columnResult.Errors).ToList(), validationReport, placeholderReasons);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Section sync cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to sync sections: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cancellationTokenSource = null;
        }
    }

    private bool CanSyncSections() => _lastMappingResult is { MappedPairs.Count: > 0 };

    [RelayCommand]
    private void CancelCurrentOperation() => _cancellationTokenSource?.Cancel();

    private void ReportProgress(ReadProgress readProgress) =>
        ProgressPercent = readProgress.Total == 0 ? 0 : 100.0 * readProgress.Completed / readProgress.Total;

    private void ShowResultsWindow(string summary, List<SyncError> errors, SectionValidationReport? report, List<PlaceholderReason>? placeholderReasons = null)
    {
        var resultsViewModel = new SyncResultsViewModel(_excelWriter);
        resultsViewModel.Load(summary, errors, report, placeholderReasons);
        new SyncResultsWindow(resultsViewModel).Show();
    }
}

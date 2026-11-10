using RevitSapSync.Core.Services;
using RevitSapSync.RevitInterop;

namespace RevitSapSync.Addin;

/// <summary>
/// Plain holder for every service RevitSapSync needs - the hand-wired replacement for a
/// DI container's <c>IServiceProvider</c>. See <see cref="ServiceComposition.Build"/> for
/// why there's no container: Revit's process can have older versions of
/// <c>Microsoft.Extensions.DependencyInjection</c> already loaded (confirmed via a
/// <see cref="TypeLoadException"/> triggered through Revit Add-in Manager), and .NET
/// Framework doesn't isolate assembly loading per add-in, so depending on that package at
/// all is a real collision risk in this host process - eliminated by not referencing it.
/// </summary>
internal sealed class RevitSapSyncServices
{
    public required IRevitContextAccessor RevitContextAccessor { get; init; }
    public required ISapModelReader SapModelReader { get; init; }
    public required IRevitFrameReader RevitFrameReader { get; init; }
    public required IRevitFrameReadDispatcher RevitFrameReadDispatcher { get; init; }
    public required IFrameMappingEngine MappingEngine { get; init; }
    public required IGeometrySyncEngine GeometrySyncEngine { get; init; }
    public required IGeometrySyncDispatcher GeometrySyncDispatcher { get; init; }
    public required ISectionSyncEngine SectionSyncEngine { get; init; }
    public required ISectionSyncDispatcher SectionSyncDispatcher { get; init; }
    public required SectionValidationReportBuilder ValidationReportBuilder { get; init; }
    public required SelectionFilterService SelectionFilterService { get; init; }
    public required ValidationReportExcelWriter ValidationReportExcelWriter { get; init; }
    public required SyncResultsPresenter SyncResultsPresenter { get; init; }
    public required RevitElementCreationEngine RevitElementCreationEngine { get; init; }
    public required RevitElementDeletionEngine RevitElementDeletionEngine { get; init; }
}

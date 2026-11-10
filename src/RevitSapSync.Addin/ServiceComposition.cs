using RevitSapSync.Core.Options;
using RevitSapSync.Core.Services;
using RevitSapSync.RevitInterop;
using RevitSapSync.Sap2000Interop;

namespace RevitSapSync.Addin;

/// <summary>
/// Hand-wires every service RevitSapSync needs - see <see cref="RevitSapSyncServices"/>
/// for why there's no DI container involved. Must be called on Revit's main thread (the
/// <c>RevitApiDispatcher</c>/<c>GeometrySyncDispatcher</c>/<c>SectionSyncDispatcher</c>
/// constructors call <c>ExternalEvent.Create</c>, which requires it).
/// </summary>
internal static class ServiceComposition
{
    public static RevitSapSyncServices Build()
    {
        var classificationOptions = new ClassificationOptions();
        var integrationOptions = new RevitIntegrationOptions();

        var classificationService = new DefaultMemberClassificationService();
        var coordinateTransformService = new IdentityCoordinateTransformService();
        var mappingEngine = new FrameMappingEngine();

        var sapConnection = new Sap2000ConnectionProvider();
        var sapModelReader = new Sap2000FrameReader(sapConnection, classificationService, coordinateTransformService, classificationOptions);

        var revitContextAccessor = new RevitContextAccessor();
        var revitFrameReader = new RevitFrameReader(revitContextAccessor, classificationService, classificationOptions, integrationOptions);
        var revitFrameReadDispatcher = new RevitApiDispatcher(revitFrameReader, revitContextAccessor);

        var geometrySyncEngine = new GeometrySyncEngine(integrationOptions);
        var geometrySyncDispatcher = new GeometrySyncDispatcher(geometrySyncEngine, revitContextAccessor);

        var sectionSyncEngine = new SectionSyncEngine(integrationOptions);
        var sectionSyncDispatcher = new SectionSyncDispatcher(sectionSyncEngine, revitContextAccessor);

        var validationReportBuilder = new SectionValidationReportBuilder(sapConnection, integrationOptions);
        var selectionFilterService = new SelectionFilterService();
        var validationReportExcelWriter = new ValidationReportExcelWriter();
        var syncResultsPresenter = new SyncResultsPresenter(selectionFilterService, validationReportBuilder);

        var revitElementCreationEngine = new RevitElementCreationEngine(integrationOptions);
        var revitElementDeletionEngine = new RevitElementDeletionEngine();

        return new RevitSapSyncServices
        {
            RevitContextAccessor = revitContextAccessor,
            SapModelReader = sapModelReader,
            RevitFrameReader = revitFrameReader,
            RevitFrameReadDispatcher = revitFrameReadDispatcher,
            MappingEngine = mappingEngine,
            GeometrySyncEngine = geometrySyncEngine,
            GeometrySyncDispatcher = geometrySyncDispatcher,
            SectionSyncEngine = sectionSyncEngine,
            SectionSyncDispatcher = sectionSyncDispatcher,
            ValidationReportBuilder = validationReportBuilder,
            SelectionFilterService = selectionFilterService,
            ValidationReportExcelWriter = validationReportExcelWriter,
            SyncResultsPresenter = syncResultsPresenter,
            RevitElementCreationEngine = revitElementCreationEngine,
            RevitElementDeletionEngine = revitElementDeletionEngine,
        };
    }
}

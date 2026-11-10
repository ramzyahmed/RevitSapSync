using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;
using RevitSapSync.Core.Services;
using Frame = RevitSapSync.Core.Models.Frame;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Reads structural framing and column instances from the active Revit document into
/// unified <see cref="Frame"/> models. Must only be invoked from inside a valid Revit API
/// context - see <see cref="RevitApiDispatcher"/> for the thread-safe entry point
/// ViewModels should actually call.
/// </summary>
public sealed class RevitFrameReader : IRevitFrameReader
{
    private readonly IRevitContextAccessor _contextAccessor;
    private readonly IMemberClassificationService _classifier;
    private readonly ClassificationOptions _classificationOptions;
    private readonly RevitIntegrationOptions _integrationOptions;

    public RevitFrameReader(
        IRevitContextAccessor contextAccessor,
        IMemberClassificationService classifier,
        ClassificationOptions classificationOptions,
        RevitIntegrationOptions integrationOptions)
    {
        _contextAccessor = contextAccessor;
        _classifier = classifier;
        _classificationOptions = classificationOptions;
        _integrationOptions = integrationOptions;
    }

    public List<Frame> ReadFrames(FrameReadOptions options, IProgress<ReadProgress>? progress, CancellationToken cancellationToken)
    {
        var document = _contextAccessor.CurrentDocument
            ?? throw new InvalidOperationException("No active Revit document. Open a document before reading Revit elements.");

        var categoryFilter = new ElementMulticategoryFilter(new List<BuiltInCategory>
        {
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_StructuralColumns,
        });

        var instances = new FilteredElementCollector(document)
            .WhereElementIsNotElementType()
            .OfClass(typeof(FamilyInstance))
            .WherePasses(categoryFilter)
            .Cast<FamilyInstance>()
            .ToList();

        if (options.SelectedOnly)
        {
            var uiDocument = _contextAccessor.CurrentUIDocument
                ?? throw new InvalidOperationException("No active Revit selection context. Open a document and select elements before reading with 'selected only'.");
            var selectedIds = new HashSet<string>(uiDocument.Selection.GetElementIds().Select(id => id.Value.ToString()));
            instances = instances.Where(instance => selectedIds.Contains(instance.Id.Value.ToString())).ToList();
        }
        else if (options.FrameIds.Count > 0)
        {
            var requestedIds = new HashSet<string>(options.FrameIds);
            instances = instances.Where(instance => requestedIds.Contains(instance.Id.Value.ToString())).ToList();
        }

        Trace.TraceInformation($"RevitSapSync: reading {instances.Count} Revit structural framing/column element(s)...");

        var frames = new List<Frame>(instances.Count);
        int skippedCount = 0;
        for (int i = 0; i < instances.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var instance = instances[i];

            try
            {
                frames.Add(ReadFrame(instance));
            }
            catch (Exception ex)
            {
                skippedCount++;
                Trace.TraceWarning($"RevitSapSync: skipped element {instance.Id.Value} while reading Revit frames. {ex.Message}");
            }

            progress?.Report(new ReadProgress(i + 1, instances.Count, instance.Id.Value.ToString()));
        }

        if (skippedCount > 0)
            Trace.TraceWarning($"RevitSapSync: skipped {skippedCount} of {instances.Count} Revit element(s) that could not be read as frames.");

        return frames;
    }

    private Frame ReadFrame(FamilyInstance instance)
    {
        bool isColumn = instance.Category is { BuiltInCategory: BuiltInCategory.OST_StructuralColumns };

        // Vertical columns use a LocationPoint, not a LocationCurve, in the Revit API -
        // only slanted (CT_EndPoint style) columns and all structural framing have a
        // straight LocationCurve. Missing this case was a real Milestone-1 gap, not a
        // reference behavior being ported: the reference's own getRevitFrames always
        // reads columns via their Base/Top Level+Offset parameters rather than assuming
        // a LocationCurve.
        var (start, end) = instance.Location switch
        {
            LocationCurve { Curve: Line line } => (ToPoint3D(line.GetEndPoint(0)), ToPoint3D(line.GetEndPoint(1))),
            LocationPoint locationPoint when isColumn => ReadVerticalColumnGeometry(instance, locationPoint),
            _ => throw new InvalidOperationException(
                $"Element {instance.Id.Value} has neither a straight LocationCurve nor a column LocationPoint and cannot be read as a Frame."),
        };

        string sapId = instance.LookupParameter(_integrationOptions.SapIdParameterName)?.AsString() ?? string.Empty;

        var memberType = isColumn
            ? ReadColumnStyle(instance) == SlantedOrVerticalColumnType.CT_Vertical ? MemberType.Column : MemberType.SlantedColumn
            : ClassifyNonColumnMemberType(start, end);

        return new Frame
        {
            Id = sapId,
            StartPoint = start,
            EndPoint = end,
            Length = Point3D.Distance(start, end),
            MemberType = memberType,
            Source = FrameSource.Revit,
            RevitElement = new ElementReference(instance.Id.Value, instance.UniqueId),
            RevitFamilyName = instance.Symbol?.Family?.Name ?? string.Empty,
            RevitTypeName = instance.Symbol?.Name ?? string.Empty,
        };
    }

    /// <summary>
    /// Only used for elements Revit's own category already confirms are NOT
    /// OST_StructuralColumns - that category is ground truth here, ruling out Column/
    /// SlantedColumn definitively. <see cref="IMemberClassificationService.Classify"/> is
    /// geometry-only and can still (incorrectly) conclude Column/SlantedColumn for a beam
    /// that happens to be modeled near-vertically; when that happens, the classified
    /// result is clamped to <see cref="MemberType.Beam"/> instead of trusting a geometric
    /// guess that contradicts the real category. The classifier's other sub-classifications
    /// (Beam/HorizontalBracing/VerticalBracing) are still used as-is - only the Column/
    /// SlantedColumn conclusions get overridden.
    /// </summary>
    private MemberType ClassifyNonColumnMemberType(Point3D start, Point3D end)
    {
        var classified = _classifier.Classify(start, end, _classificationOptions);
        return classified is MemberType.Column or MemberType.SlantedColumn ? MemberType.Beam : classified;
    }

    private static SlantedOrVerticalColumnType ReadColumnStyle(FamilyInstance instance)
    {
        var parameter = instance.get_Parameter(BuiltInParameter.SLANTED_COLUMN_TYPE_PARAM);
        return parameter is null ? SlantedOrVerticalColumnType.CT_Vertical : (SlantedOrVerticalColumnType)parameter.AsInteger();
    }

    /// <summary>
    /// A vertical column's endpoints aren't in its LocationPoint at all - only the plan
    /// (X,Y) position is. The actual Z extents come from Base/Top Level + Base/Top Offset,
    /// mirroring how the reference's <c>CreateAllColumns3</c> computes the same thing in
    /// the opposite direction (writing offsets from known elevations rather than reading them).
    /// </summary>
    private static (Point3D Start, Point3D End) ReadVerticalColumnGeometry(FamilyInstance instance, LocationPoint locationPoint)
    {
        var baseLevelParameter = instance.LookupParameter("Base Level")
            ?? throw new InvalidOperationException($"Element {instance.Id.Value} has no 'Base Level' parameter.");
        var topLevelParameter = instance.LookupParameter("Top Level")
            ?? throw new InvalidOperationException($"Element {instance.Id.Value} has no 'Top Level' parameter.");

        var document = instance.Document;
        var baseLevel = document.GetElement(baseLevelParameter.AsElementId()) as Level
            ?? throw new InvalidOperationException($"Element {instance.Id.Value}'s Base Level could not be resolved.");
        var topLevel = document.GetElement(topLevelParameter.AsElementId()) as Level
            ?? throw new InvalidOperationException($"Element {instance.Id.Value}'s Top Level could not be resolved.");

        double baseOffset = instance.LookupParameter("Base Offset")?.AsDouble() ?? 0;
        double topOffset = instance.LookupParameter("Top Offset")?.AsDouble() ?? 0;

        // ProjectElevation (not Elevation) to match CreateAllColumns3's/RevitElementCreationEngine's
        // write-side convention - correct whenever the project has a non-zero survey/base
        // point elevation offset; using Elevation here would silently disagree with what
        // was actually written.
        var point = locationPoint.Point;
        var start = new XYZ(point.X, point.Y, baseLevel.ProjectElevation + baseOffset);
        var end = new XYZ(point.X, point.Y, topLevel.ProjectElevation + topOffset);

        return (ToPoint3D(start), ToPoint3D(end));
    }

    // Revit's internal units are always feet regardless of project display settings;
    // SAP2000 frames are read in millimeters (Sap2000FrameReader sets kN_mm_C), so this
    // conversion is required for the two sources' coordinates to be comparable at all -
    // omitting it would silently break every distance-based tolerance check.
    private static Point3D ToPoint3D(XYZ point) => new(
        UnitUtils.ConvertFromInternalUnits(point.X, RevitUnitConventions.DisplayUnit),
        UnitUtils.ConvertFromInternalUnits(point.Y, RevitUnitConventions.DisplayUnit),
        UnitUtils.ConvertFromInternalUnits(point.Z, RevitUnitConventions.DisplayUnit));
}

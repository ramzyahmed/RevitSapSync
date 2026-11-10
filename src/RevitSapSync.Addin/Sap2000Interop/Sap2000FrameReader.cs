using System.Diagnostics;
using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;
using RevitSapSync.Core.Services;
using SAP2000v1;

namespace RevitSapSync.Sap2000Interop;

/// <summary>
/// Reads all frame objects from the currently running SAP2000 instance, converting them
/// to unified <see cref="Frame"/> models. Reimplements the reference project's
/// <c>SapUtils.getSapFrames</c> read loop against a clean, testable seam
/// (<see cref="ISapConnectionProvider"/>) rather than a static connection holder.
/// </summary>
public sealed class Sap2000FrameReader : ISapModelReader
{
    private readonly ISapConnectionProvider _connection;
    private readonly IMemberClassificationService _classifier;
    private readonly ICoordinateTransformService _coordinateTransform;
    private readonly ClassificationOptions _classificationOptions;

    public Sap2000FrameReader(
        ISapConnectionProvider connection,
        IMemberClassificationService classifier,
        ICoordinateTransformService coordinateTransform,
        ClassificationOptions classificationOptions)
    {
        _connection = connection;
        _classifier = classifier;
        _coordinateTransform = coordinateTransform;
        _classificationOptions = classificationOptions;
    }

    public Task<List<Frame>> ReadFramesAsync(FrameReadOptions options, IProgress<ReadProgress>? progress, CancellationToken cancellationToken)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        return Task.Run(() => ReadFramesCore(options, progress, cancellationToken), cancellationToken);
    }

    private List<Frame> ReadFramesCore(FrameReadOptions options, IProgress<ReadProgress>? progress, CancellationToken cancellationToken)
    {
        Trace.TraceInformation("RevitSapSync: connecting to SAP2000...");
        var model = _connection.GetActiveModel();

        Check(model.SetPresentUnits(eUnits.kN_mm_C), "SapModel.SetPresentUnits");

        string[] names = ResolveFrameNames(model, options);
        Trace.TraceInformation($"RevitSapSync: reading {names.Length} SAP2000 frame object(s)...");

        var frames = new List<Frame>(names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = names[i];

            frames.Add(ReadFrame(model, name));
            progress?.Report(new ReadProgress(i + 1, names.Length, name));
        }

        return frames;
    }

    private static string[] ResolveFrameNames(cSapModel model, FrameReadOptions options)
    {
        if (options.SelectedOnly)
            return GetSelectedFrameNames(model);

        if (options.FrameIds.Count > 0)
            return options.FrameIds.ToArray();

        int count = 0;
        string[]? names = null;
        Check(model.FrameObj.GetNameList(ref count, ref names), "FrameObj.GetNameList");
        return names ?? Array.Empty<string>();
    }

    /// <summary>
    /// <c>SelectObj.GetSelected</c> returns every currently selected object regardless of
    /// type (points, frames, areas, ...) - <c>ObjectType[i] == (int)eObjType.Frame</c> is
    /// how the OAPI distinguishes frame objects within that mixed result.
    /// </summary>
    private static string[] GetSelectedFrameNames(cSapModel model)
    {
        int numberItems = 0;
        int[]? objectTypes = null;
        string[]? objectNames = null;
        Check(model.SelectObj.GetSelected(ref numberItems, ref objectTypes, ref objectNames), "SelectObj.GetSelected");

        if (objectTypes is null || objectNames is null)
            return Array.Empty<string>();

        return objectNames.Where((_, i) => objectTypes[i] == (int)eObjType.Frame).ToArray();
    }

    private Frame ReadFrame(cSapModel model, string name)
    {
        string? pointName1 = null, pointName2 = null;
        Check(model.FrameObj.GetPoints(name, ref pointName1, ref pointName2), $"FrameObj.GetPoints({name})");

        var start = _coordinateTransform.TransformSapToRevit(ReadPoint(model, pointName1!));
        var end = _coordinateTransform.TransformSapToRevit(ReadPoint(model, pointName2!));

        string? sectionName = null, autoSelectList = null;
        Check(model.FrameObj.GetSection(name, ref sectionName, ref autoSelectList), $"FrameObj.GetSection({name})");

        double localAxisAngle = 0;
        bool hasAdvancedLocalAxes = false;
        Check(model.FrameObj.GetLocalAxes(name, ref localAxisAngle, ref hasAdvancedLocalAxes), $"FrameObj.GetLocalAxes({name})");

        int cardinalPoint = 0;
        bool mirror2 = false, mirror3 = false, stiffTransform = false;
        double[]? offset1 = null, offset2 = null;
        string? insertionCSys = null;
        Check(
            model.FrameObj.GetInsertionPoint_1(name, ref cardinalPoint, ref mirror2, ref mirror3, ref stiffTransform, ref offset1, ref offset2, ref insertionCSys),
            $"FrameObj.GetInsertionPoint_1({name})");

        bool autoOffset = false;
        double endLengthOffset1 = 0, endLengthOffset2 = 0, rigidZoneFactor = 0;
        Check(
            model.FrameObj.GetEndLengthOffset(name, ref autoOffset, ref endLengthOffset1, ref endLengthOffset2, ref rigidZoneFactor),
            $"FrameObj.GetEndLengthOffset({name})");

        var memberType = _classifier.Classify(start, end, _classificationOptions);
        var (normalizedSectionName, shape, isBuiltUp, library) = ClassifyAndNormalizeSection(model, name, sectionName ?? string.Empty);

        return new Frame
        {
            Id = name,
            StartPoint = start,
            EndPoint = end,
            Length = Point3D.Distance(start, end),
            Beta = localAxisAngle,
            MemberType = memberType,
            Source = FrameSource.Sap,
            SapSectionName = normalizedSectionName,
            RawSapSectionName = sectionName ?? string.Empty,
            Shape = shape,
            IsBuiltUp = isBuiltUp,
            SectionLibrary = library,
            InsertionPoint = MapCardinalPoint(cardinalPoint),
            StartZOffset = endLengthOffset1,
            EndZOffset = endLengthOffset2,
        };
    }

    /// <summary>
    /// Reimplements <c>ChangeSapSectionNames.GetSectionsDimentions2</c>'s mutation of
    /// <c>frame.Sec</c>: classifies the section, then - only for the shapes actually
    /// exercised by the reference (built-up I, Box/Tube, Pipe, and their tapered/solid
    /// variants) - overwrites the section name with a normalized dimension string. Every
    /// other shape (catalog I-sections, Channel, Tee, Angle, DblAngle) passes through
    /// unchanged, matching confirmed reference behavior.
    /// <para>
    /// A classification/dimension-query failure (e.g. an exotic section type not covered
    /// here) falls back to the raw catalog name rather than failing the whole read -
    /// mirrors the reference's resilience (one bad section never blocks the others).
    /// </para>
    /// </summary>
    private (string SectionName, string Shape, bool IsBuiltUp, string Library) ClassifyAndNormalizeSection(cSapModel model, string frameName, string sectionName)
    {
        try
        {
            var classification = SapSectionClassifier.Classify(model, sectionName);
            string normalizedName = SapSectionNameNormalizer.Normalize(model, classification, sectionName);
            return (normalizedName, classification.Shape, classification.IsBuiltUp, classification.Library);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"RevitSapSync: failed to classify/normalize section '{sectionName}' for frame {frameName}; using raw section name. {ex}");
            return (sectionName, string.Empty, false, string.Empty);
        }
    }

    private static Point3D ReadPoint(cSapModel model, string pointName)
    {
        double x = 0, y = 0, z = 0;
        Check(model.PointObj.GetCoordCartesian(pointName, ref x, ref y, ref z, "Global"), $"PointObj.GetCoordCartesian({pointName})");
        return new Point3D(x, y, z);
    }

    private static void Check(int returnCode, string what)
    {
        if (returnCode != 0)
            throw new InvalidOperationException($"SAP2000 OAPI call failed: {what} (return code {returnCode}).");
    }

    /// <summary>
    /// Best-effort mapping of SAP2000's cardinal-point convention to
    /// <see cref="InsertionPointType"/>. Not yet confirmed against the project's real
    /// SAP2000 documentation/version - fine for Milestone 1 (the mapping engine doesn't
    /// consume this field), but must be verified before the insertion-point-sync
    /// milestone relies on it.
    /// </summary>
    private static InsertionPointType MapCardinalPoint(int cardinalPoint) => cardinalPoint switch
    {
        1 => InsertionPointType.BottomLeft,
        2 => InsertionPointType.BottomCenter,
        3 => InsertionPointType.BottomRight,
        4 => InsertionPointType.MiddleLeft,
        5 => InsertionPointType.Centroid,
        6 => InsertionPointType.MiddleRight,
        7 => InsertionPointType.TopLeft,
        8 => InsertionPointType.TopCenter,
        9 => InsertionPointType.TopRight,
        10 => InsertionPointType.Centroid,
        11 => InsertionPointType.ShearCenter,
        _ => InsertionPointType.Centroid,
    };
}

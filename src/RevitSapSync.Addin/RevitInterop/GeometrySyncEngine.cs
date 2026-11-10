using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;
using Frame = RevitSapSync.Core.Models.Frame;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Reimplements the reference project's <c>Beam.updateRevitGeomertyForBeams2</c> and
/// <c>Column.updateRevitGeomertyForColumns2AsSlantted</c> (including the
/// CT_Vertical&#8596;CT_EndPoint Column-Style dance and <c>alignXYForBothPoints</c>) against
/// the clean <see cref="Frame"/>/<see cref="SapRevitFrame"/> models instead of the
/// reference's overloaded types.
/// <para>
/// Category changes (a mapped pair where the Revit element is a Column but SAP now says
/// Beam, or vice versa) need a freshly provisioned family symbol and a brand-new instance
/// - this engine only detects the condition and reports the pair via
/// <see cref="GeometrySyncResult.DeferredTypeChanges"/> (carrying the old Revit element
/// reference too) rather than acting on it directly; <c>RevitElementCreationEngine.RecreateForCategoryChange</c>
/// is what actually deletes the old instance and creates the new one, gated by
/// <see cref="MappingOptions.AllowCategoryChangeForRecreation"/> having let the mismatched
/// pair map through in the first place.
/// </para>
/// <para>
/// <b>Known scope gap</b>: <see cref="ApplySapParameters"/> currently writes only the
/// SAP-ID parameter. The reference's <c>setParametersSAP</c> also writes Material,
/// Main-Type-ID/sub-zone groups, and end-release parameters, none of which exist yet on
/// the Milestone-1 <see cref="Frame"/> model or <c>Sap2000FrameReader</c> - extending both
/// is a natural next increment, not done here to avoid fabricating data.
/// </para>
/// </summary>
public sealed class GeometrySyncEngine : IGeometrySyncEngine
{
    private readonly RevitIntegrationOptions _integrationOptions;

    public GeometrySyncEngine(RevitIntegrationOptions integrationOptions)
    {
        _integrationOptions = integrationOptions;
    }

    public GeometrySyncResult SyncBeamGeometry(Document document, List<SapRevitFrame> mappedBeams, GeometrySyncOptions options, bool previewOnly = false)
    {
        var updated = new List<ElementReference>();
        var unchanged = new List<ElementReference>();
        var deferredTypeChanges = new List<SapRevitFrame>();
        var errors = new List<SyncError>();

        using var transactionGroup = new TransactionGroup(document, "RevitSapSync: Update Beam Geometry");
        transactionGroup.Start();

        foreach (var chunk in ChunkList(mappedBeams, options.TransactionChunkSize))
        {
            using var transaction = new Transaction(document, "RevitSapSync: Update Beam Geometry Chunk");
            transaction.Start();
            var failureOptions = transaction.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new SuppressWarningsFailuresPreprocessor());
            transaction.SetFailureHandlingOptions(failureOptions);

            foreach (var pair in chunk)
            {
                try
                {
                    SyncSingleBeam(document, pair, options, updated, unchanged, deferredTypeChanges);
                }
                catch (Exception ex)
                {
                    errors.Add(new SyncError { SapId = pair.SapFrame.Id, RevitElement = pair.RevitFrame.RevitElement, Message = ex.Message });
                    Trace.TraceWarning($"RevitSapSync: failed to sync geometry for beam {pair.SapFrame.Id}. {ex}");
                }
            }

            transaction.Commit();
        }

        if (previewOnly)
            transactionGroup.RollBack();
        else
            transactionGroup.Assimilate();

        return new GeometrySyncResult { Updated = updated, Unchanged = unchanged, DeferredTypeChanges = deferredTypeChanges, Errors = errors };
    }

    public GeometrySyncResult SyncColumnGeometry(Document document, List<SapRevitFrame> mappedColumns, GeometrySyncOptions options, bool previewOnly = false)
    {
        var updated = new List<ElementReference>();
        var unchanged = new List<ElementReference>();
        var deferredTypeChanges = new List<SapRevitFrame>();
        var errors = new List<SyncError>();

        using var transactionGroup = new TransactionGroup(document, "RevitSapSync: Update Column Geometry");
        transactionGroup.Start();

        foreach (var chunk in ChunkList(mappedColumns, options.TransactionChunkSize))
        {
            using var transaction = new Transaction(document, "RevitSapSync: Update Column Geometry Chunk");
            transaction.Start();
            var failureOptions = transaction.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new SuppressWarningsFailuresPreprocessor());
            transaction.SetFailureHandlingOptions(failureOptions);

            foreach (var pair in chunk)
            {
                try
                {
                    SyncSingleColumn(document, pair, options, updated, unchanged, deferredTypeChanges);
                }
                catch (Exception ex)
                {
                    errors.Add(new SyncError { SapId = pair.SapFrame.Id, RevitElement = pair.RevitFrame.RevitElement, Message = ex.Message });
                    Trace.TraceWarning($"RevitSapSync: failed to sync geometry for column {pair.SapFrame.Id}. {ex}");
                }
            }

            transaction.Commit();
        }

        if (previewOnly)
            transactionGroup.RollBack();
        else
            transactionGroup.Assimilate();

        return new GeometrySyncResult { Updated = updated, Unchanged = unchanged, DeferredTypeChanges = deferredTypeChanges, Errors = errors };
    }

    private void SyncSingleBeam(Document document, SapRevitFrame pair, GeometrySyncOptions options,
        List<ElementReference> updated, List<ElementReference> unchanged, List<SapRevitFrame> deferredTypeChanges)
    {
        var instance = (FamilyInstance)document.GetElement(new ElementId(pair.RevitFrame.RevitElement!.IntegerId));

        double tolerance = options.MovementTolerancePercent * pair.RevitFrame.Length;
        double startDistance = Point3D.Distance(pair.RevitFrame.StartPoint, pair.SapFrame.StartPoint);
        double endDistance = Point3D.Distance(pair.RevitFrame.EndPoint, pair.SapFrame.EndPoint);
        double rotationRadians = pair.SapFrame.Beta * Math.PI / 180.0;

        if (startDistance < tolerance && endDistance < tolerance)
        {
            if (options.UpdateBeamRotation)
                instance.LookupParameter("Cross-Section Rotation")?.Set(rotationRadians);
            if (options.UpdateJustificationReferences)
                InsertionPointHelpers.Apply(instance, pair.SapFrame, RevitUnitConventions.DisplayUnit);
            ApplySapParameters(instance, pair.SapFrame);
            unchanged.Add(pair.RevitFrame.RevitElement);
            return;
        }

        bool isStillFraming = instance.Category is { BuiltInCategory: BuiltInCategory.OST_StructuralFraming };
        if (!isStillFraming)
        {
            // Column -> Beam: needs a fresh FamilySymbol from the section-sync engine.
            deferredTypeChanges.Add(pair);
            return;
        }

        var line = Line.CreateBound(ColumnGeometryHelpers.ToXyz(pair.SapFrame.StartPoint), ColumnGeometryHelpers.ToXyz(pair.SapFrame.EndPoint));
        ((LocationCurve)instance.Location).Curve = line;
        if (options.UpdateBeamRotation)
            instance.LookupParameter("Cross-Section Rotation")?.Set(rotationRadians);
        if (options.UpdateJustificationReferences)
            InsertionPointHelpers.Apply(instance, pair.SapFrame, RevitUnitConventions.DisplayUnit);
        ApplySapParameters(instance, pair.SapFrame);
        updated.Add(pair.RevitFrame.RevitElement);
    }

    private void SyncSingleColumn(Document document, SapRevitFrame pair, GeometrySyncOptions options,
        List<ElementReference> updated, List<ElementReference> unchanged, List<SapRevitFrame> deferredTypeChanges)
    {
        var instance = (FamilyInstance)document.GetElement(new ElementId(pair.RevitFrame.RevitElement!.IntegerId));

        double tolerance = options.MovementTolerancePercent * pair.RevitFrame.Length;
        double startDistance = Point3D.Distance(pair.RevitFrame.StartPoint, pair.SapFrame.StartPoint);
        double endDistance = Point3D.Distance(pair.RevitFrame.EndPoint, pair.SapFrame.EndPoint);

        if (startDistance <= tolerance && endDistance <= tolerance)
        {
            if (options.UpdateColumnRotation)
                ApplyColumnRotation(document, instance, pair.SapFrame);
            if (options.UpdateJustificationReferences)
                InsertionPointHelpers.Apply(instance, pair.SapFrame, RevitUnitConventions.DisplayUnit);
            ApplySapParameters(instance, pair.SapFrame);
            unchanged.Add(pair.RevitFrame.RevitElement);
            return;
        }

        var orientedLine = ColumnGeometryHelpers.GetOrientedLine(pair.SapFrame.StartPoint, pair.SapFrame.EndPoint);

        bool isStillColumn = instance.Category is { BuiltInCategory: BuiltInCategory.OST_StructuralColumns };
        bool handled = false;

        if (isStillColumn)
        {
            var columnStyleParameter = instance.get_Parameter(BuiltInParameter.SLANTED_COLUMN_TYPE_PARAM);
            var currentStyle = (SlantedOrVerticalColumnType)columnStyleParameter.AsInteger();

            if (currentStyle == SlantedOrVerticalColumnType.CT_Vertical && pair.SapFrame.MemberType == MemberType.Column)
            {
                columnStyleParameter.Set((int)SlantedOrVerticalColumnType.CT_EndPoint);
                ((LocationCurve)instance.Location).Curve = ColumnGeometryHelpers.AlignXYForBothPoints(orientedLine);
                columnStyleParameter.Set((int)SlantedOrVerticalColumnType.CT_Vertical);
                handled = true;
            }
            else if (currentStyle == SlantedOrVerticalColumnType.CT_EndPoint && pair.SapFrame.MemberType == MemberType.SlantedColumn)
            {
                ((LocationCurve)instance.Location).Curve = orientedLine;
                handled = true;
            }
        }

        if (!handled)
        {
            // Column<->Beam category change, or a vertical/slanted style mismatch that
            // needs re-creation - both need the section-sync engine's family lookup.
            deferredTypeChanges.Add(pair);
            return;
        }

        if (options.UpdateColumnRotation)
            ApplyColumnRotation(document, instance, pair.SapFrame);
        if (options.UpdateJustificationReferences)
            InsertionPointHelpers.Apply(instance, pair.SapFrame, RevitUnitConventions.DisplayUnit);
        ApplySapParameters(instance, pair.SapFrame);
        updated.Add(pair.RevitFrame.RevitElement);
    }

    /// <summary>
    /// Reimplements the reference's <c>updateRevitGeomertyForColumns2</c>
    /// <c>#region SetRotationAngle</c> block (present but fully commented out there for
    /// already-mapped columns - no direct "working" precedent, but the intended mechanics
    /// are exactly this): vertical columns are LocationPoint-based, so
    /// <c>Cross-Section Rotation</c> doesn't apply to them - <c>RotateElement</c> rotates BY
    /// an angle rather than TO one, so the current rotation (<c>pt.Rotation</c> in the
    /// reference) must be read first and the DELTA to the target applied, around the
    /// column's raw start-&gt;end axis line (<c>line2</c> in the reference - the same
    /// un-oriented line <c>CreateAllColumns3</c> rotates around at creation time, not the
    /// possibly-reversed line used for the slanted LocationCurve). Slanted columns are
    /// LocationCurve-based like beams and use the same <c>Cross-Section Rotation</c>
    /// parameter (absolute), matching the reference's "recreate as slanted" path.
    /// </summary>
    private static void ApplyColumnRotation(Document document, FamilyInstance instance, Frame sapFrame)
    {
        if (sapFrame.MemberType == MemberType.SlantedColumn)
        {
            instance.LookupParameter("Cross-Section Rotation")?.Set(sapFrame.Beta * Math.PI / 180.0);
            return;
        }

        if (instance.Location is not LocationPoint locationPoint)
            return;

        double targetRotationRadians = ColumnGeometryHelpers.GetVerticalColumnTargetRotationRadians(sapFrame.Beta);
        double deltaRadians = targetRotationRadians - locationPoint.Rotation;
        if (Math.Abs(deltaRadians) < 1e-9)
            return;

        var rotationAxis = Line.CreateBound(ColumnGeometryHelpers.ToXyz(sapFrame.StartPoint), ColumnGeometryHelpers.ToXyz(sapFrame.EndPoint));
        ElementTransformUtils.RotateElement(document, instance.Id, rotationAxis, deltaRadians);
    }

    private void ApplySapParameters(FamilyInstance instance, Frame sapFrame) =>
        instance.LookupParameter(_integrationOptions.SapIdParameterName)?.Set(sapFrame.Id);

    // Enumerable.Chunk is net6.0+ only and unavailable on net48; this is the portable equivalent.
    private static IEnumerable<List<T>> ChunkList<T>(List<T> source, int chunkSize)
    {
        for (int i = 0; i < source.Count; i += chunkSize)
            yield return source.GetRange(i, Math.Min(chunkSize, source.Count - i));
    }
}

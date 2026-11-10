using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;
using RevitSapSync.RevitInterop.SectionFamilies;
using Frame = RevitSapSync.Core.Models.Frame;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Creates brand-new Revit structural framing/column instances for SAP frames that have
/// no mapped Revit element. Not a port - the reference's <c>CreateAllBeams3</c>/
/// <c>CreateAllColumns3</c> place new elements on an arbitrary "first level in the
/// document" (confirmed via direct inspection: two independent, unordered
/// <c>FilteredElementCollector</c> calls, occasionally even resolving to the SAME level
/// for both Base and Top). <see cref="FindNearestLevelAtOrBelow"/> replaces that with a
/// real elevation-based lookup.
/// <para>
/// Unlike section-sync (which falls back to a "NotFound(...)" placeholder for an
/// unrecognized section), creation never uses a placeholder - a section that fails to
/// provision means that member is skipped entirely (see <see cref="ValidateSections"/>),
/// not created with a wrong/stand-in profile.
/// </para>
/// </summary>
public sealed class RevitElementCreationEngine
{
    private readonly BeamSectionSymbolProvisioner _beamProvisioner = new();
    private readonly ColumnSectionSymbolProvisioner _columnProvisioner = new();
    private readonly RevitIntegrationOptions _integrationOptions;

    public RevitElementCreationEngine(RevitIntegrationOptions integrationOptions)
    {
        _integrationOptions = integrationOptions;
    }

    public ElementCreationResult CreateBeams(Document document, List<Frame> unmappedSapBeams, GeometrySyncOptions options, bool previewOnly = false) =>
        Create(document, unmappedSapBeams, BuiltInCategory.OST_StructuralFraming, _beamProvisioner, options, previewOnly, CreateBeamInstance);

    public ElementCreationResult CreateColumns(Document document, List<Frame> unmappedSapColumns, GeometrySyncOptions options, bool previewOnly = false) =>
        Create(document, unmappedSapColumns, BuiltInCategory.OST_StructuralColumns, _columnProvisioner, options, previewOnly, CreateColumnInstance);

    private const int ChunkSize = 1000;

    /// <summary>
    /// Recreates mapped pairs whose category has genuinely changed (e.g. a Revit Column
    /// whose SAP counterpart's geometry now classifies as a Beam) - only reachable when
    /// <see cref="MappingOptions.AllowCategoryChangeForRecreation"/> let the mismatched
    /// pair map through in the first place, so <c>deferredPairs</c> is exactly
    /// <c>GeometrySyncEngine</c>'s <see cref="GeometrySyncResult.DeferredTypeChanges"/>
    /// from both the beam and column geometry-sync calls, combined. Not a port -
    /// <c>updateRevitGeomertyForColumns2AsSlantted</c> (the closest reference precedent)
    /// only has "recreate as vertical column"/"recreate as slanted column" branches, never
    /// a Column&#8594;Beam one - but it establishes the technique this follows exactly:
    /// provision the target category's symbol for this one frame, fully set up the new
    /// instance (reusing the same <see cref="CreateBeamInstance"/>/<see cref="CreateColumnInstance"/>
    /// used for plain creation - a recreated element gets identical treatment to a
    /// brand-new one), and only once that succeeds, delete the old instance.
    /// </summary>
    public TypeChangeRecreationResult RecreateForCategoryChange(Document document, List<SapRevitFrame> deferredPairs, GeometrySyncOptions options, bool previewOnly = false)
    {
        var beamLikePairs = deferredPairs.Where(p => p.SapFrame.MemberType is MemberType.Beam or MemberType.HorizontalBracing or MemberType.VerticalBracing).ToList();
        var columnLikePairs = deferredPairs.Where(p => p.SapFrame.MemberType is MemberType.Column or MemberType.SlantedColumn).ToList();

        var asBeam = Recreate(document, beamLikePairs, BuiltInCategory.OST_StructuralFraming, _beamProvisioner, options, previewOnly, CreateBeamInstance);
        var asColumn = Recreate(document, columnLikePairs, BuiltInCategory.OST_StructuralColumns, _columnProvisioner, options, previewOnly, CreateColumnInstance);

        return new TypeChangeRecreationResult
        {
            Created = asBeam.Created.Concat(asColumn.Created).ToList(),
            Deleted = asBeam.Deleted.Concat(asColumn.Deleted).ToList(),
            Skipped = asBeam.Skipped.Concat(asColumn.Skipped).ToList(),
            Errors = asBeam.Errors.Concat(asColumn.Errors).ToList(),
        };
    }

    private (List<ElementReference> Created, List<ElementReference> Deleted, List<SkippedCreation> Skipped, List<SyncError> Errors) Recreate(
        Document document, List<SapRevitFrame> pairs, BuiltInCategory category, ISectionSymbolProvisioner provisioner,
        GeometrySyncOptions options, bool previewOnly, Func<Document, Frame, FamilySymbol, GeometrySyncOptions, FamilyInstance> createInstance)
    {
        var created = new List<ElementReference>();
        var deleted = new List<ElementReference>();
        var errors = new List<SyncError>();

        if (pairs.Count == 0)
            return (created, deleted, new List<SkippedCreation>(), errors);

        using var transactionGroup = new TransactionGroup(document, $"RevitSapSync: Recreate as {category}");
        transactionGroup.Start();

        var frames = pairs.Select(p => p.SapFrame).ToList();
        var (symbolsBySection, skipped) = ValidateSections(document, frames, category, provisioner);
        var skippedSections = new HashSet<string>(skipped.Select(s => s.SectionName));
        var pairsToRecreate = pairs.Where(p => !skippedSections.Contains(p.SapFrame.SapSectionName)).ToList();

        foreach (var chunk in ChunkList(pairsToRecreate, ChunkSize))
        {
            using var transaction = new Transaction(document, $"RevitSapSync: Recreate as {category} Chunk");
            transaction.Start();
            var failureOptions = transaction.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new SuppressWarningsFailuresPreprocessor());
            transaction.SetFailureHandlingOptions(failureOptions);

            foreach (var pair in chunk)
            {
                try
                {
                    var symbol = symbolsBySection[pair.SapFrame.SapSectionName];
                    var instance = createInstance(document, pair.SapFrame, symbol, options);
                    instance.LookupParameter(_integrationOptions.SapIdParameterName)?.Set(pair.SapFrame.Id);
                    if (options.UpdateJustificationReferences)
                        InsertionPointHelpers.Apply(instance, pair.SapFrame, RevitUnitConventions.DisplayUnit);

                    ApplyCategoryChangeRotation(document, instance, pair, category, options);

                    created.Add(new ElementReference(instance.Id.Value, instance.UniqueId));

                    // Matches the reference's recreate pattern exactly: delete the OLD
                    // instance only after the new one is fully set up - if creation itself
                    // throws, the old instance is left completely untouched.
                    document.Delete(new ElementId(pair.RevitFrame.RevitElement!.IntegerId));
                    deleted.Add(pair.RevitFrame.RevitElement);
                }
                catch (Exception ex)
                {
                    errors.Add(new SyncError { SapId = pair.SapFrame.Id, RevitElement = pair.RevitFrame.RevitElement, Message = ex.Message });
                    Trace.TraceWarning($"RevitSapSync: failed to recreate {pair.SapFrame.Id} as a {category} element. {ex}");
                }
            }

            transaction.Commit();
        }

        if (previewOnly)
            transactionGroup.RollBack();
        else
            transactionGroup.Assimilate();

        return (created, deleted, skipped, errors);
    }

    /// <summary>
    /// A recreated element should read as "the old element, rotated 90" - the old
    /// Column's (or Beam's) actual Revit rotation plus a 90-degree offset, matching the
    /// same axis-convention flip already established for vertical columns vs beams
    /// (<see cref="ColumnGeometryHelpers.GetVerticalColumnTargetRotationRadians"/>) - rather
    /// than recomputing from SAP's raw Beta angle the way plain creation does. This
    /// overwrites whatever rotation <paramref name="instance"/> already has from
    /// <c>createInstance</c>'s own Beta-based logic.
    /// </summary>
    private static void ApplyCategoryChangeRotation(Document document, FamilyInstance instance, SapRevitFrame pair, BuiltInCategory targetCategory, GeometrySyncOptions options)
    {
        bool rotationEnabled = targetCategory == BuiltInCategory.OST_StructuralFraming ? options.UpdateBeamRotation : options.UpdateColumnRotation;
        if (!rotationEnabled)
            return;

        var oldInstance = (FamilyInstance)document.GetElement(new ElementId(pair.RevitFrame.RevitElement!.IntegerId));
        double targetRotationRadians = ReadCurrentRotationRadians(oldInstance) + Math.PI / 2.0;

        if (instance.Location is LocationPoint locationPoint)
        {
            double deltaRadians = targetRotationRadians - locationPoint.Rotation;
            if (Math.Abs(deltaRadians) < 1e-9)
                return;

            var rotationAxis = Line.CreateBound(ColumnGeometryHelpers.ToXyz(pair.SapFrame.StartPoint), ColumnGeometryHelpers.ToXyz(pair.SapFrame.EndPoint));
            ElementTransformUtils.RotateElement(document, instance.Id, rotationAxis, deltaRadians);
        }
        else
        {
            instance.LookupParameter("Cross-Section Rotation")?.Set(targetRotationRadians);
        }
    }

    private static double ReadCurrentRotationRadians(FamilyInstance instance) =>
        instance.Location is LocationPoint locationPoint
            ? locationPoint.Rotation
            : instance.LookupParameter("Cross-Section Rotation")?.AsDouble() ?? 0.0;

    private ElementCreationResult Create(
        Document document, List<Frame> unmappedFrames, BuiltInCategory category, ISectionSymbolProvisioner provisioner,
        GeometrySyncOptions options, bool previewOnly, Func<Document, Frame, FamilySymbol, GeometrySyncOptions, FamilyInstance> createInstance)
    {
        var created = new List<ElementReference>();
        var errors = new List<SyncError>();

        using var transactionGroup = new TransactionGroup(document, $"RevitSapSync: Create {category}");
        transactionGroup.Start();

        var (symbolsBySection, skipped) = ValidateSections(document, unmappedFrames, category, provisioner);
        var skippedSections = new HashSet<string>(skipped.Select(s => s.SectionName));
        var framesToCreate = unmappedFrames.Where(f => !skippedSections.Contains(f.SapSectionName)).ToList();

        // Chunked into its own Transaction per 1000 elements (not one giant transaction
        // for the whole run), matching the same pattern SectionSyncEngine/GeometrySyncEngine
        // already use - keeps each commit small and makes debugging a failed run easier.
        foreach (var chunk in ChunkList(framesToCreate, ChunkSize))
        {
            using var transaction = new Transaction(document, $"RevitSapSync: Create {category} Instances Chunk");
            transaction.Start();
            var failureOptions = transaction.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new SuppressWarningsFailuresPreprocessor());
            transaction.SetFailureHandlingOptions(failureOptions);

            foreach (var frame in chunk)
            {
                try
                {
                    var symbol = symbolsBySection[frame.SapSectionName];
                    var instance = createInstance(document, frame, symbol, options);
                    instance.LookupParameter(_integrationOptions.SapIdParameterName)?.Set(frame.Id);
                    if (options.UpdateJustificationReferences)
                        InsertionPointHelpers.Apply(instance, frame, RevitUnitConventions.DisplayUnit);

                    created.Add(new ElementReference(instance.Id.Value, instance.UniqueId));
                }
                catch (Exception ex)
                {
                    errors.Add(new SyncError { SapId = frame.Id, RevitElement = null, Message = ex.Message });
                    Trace.TraceWarning($"RevitSapSync: failed to create a {category} element for SAP frame {frame.Id}. {ex}");
                }
            }

            transaction.Commit();
        }

        if (previewOnly)
            transactionGroup.RollBack();
        else
            transactionGroup.Assimilate();

        return new ElementCreationResult { Created = created, Skipped = skipped, Errors = errors };
    }

    // Enumerable.Chunk is net6.0+ only and unavailable on net48; this is the portable equivalent.
    private static IEnumerable<List<T>> ChunkList<T>(List<T> source, int chunkSize)
    {
        for (int i = 0; i < source.Count; i += chunkSize)
            yield return source.GetRange(i, Math.Min(chunkSize, source.Count - i));
    }

    /// <summary>
    /// Attempts to provision every distinct section BEFORE any instance is created, so a
    /// missing family is known upfront (and reported once per section) rather than
    /// discovered partway through a run.
    /// </summary>
    private static (Dictionary<string, FamilySymbol> Symbols, List<SkippedCreation> Skipped) ValidateSections(
        Document document, List<Frame> unmappedFrames, BuiltInCategory category, ISectionSymbolProvisioner provisioner)
    {
        var symbols = new Dictionary<string, FamilySymbol>();
        var skipped = new List<SkippedCreation>();

        var distinctSections = unmappedFrames.GroupBy(f => f.SapSectionName).Select(g => g.First());
        foreach (var frame in distinctSections)
        {
            try
            {
                var symbol = provisioner.Provision(document, frame, RevitUnitConventions.DisplayUnit);
                if (symbol is null)
                {
                    skipped.Add(new SkippedCreation
                    {
                        SapId = frame.Id,
                        SectionName = frame.SapSectionName,
                        Reason = $"Missing Family: shape '{frame.Shape}' (IsBuiltUp={frame.IsBuiltUp}) is not covered by the section provisioner.",
                    });
                    continue;
                }

                if (!symbol.IsActive)
                    symbol.Activate();

                symbols[frame.SapSectionName] = symbol;
            }
            catch (Exception ex)
            {
                skipped.Add(new SkippedCreation { SapId = frame.Id, SectionName = frame.SapSectionName, Reason = $"Missing Family: {ex.Message}" });
                Trace.TraceWarning($"RevitSapSync: section validation failed for '{frame.SapSectionName}' in category {category}. {ex}");
            }
        }

        return (symbols, skipped);
    }

    private static FamilyInstance CreateBeamInstance(Document document, Frame frame, FamilySymbol symbol, GeometrySyncOptions options)
    {
        var level = FindNearestLevelAtOrBelow(document, ColumnGeometryHelpers.ToXyz(frame.StartPoint).Z);
        var line = Line.CreateBound(ColumnGeometryHelpers.ToXyz(frame.StartPoint), ColumnGeometryHelpers.ToXyz(frame.EndPoint));
        var instance = document.Create.NewFamilyInstance(line, symbol, level, StructuralType.Beam);

        if (options.UpdateBeamRotation)
            instance.LookupParameter("Cross-Section Rotation")?.Set(frame.Beta * Math.PI / 180.0);

        // Matches CreateAllBeams3 exactly - always disallowed at both ends for a newly
        // created beam, not gated by any option (the reference never gates it either).
        StructuralFramingUtils.DisallowJoinAtEnd(instance, 0);
        StructuralFramingUtils.DisallowJoinAtEnd(instance, 1);

        return instance;
    }

    /// <summary>
    /// Matches <c>CreateAllColumns3</c>: place at the start point, set location (curve for
    /// slanted, Base/Top Level+Offset for vertical), THEN rotate - identically for both
    /// vertical and slanted columns - via <c>ElementTransformUtils.RotateElement</c> around
    /// the column's own raw start-&gt;end axis line (not the possibly-reversed oriented
    /// line used for the slanted LocationCurve, and not the
    /// <c>Cross-Section Rotation</c> parameter - that's only used in the reference's
    /// separate "recreate as slanted" path, never at creation time).
    /// </summary>
    private static FamilyInstance CreateColumnInstance(Document document, Frame frame, FamilySymbol symbol, GeometrySyncOptions options)
    {
        var startXyz = ColumnGeometryHelpers.ToXyz(frame.StartPoint);
        var placementLevel = FindNearestLevelAtOrBelow(document, startXyz.Z);
        var instance = document.Create.NewFamilyInstance(startXyz, symbol, placementLevel, StructuralType.Column);

        if (frame.MemberType == MemberType.SlantedColumn)
        {
            instance.get_Parameter(BuiltInParameter.SLANTED_COLUMN_TYPE_PARAM)?.Set((int)SlantedOrVerticalColumnType.CT_EndPoint);
            var orientedLine = ColumnGeometryHelpers.GetOrientedLine(frame.StartPoint, frame.EndPoint);
            ((LocationCurve)instance.Location).Curve = orientedLine;
        }
        else
        {
            var endXyz = ColumnGeometryHelpers.ToXyz(frame.EndPoint);

            // Matches CreateAllColumns3 exactly: Base is always the LOWER of Start/End Z
            // and Top is always the HIGHER, regardless of which one is literally
            // "StartPoint" vs "EndPoint" in the SAP frame - a real bug in an earlier
            // version of this port that blindly assumed Start=Base, End=Top.
            double basePointElevation = Math.Min(startXyz.Z, endXyz.Z);
            double topPointElevation = Math.Max(startXyz.Z, endXyz.Z);

            var baseLevel = FindNearestLevelAtOrBelow(document, basePointElevation);
            var topLevel = FindNearestLevelAtOrBelow(document, topPointElevation);

            instance.LookupParameter("Base Level")?.Set(baseLevel.Id);
            instance.LookupParameter("Top Level")?.Set(topLevel.Id);
            // Matches CreateAllColumns3's use of Level.ProjectElevation (not Level.Elevation)
            // for the offset math - correct whenever the project has a non-zero survey/base
            // point elevation offset.
            instance.LookupParameter("Base Offset")?.Set(basePointElevation - baseLevel.ProjectElevation);
            instance.LookupParameter("Top Offset")?.Set(topPointElevation - topLevel.ProjectElevation);
        }

        if (options.UpdateColumnRotation)
        {
            // Matches CreateAllColumns3's mechanism (RotateElement, unconditional for both
            // vertical and slanted) exactly, but the TARGET angle differs: only a
            // genuinely vertical column's local-1 axis hits SAP2000's special
            // vertical-member rule (see GetVerticalColumnTargetRotationRadians) - a
            // slanted column's local-1 isn't vertical, so it uses the same general
            // convention as beams (raw Beta, no offset).
            var rotationAxis = Line.CreateBound(startXyz, ColumnGeometryHelpers.ToXyz(frame.EndPoint));
            double rotationRadians = frame.MemberType == MemberType.SlantedColumn
                ? frame.Beta * Math.PI / 180.0
                : ColumnGeometryHelpers.GetVerticalColumnTargetRotationRadians(frame.Beta);
            ElementTransformUtils.RotateElement(document, instance.Id, rotationAxis, rotationRadians);
        }

        return instance;
    }

    /// <summary>
    /// Picks the level with the greatest <see cref="Level.ProjectElevation"/> that is
    /// still at or below the target, falling back to the lowest level in the document if
    /// the target is below every level. Fixes the reference's arbitrary "first level
    /// found" approach. Compares <c>ProjectElevation</c> (not <c>Elevation</c>) to match
    /// the same convention <c>CreateAllColumns3</c> uses for its offset math - correct
    /// whenever the project has a non-zero survey/base point elevation offset.
    /// </summary>
    private static Level FindNearestLevelAtOrBelow(Document document, double elevationFeet)
    {
        var levels = new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>().ToList();
        if (levels.Count == 0)
            throw new InvalidOperationException("No Level exists in the document to place a new element on.");

        return levels.Where(l => l.ProjectElevation <= elevationFeet).OrderByDescending(l => l.ProjectElevation).FirstOrDefault()
            ?? levels.OrderBy(l => l.ProjectElevation).First();
    }
}

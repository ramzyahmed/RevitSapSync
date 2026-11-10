using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitSapSync.Core.Models;
using RevitSapSync.RevitInterop.SectionFamilies;
using Frame = RevitSapSync.Core.Models.Frame;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Reimplements the reference project's two-phase section-sync flow: provision a
/// FamilySymbol per distinct SAP section first (<c>createRvtFamilySectionForAllBeam</c>/
/// <c>ForColumn</c>'s dictionary-building, including the "NotFound" placeholder fallback),
/// then assign symbols onto the mapped elements
/// (<c>updateRevitSectionsForBeams2</c>/<c>ForColumns2</c>).
/// </summary>
public sealed class SectionSyncEngine : ISectionSyncEngine
{
    private const string NotFoundRevitTypeName = "TUBE 80X80X4";
    private const int ChunkSize = 1000;

    private readonly BeamSectionSymbolProvisioner _beamProvisioner = new();
    private readonly ColumnSectionSymbolProvisioner _columnProvisioner = new();
    private readonly RevitIntegrationOptions _integrationOptions;

    public SectionSyncEngine(RevitIntegrationOptions integrationOptions)
    {
        _integrationOptions = integrationOptions;
    }

    public SectionSyncResult SyncBeamSections(Document document, List<SapRevitFrame> mappedBeams, bool previewOnly = false) =>
        Sync(document, mappedBeams, BuiltInCategory.OST_StructuralFraming, _beamProvisioner, previewOnly);

    public SectionSyncResult SyncColumnSections(Document document, List<SapRevitFrame> mappedColumns, bool previewOnly = false) =>
        Sync(document, mappedColumns, BuiltInCategory.OST_StructuralColumns, _columnProvisioner, previewOnly);

    private SectionSyncResult Sync(Document document, List<SapRevitFrame> mappedPairs, BuiltInCategory category, ISectionSymbolProvisioner provisioner, bool previewOnly)
    {
        var updated = new List<ElementReference>();
        var unchanged = new List<ElementReference>();
        var usingPlaceholder = new List<ElementReference>();
        var errors = new List<SyncError>();

        // Started before BuildSymbolCatalog (not after) so that any placeholder-symbol
        // duplication it performs - which commits its own nested Transaction - is still
        // undone by RollBack() below when previewOnly is true. A TransactionGroup rolls
        // back every Transaction committed while it was active, nested or not.
        using var transactionGroup = new TransactionGroup(document, "RevitSapSync: Update Sections");
        transactionGroup.Start();

        var (symbolsBySection, placeholderSections, placeholderReasons) = BuildSymbolCatalog(document, mappedPairs, category, provisioner);

        foreach (var chunk in ChunkList(mappedPairs, ChunkSize))
        {
            using var transaction = new Transaction(document, "RevitSapSync: Update Sections Chunk");
            transaction.Start();
            var failureOptions = transaction.GetFailureHandlingOptions();
            failureOptions.SetFailuresPreprocessor(new SuppressWarningsFailuresPreprocessor());
            transaction.SetFailureHandlingOptions(failureOptions);

            foreach (var pair in chunk)
            {
                try
                {
                    SyncSingle(document, pair, symbolsBySection, placeholderSections, updated, unchanged, usingPlaceholder);
                }
                catch (Exception ex)
                {
                    errors.Add(new SyncError { SapId = pair.SapFrame.Id, RevitElement = pair.RevitFrame.RevitElement, Message = ex.Message });
                    Trace.TraceWarning($"RevitSapSync: failed to sync section for {pair.SapFrame.Id}. {ex}");
                }
            }

            transaction.Commit();
        }

        if (previewOnly)
            transactionGroup.RollBack();
        else
            transactionGroup.Assimilate();

        var sectionAssignments = symbolsBySection.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);
        return new SectionSyncResult
        {
            Updated = updated,
            Unchanged = unchanged,
            UsingPlaceholderSymbol = usingPlaceholder,
            Errors = errors,
            SectionAssignments = sectionAssignments,
            PlaceholderReasons = placeholderReasons,
        };
    }

    private void SyncSingle(
        Document document, SapRevitFrame pair, Dictionary<string, FamilySymbol> symbolsBySection, HashSet<string> placeholderSections,
        List<ElementReference> updated, List<ElementReference> unchanged, List<ElementReference> usingPlaceholder)
    {
        if (!symbolsBySection.TryGetValue(pair.SapFrame.SapSectionName, out var symbol))
            throw new InvalidOperationException($"No symbol was provisioned for section '{pair.SapFrame.SapSectionName}'.");

        var instance = (FamilyInstance)document.GetElement(new ElementId(pair.RevitFrame.RevitElement!.IntegerId));
        bool symbolChanged = symbol.Name != pair.RevitFrame.RevitTypeName;

        if (symbolChanged)
            AssignSymbol(instance, symbol, pair.SapFrame.MemberType);

        instance.LookupParameter(_integrationOptions.SapIdParameterName)?.Set(pair.SapFrame.Id);

        var reference = pair.RevitFrame.RevitElement;
        if (placeholderSections.Contains(pair.SapFrame.SapSectionName))
            usingPlaceholder.Add(reference);
        else if (symbolChanged)
            updated.Add(reference);
        else
            unchanged.Add(reference);
    }

    /// <summary>
    /// Revit refuses a bare <c>instance.Symbol = symbol</c> ("Family symbol is not valid
    /// for this instance") whenever a column's
    /// <see cref="BuiltInParameter.SLANTED_COLUMN_TYPE_PARAM"/> isn't CT_Vertical - that
    /// includes CT_EndPoint (two-point slant) AND CT_Angle (angle-defined slant); an
    /// earlier version of this fix only excluded CT_EndPoint, which silently let CT_Angle
    /// columns keep failing with the exact same error. Mirrors <c>GeometrySyncEngine</c>'s
    /// CT_Vertical&#8596;CT_EndPoint dance: briefly revert to vertical, swap the symbol,
    /// then restore to CT_EndPoint (RevitSapSync's chosen slanted representation
    /// throughout, matching <c>GeometrySyncEngine</c> - a CT_Angle column touched here ends
    /// up CT_EndPoint afterward, a deliberate simplification, not a bug) if the SAP frame
    /// still calls for a slanted column. Also guards against assigning an inactive symbol,
    /// which throws the same exception - duplicated/found symbols are normally already
    /// active, but this closes the gap defensively.
    /// </summary>
    private static void AssignSymbol(FamilyInstance instance, FamilySymbol symbol, MemberType targetMemberType)
    {
        if (!symbol.IsActive)
            symbol.Activate();

        if (instance.Category is not { BuiltInCategory: BuiltInCategory.OST_StructuralColumns })
        {
            instance.Symbol = symbol;
            return;
        }

        var columnStyleParameter = instance.get_Parameter(BuiltInParameter.SLANTED_COLUMN_TYPE_PARAM);
        if (columnStyleParameter is null || (SlantedOrVerticalColumnType)columnStyleParameter.AsInteger() == SlantedOrVerticalColumnType.CT_Vertical)
        {
            instance.Symbol = symbol;
            return;
        }

        columnStyleParameter.Set((int)SlantedOrVerticalColumnType.CT_Vertical);
        instance.Symbol = symbol;
        if (targetMemberType == MemberType.SlantedColumn)
            columnStyleParameter.Set((int)SlantedOrVerticalColumnType.CT_EndPoint);
    }

    private (Dictionary<string, FamilySymbol> Symbols, HashSet<string> PlaceholderSections, List<PlaceholderReason> PlaceholderReasons) BuildSymbolCatalog(
        Document document, List<SapRevitFrame> mappedPairs, BuiltInCategory category, ISectionSymbolProvisioner provisioner)
    {
        var symbols = new Dictionary<string, FamilySymbol>();
        var placeholderSections = new HashSet<string>();
        var placeholderReasons = new List<PlaceholderReason>();

        var distinctSections = mappedPairs.Select(p => p.SapFrame).GroupBy(f => f.SapSectionName).Select(g => g.First());

        foreach (var frame in distinctSections)
        {
            FamilySymbol symbol;
            try
            {
                symbol = provisioner.Provision(document, frame, RevitUnitConventions.DisplayUnit);
                if (symbol is null)
                {
                    placeholderReasons.Add(new PlaceholderReason
                    {
                        SapSectionName = frame.SapSectionName,
                        Reason = $"Shape '{frame.Shape}' (IsBuiltUp={frame.IsBuiltUp}) is not yet covered by the section provisioner.",
                    });
                    symbol = GetOrCreatePlaceholder(document, category, frame.SapSectionName);
                }
            }
            catch (Exception ex)
            {
                placeholderReasons.Add(new PlaceholderReason { SapSectionName = frame.SapSectionName, Reason = ex.Message });
                Trace.TraceWarning($"RevitSapSync: failed to provision a FamilySymbol for section '{frame.SapSectionName}'; falling back to placeholder. {ex}");
                symbol = GetOrCreatePlaceholder(document, category, frame.SapSectionName);
            }

            if (symbol.Name.StartsWith("NotFound(", StringComparison.Ordinal))
                placeholderSections.Add(frame.SapSectionName);

            symbols[frame.SapSectionName] = symbol;
        }

        return (symbols, placeholderSections, placeholderReasons);
    }

    private static FamilySymbol GetOrCreatePlaceholder(Document document, BuiltInCategory category, string sectionName)
    {
        string placeholderName = $"NotFound( {sectionName} )";
        var existing = SectionSymbolProvisioningHelpers.FindByTypeName(document, category, placeholderName);
        if (existing is not null)
            return existing;

        var baseSymbol = SectionSymbolProvisioningHelpers.FindByTypeName(document, category, NotFoundRevitTypeName)
            ?? throw new InvalidOperationException(
                $"No base FamilySymbol named '{NotFoundRevitTypeName}' found in category {category} to seed NotFound placeholders " +
                "(matches the reference implementation's convention - this type must already exist in the project).");

        using var transaction = new Transaction(document, "RevitSapSync: Create NotFound Placeholder");
        transaction.Start();
        var failureOptions = transaction.GetFailureHandlingOptions();
        failureOptions.SetFailuresPreprocessor(new SuppressWarningsFailuresPreprocessor());
        transaction.SetFailureHandlingOptions(failureOptions);
        var placeholder = (FamilySymbol)baseSymbol.Duplicate(placeholderName);
        transaction.Commit();
        return placeholder;
    }

    // Enumerable.Chunk is net6.0+ only and unavailable on net48; this is the portable equivalent.
    private static IEnumerable<List<T>> ChunkList<T>(List<T> source, int chunkSize)
    {
        for (int i = 0; i < source.Count; i += chunkSize)
            yield return source.GetRange(i, Math.Min(chunkSize, source.Count - i));
    }
}

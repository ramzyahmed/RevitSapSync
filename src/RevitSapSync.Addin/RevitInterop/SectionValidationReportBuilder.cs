using Autodesk.Revit.DB;
using RevitSapSync.Core.Models;
using RevitSapSync.Sap2000Interop;
using SAP2000v1;
using Frame = RevitSapSync.Core.Models.Frame;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Reimplements the reference project's <c>SapUtils.validation</c>: for each distinct
/// Revit type actually assigned (grouped by <c>FamilyInstance.Symbol.Name</c>), finds the
/// SAP section that was provisioned to produce it (via <see cref="SectionSyncResult.SectionAssignments"/>'s
/// reverse lookup) and compares member count/length/tonnage on both sides, plus the SAP
/// section's cross-section area. The area comes straight from a <c>PropFrame.GetSectProps</c>
/// OAPI call (matches the reference's <c>getAreaOfSapProfiles</c> exactly - not a
/// re-derived geometry formula), and Revit-side tonnage uses the built-in "Length"/
/// "Cut Length"/"Volume" instance parameters times <see cref="RevitIntegrationOptions.SteelDensityTonnesPerCubicMeter"/>
/// (matches the reference's <c>HardCodedData.GamaSteel</c>).
/// </summary>
public sealed class SectionValidationReportBuilder
{
    private readonly ISapConnectionProvider _sapConnection;
    private readonly RevitIntegrationOptions _options;

    public SectionValidationReportBuilder(ISapConnectionProvider sapConnection, RevitIntegrationOptions options)
    {
        _sapConnection = sapConnection;
        _options = options;
    }

    public SectionValidationReport Build(
        Document document,
        List<Frame> sapFrames,
        List<ElementId> mappedRevitBeamIds,
        List<ElementId> mappedRevitColumnIds,
        List<ElementId> unmappedRevitBeamIds,
        List<ElementId> unmappedRevitColumnIds,
        Dictionary<string, string> beamSectionAssignments,
        Dictionary<string, string> columnSectionAssignments)
    {
        var sapBeams = sapFrames.Where(f => f.MemberType is MemberType.Beam or MemberType.HorizontalBracing or MemberType.VerticalBracing).ToList();
        var sapColumns = sapFrames.Where(f => f.MemberType is MemberType.Column or MemberType.SlantedColumn).ToList();

        var areaByRawSection = LoadSapAreas(sapFrames.Select(f => f.RawSapSectionName).Distinct().ToList());

        return new SectionValidationReport
        {
            MatchingBeams = BuildMatchingRecords(document, mappedRevitBeamIds, sapBeams, beamSectionAssignments, areaByRawSection, isBeamCategory: true),
            MatchingColumns = BuildMatchingRecords(document, mappedRevitColumnIds, sapColumns, columnSectionAssignments, areaByRawSection, isBeamCategory: false),
            UnmatchedRevitBeams = BuildUnmatchedRecords(document, unmappedRevitBeamIds, isBeamCategory: true),
            UnmatchedRevitColumns = BuildUnmatchedRecords(document, unmappedRevitColumnIds, isBeamCategory: false),
        };
    }

    private List<SectionValidationRecord> BuildMatchingRecords(
        Document document, List<ElementId> revitElementIds, List<Frame> sapMembersOfThisCategory,
        Dictionary<string, string> sectionAssignments, Dictionary<string, double> areaByRawSection, bool isBeamCategory)
    {
        var instances = ResolveInstances(document, revitElementIds);

        // sectionAssignments maps SAP section name -> Revit type name; the report needs the reverse.
        var sapSectionByRevitType = sectionAssignments
            .GroupBy(kvp => kvp.Value)
            .ToDictionary(g => g.Key, g => g.First().Key);

        var sapFramesBySection = sapMembersOfThisCategory.ToLookup(f => f.SapSectionName);

        var records = new List<SectionValidationRecord>();
        foreach (var group in instances.GroupBy(i => i.Symbol.Name))
        {
            var groupInstances = group.ToList();
            var (revitLength, revitCutLength, revitTonnage) = GetRevitLengthAndTonnage(groupInstances, isBeamCategory);

            string sapSectionName = "NotFound";
            int sapCount = 0;
            double sapLength = 0, sapTonnage = 0, sapArea = 0;

            if (sapSectionByRevitType.TryGetValue(group.Key, out var matchedSection))
            {
                var sapMembers = sapFramesBySection[matchedSection].ToList();
                if (sapMembers.Count > 0)
                {
                    // Matches the reference's "ExistSec" display: the raw name of the
                    // first member in the group, not the normalized matching key.
                    sapSectionName = sapMembers[0].RawSapSectionName;
                    sapCount = sapMembers.Count;
                    sapLength = sapMembers.Sum(f => f.Length) / 1000.0;
                    areaByRawSection.TryGetValue(sapMembers[0].RawSapSectionName, out sapArea);
                    double areaSquareMeters = sapArea / 1_000_000.0;
                    sapTonnage = _options.SteelDensityTonnesPerCubicMeter * sapLength * areaSquareMeters;
                }
            }

            records.Add(new SectionValidationRecord
            {
                RevitTypeName = group.Key,
                RevitMemberCount = groupInstances.Count,
                RevitTotalLengthMeters = revitLength,
                RevitTotalCutLengthMeters = revitCutLength,
                RevitTotalTonnage = revitTonnage,
                SapSectionName = sapSectionName,
                SapMemberCount = sapCount,
                SapTotalLengthMeters = sapLength,
                SapTotalTonnage = sapTonnage,
                SapCrossSectionAreaMm2 = sapArea,
            });
        }

        return records;
    }

    private List<UnmappedRevitTypeRecord> BuildUnmatchedRecords(Document document, List<ElementId> elementIds, bool isBeamCategory)
    {
        var instances = ResolveInstances(document, elementIds);

        return instances.GroupBy(i => i.Symbol.Name).Select(group =>
        {
            var groupInstances = group.ToList();
            var (length, cutLength, tonnage) = GetRevitLengthAndTonnage(groupInstances, isBeamCategory);
            return new UnmappedRevitTypeRecord
            {
                RevitTypeName = group.Key,
                RevitMemberCount = groupInstances.Count,
                RevitTotalLengthMeters = length,
                RevitTotalCutLengthMeters = cutLength,
                RevitTotalTonnage = tonnage,
            };
        }).ToList();
    }

    private static List<FamilyInstance> ResolveInstances(Document document, List<ElementId> elementIds) =>
        elementIds
            .Select(id => document.GetElement(id) as FamilyInstance)
            .Where(instance => instance is not null)
            .Cast<FamilyInstance>()
            .ToList();

    private (double LengthMeters, double CutLengthMeters, double TonnageTons) GetRevitLengthAndTonnage(List<FamilyInstance> instances, bool isBeamCategory)
    {
        double totalLength = 0, totalCutLength = 0, totalVolume = 0;

        foreach (var instance in instances)
        {
            totalLength += UnitUtils.ConvertFromInternalUnits(instance.LookupParameter("Length")?.AsDouble() ?? 0, UnitTypeId.Meters);
            totalVolume += UnitUtils.ConvertFromInternalUnits(instance.LookupParameter("Volume")?.AsDouble() ?? 0, UnitTypeId.CubicMeters);

            if (isBeamCategory)
            {
                // Matches the reference: a missing/invalid Cut Length on one instance never blocks the rest.
                try
                {
                    totalCutLength += UnitUtils.ConvertFromInternalUnits(instance.LookupParameter("Cut Length")?.AsDouble() ?? 0, UnitTypeId.Meters);
                }
                catch
                {
                }
            }
        }

        return (totalLength, totalCutLength, _options.SteelDensityTonnesPerCubicMeter * totalVolume);
    }

    private Dictionary<string, double> LoadSapAreas(List<string> rawSectionNames)
    {
        var model = _sapConnection.GetActiveModel();
        var areas = new Dictionary<string, double>();

        foreach (var section in rawSectionNames)
        {
            if (string.IsNullOrEmpty(section))
                continue;

            double area = 0, as2 = 0, as3 = 0, torsion = 0, i22 = 0, i33 = 0, s22 = 0, s33 = 0, z22 = 0, z33 = 0, r22 = 0, r33 = 0;
            var shape = eFramePropType.Circle;
            model.PropFrame.GetTypeOAPI(section, ref shape);

            if (shape == eFramePropType.Variable)
            {
                int numberItems = 0;
                string[]? startSec = null, endSec = null;
                double[]? lengths = null;
                int[]? types = null, ei33 = null, ei22 = null;
                int color = 0;
                string? notes = null, guid = null;
                model.PropFrame.GetNonPrismatic(section, ref numberItems, ref startSec, ref endSec, ref lengths, ref types, ref ei33, ref ei22, ref color, ref notes, ref guid);

                model.PropFrame.GetSectProps(startSec![0], ref area, ref as2, ref as3, ref torsion, ref i22, ref i33, ref s22, ref s33, ref z22, ref z33, ref r22, ref r33);
                double startArea = area;
                model.PropFrame.GetSectProps(endSec![0], ref area, ref as2, ref as3, ref torsion, ref i22, ref i33, ref s22, ref s33, ref z22, ref z33, ref r22, ref r33);
                double endArea = area;

                areas[section] = (startArea + endArea) / 2;
            }
            else
            {
                model.PropFrame.GetSectProps(section, ref area, ref as2, ref as3, ref torsion, ref i22, ref i33, ref s22, ref s33, ref z22, ref z33, ref r22, ref r33);
                areas[section] = area;
            }
        }

        return areas;
    }
}

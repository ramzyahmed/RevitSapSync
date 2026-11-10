using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitSapSync.Core.Models;
using Frame = RevitSapSync.Core.Models.Frame;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Reimplements the reference project's <c>RevitUtils.SetInsertionPoint</c>: maps
/// <see cref="InsertionPointType"/> onto the "y Justification"/"z Justification"
/// parameters (matched by the reference's literal display names, not a BuiltInParameter -
/// safer here since it's proven against these exact AECOM families) plus "Start
/// Extension"/"End Extension" from <see cref="Frame.StartZOffset"/>/<see cref="Frame.EndZOffset"/>.
/// Shared by <see cref="GeometrySyncEngine"/> (existing mapped elements) and
/// <see cref="RevitElementCreationEngine"/> (newly created ones) so both apply identical
/// logic. Only the two named cases (BottomLeft, BottomCenter) were directly visible in the
/// reference's switch during porting; the remaining combinations follow the same "Origin
/// = middle/center/unmapped" pattern the reference's own default case established.
/// </summary>
internal static class InsertionPointHelpers
{
    public static void Apply(FamilyInstance instance, Frame sapFrame, ForgeTypeId displayUnit)
    {
        var (zJustification, yJustification) = MapInsertionPoint(sapFrame.InsertionPoint);
        instance.LookupParameter("z Justification")?.Set((int)zJustification);
        instance.LookupParameter("y Justification")?.Set((int)yJustification);

        instance.LookupParameter("Start Extension")?.Set(UnitUtils.ConvertToInternalUnits(-sapFrame.StartZOffset, displayUnit));
        instance.LookupParameter("End Extension")?.Set(UnitUtils.ConvertToInternalUnits(sapFrame.EndZOffset, displayUnit));
    }

    private static (ZJustification Z, YJustification Y) MapInsertionPoint(InsertionPointType insertionPoint) => insertionPoint switch
    {
        InsertionPointType.BottomLeft => (ZJustification.Bottom, YJustification.Left),
        InsertionPointType.BottomCenter => (ZJustification.Bottom, YJustification.Origin),
        InsertionPointType.BottomRight => (ZJustification.Bottom, YJustification.Right),
        InsertionPointType.MiddleLeft => (ZJustification.Origin, YJustification.Left),
        InsertionPointType.MiddleRight => (ZJustification.Origin, YJustification.Right),
        InsertionPointType.TopLeft => (ZJustification.Top, YJustification.Left),
        InsertionPointType.TopCenter => (ZJustification.Top, YJustification.Origin),
        InsertionPointType.TopRight => (ZJustification.Top, YJustification.Right),
        _ => (ZJustification.Origin, YJustification.Origin), // Centroid, ShearCenter, and any unmapped value
    };
}

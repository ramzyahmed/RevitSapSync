using Autodesk.Revit.DB;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// The single display-unit convention shared by every Revit read/write in this project -
/// matches the reference implementation's <c>ForgeTypeId dis = UnitTypeId.Millimeters</c>,
/// used consistently so SAP-sourced and Revit-sourced <c>Frame</c> coordinates are always
/// comparable without a conversion bug creeping in at one call site but not another.
/// </summary>
internal static class RevitUnitConventions
{
    public static readonly ForgeTypeId DisplayUnit = UnitTypeId.Millimeters;
}

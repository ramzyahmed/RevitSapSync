using Autodesk.Revit.DB;
using RevitSapSync.Core.Models;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Shared point/curve conventions used by both <see cref="GeometrySyncEngine"/> (updating
/// existing elements) and <see cref="RevitElementCreationEngine"/> (creating new ones), so
/// both apply the exact same orientation rules rather than each re-deriving them.
/// </summary>
internal static class ColumnGeometryHelpers
{
    /// <summary>
    /// SAP2000 special-cases the local axis convention for a member whose local-1 axis is
    /// exactly vertical (i.e. a vertical column): local-2 defaults to parallel with global
    /// X, rather than the general "vertical-ish" rule non-vertical members (beams, slanted
    /// columns) use - which is why beams and slanted columns (both driven by the
    /// <c>Cross-Section Rotation</c> parameter, whose Beta=0 reference lines up correctly)
    /// show no rotation discrepancy, but vertical columns (driven by
    /// <c>ElementTransformUtils.RotateElement</c>) were consistently off by 90 degrees
    /// against the live SAP2000 model - the family's own rotation=0 reference direction
    /// apparently doesn't line up with SAP's local-2-parallel-to-X convention for vertical
    /// members. Confirmed empirically against a live model, not derived from the
    /// reference (which never actually exercised update-time column rotation - its own
    /// version of this logic is commented out - and wasn't verified for create-time
    /// vertical column rotation either). If this ends up rotated a further 90 degrees the
    /// wrong way after testing, flip the sign here - it's the only place this needs to change.
    /// </summary>
    private const double VerticalColumnRotationOffsetDegrees = -90.0;

    public static double GetVerticalColumnTargetRotationRadians(double betaDegrees) =>
        (betaDegrees + VerticalColumnRotationOffsetDegrees) * Math.PI / 180.0;

    public static XYZ ToXyz(Point3D point) => new(
        UnitUtils.ConvertToInternalUnits(point.X, RevitUnitConventions.DisplayUnit),
        UnitUtils.ConvertToInternalUnits(point.Y, RevitUnitConventions.DisplayUnit),
        UnitUtils.ConvertToInternalUnits(point.Z, RevitUnitConventions.DisplayUnit));

    /// <summary>
    /// Whichever orientation starts at the geometrically lower end - mirrors the
    /// reference's "z &gt; z1 -&gt; use the reversed curve" rule for slanted columns.
    /// </summary>
    public static Curve GetOrientedLine(Point3D start, Point3D end)
    {
        var startXyz = ToXyz(start);
        var endXyz = ToXyz(end);
        var line = Line.CreateBound(startXyz, endXyz);
        var reversedLine = Line.CreateBound(endXyz, startXyz);
        return startXyz.Z > endXyz.Z ? reversedLine : line;
    }

    /// <summary>
    /// Forces the curve's end point to share the start point's X/Y, guaranteeing a truly
    /// vertical curve - reimplementation of the reference's <c>alignXYForBothPoints</c>.
    /// </summary>
    public static Curve AlignXYForBothPoints(Curve curve)
    {
        var start = curve.GetEndPoint(0);
        var end = new XYZ(start.X, start.Y, curve.GetEndPoint(1).Z);
        return Line.CreateBound(start, end);
    }
}

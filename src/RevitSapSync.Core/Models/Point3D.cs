namespace RevitSapSync.Core.Models;

/// <summary>
/// A plain 3D coordinate, independent of any host application's unit system.
/// Callers are responsible for ensuring both operands of a comparison use the
/// same unit and coordinate system.
/// </summary>
public sealed record Point3D(double X, double Y, double Z)
{
    public static double Distance(Point3D a, Point3D b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        double dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public static Point3D Midpoint(Point3D a, Point3D b) =>
        new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);
}

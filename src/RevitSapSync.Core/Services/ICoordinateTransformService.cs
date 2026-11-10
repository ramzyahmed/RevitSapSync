using RevitSapSync.Core.Models;

namespace RevitSapSync.Core.Services;

/// <summary>
/// Seam for aligning SAP2000 coordinates onto Revit's coordinate system. v1 assumes the
/// two models already share a coordinate system, so the registered default
/// (<see cref="IdentityCoordinateTransformService"/>) is a no-op; a real transform can be
/// substituted later via DI without changing any caller.
/// </summary>
public interface ICoordinateTransformService
{
    Point3D TransformSapToRevit(Point3D sapPoint);
}

public sealed class IdentityCoordinateTransformService : ICoordinateTransformService
{
    public Point3D TransformSapToRevit(Point3D sapPoint) => sapPoint;
}

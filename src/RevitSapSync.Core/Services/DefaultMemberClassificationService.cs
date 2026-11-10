using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;

namespace RevitSapSync.Core.Services;

/// <summary>
/// Placeholder classification heuristic - angle-threshold classification, reimplemented
/// cleanly from the reference project's approach. This is explicitly a stand-in: the
/// project owner intends to supply their own classification method later, at which
/// point it replaces this class via a one-line DI registration change.
/// </summary>
public sealed class DefaultMemberClassificationService : IMemberClassificationService
{
    public MemberType Classify(Point3D startPoint, Point3D endPoint, ClassificationOptions options)
    {
        if (startPoint is null) throw new ArgumentNullException(nameof(startPoint));
        if (endPoint is null) throw new ArgumentNullException(nameof(endPoint));
        if (options is null) throw new ArgumentNullException(nameof(options));

        double dx = Math.Abs(endPoint.X - startPoint.X);
        double dy = Math.Abs(endPoint.Y - startPoint.Y);
        double dz = Math.Abs(endPoint.Z - startPoint.Z);

        if (dx == 0 && dy == 0)
            return MemberType.Column;

        double inclinationX = RadiansToDegrees(Math.Atan2(dx, dz));
        double inclinationY = RadiansToDegrees(Math.Atan2(dy, dz));

        bool withinColumnTolerance = inclinationX < options.ColumnInclinationToleranceDegrees
            && inclinationY < options.ColumnInclinationToleranceDegrees;

        if (withinColumnTolerance)
        {
            bool nearVertical = inclinationX < options.VerticalColumnToleranceDegrees
                && inclinationY < options.VerticalColumnToleranceDegrees;
            return nearVertical ? MemberType.Column : MemberType.SlantedColumn;
        }

        bool withinBracingTolerance = inclinationX < options.BracingInclinationToleranceDegrees
            && inclinationY < options.BracingInclinationToleranceDegrees;

        return withinBracingTolerance ? MemberType.VerticalBracing : MemberType.Beam;
    }

    private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
}

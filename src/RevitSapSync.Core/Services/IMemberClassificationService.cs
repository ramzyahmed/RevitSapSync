using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;

namespace RevitSapSync.Core.Services;

/// <summary>
/// Classifies a frame as a beam, column, slanted column, or bracing member from its
/// endpoints. Registered behind DI so a project-specific classification method can be
/// swapped in later without touching any caller.
/// </summary>
public interface IMemberClassificationService
{
    MemberType Classify(Point3D startPoint, Point3D endPoint, ClassificationOptions options);
}

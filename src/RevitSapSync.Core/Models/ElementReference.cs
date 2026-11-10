namespace RevitSapSync.Core.Models;

/// <summary>
/// Stands in for a Revit <c>ElementId</c> so Core has no RevitAPI dependency.
/// </summary>
public sealed record ElementReference(long IntegerId, string UniqueId);

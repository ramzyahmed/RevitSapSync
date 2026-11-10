namespace RevitSapSync.Core.Models;

/// <summary>
/// Explains why a specific SAP section fell back to a "NotFound(...)" placeholder symbol
/// instead of a properly provisioned one - either the shape isn't covered by any
/// provisioner yet, or provisioning was attempted and threw (e.g. the required base family
/// isn't loaded in the project). Without this, the only trace of "why" went to
/// <c>Trace.TraceWarning</c>, invisible in a normal Revit session.
/// </summary>
public sealed record PlaceholderReason
{
    public required string SapSectionName { get; init; }
    public required string Reason { get; init; }
}

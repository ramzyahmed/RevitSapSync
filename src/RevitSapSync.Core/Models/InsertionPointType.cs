namespace RevitSapSync.Core.Models;

/// <summary>
/// Mirrors SAP2000's nine-point cardinal insertion-point convention. The mapping to
/// Revit's Y/Z justification parameters is a later-milestone concern (see plan section
/// "Open items pending the user").
/// </summary>
public enum InsertionPointType
{
    BottomLeft,
    BottomCenter,
    BottomRight,
    MiddleLeft,
    Centroid,
    MiddleRight,
    TopLeft,
    TopCenter,
    TopRight,
    ShearCenter,
}

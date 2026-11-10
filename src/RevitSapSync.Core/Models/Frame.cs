namespace RevitSapSync.Core.Models;

/// <summary>
/// Unified representation of a structural line element, populated either from a SAP2000
/// frame object or from a Revit structural framing/column instance. The mapping engine
/// and geometry-comparison logic operate exclusively on this type so they stay free of
/// any Revit/SAP2000 API dependency.
/// </summary>
public sealed class Frame
{
    public required string Id { get; set; }
    public required Point3D StartPoint { get; init; }
    public required Point3D EndPoint { get; init; }
    public Point3D MidPoint => Point3D.Midpoint(StartPoint, EndPoint);
    public double Length { get; init; }

    /// <summary>Local-axis rotation, in degrees.</summary>
    public double Beta { get; set; }

    public MemberType MemberType { get; set; }
    public required FrameSource Source { get; init; }

    public string SapSectionName { get; init; } = string.Empty;

    /// <summary>
    /// The literal SAP2000 section-property name as read from <c>FrameObj.GetSection</c>,
    /// before <see cref="SapSectionName"/> is overwritten with a normalized dimension
    /// string. Only meaningful for SAP-sourced frames. Needed anywhere the actual SAP OAPI
    /// must be queried by section name (e.g. <c>PropFrame.GetSectProps</c> for the
    /// validation report's cross-section area) - matches the reference's <c>ExistSec</c>
    /// vs <c>Sec</c> distinction.
    /// </summary>
    public string RawSapSectionName { get; init; } = string.Empty;

    /// <summary>
    /// SAP2000 <c>eFramePropType</c> classification, as a string ("I", "Box", "Pipe",
    /// "Channel", "Angle", "DblAngle", "Rectangular", "Circle", "VARI", "VARBox",
    /// "VARPipe" for tapered/non-prismatic frames). Only meaningful for SAP-sourced frames.
    /// </summary>
    public string Shape { get; init; } = string.Empty;

    /// <summary>
    /// True when the section is generic/user-defined ("Built-Up" per SAP2000, i.e. not
    /// sourced from a section-property catalog file) rather than a named database
    /// section. Only meaningful for SAP-sourced frames.
    /// </summary>
    public bool IsBuiltUp { get; init; }

    /// <summary>
    /// The section-property-file name (e.g. containing "Euro"/"AISC"/"BSShapes") when
    /// <see cref="IsBuiltUp"/> is false; empty otherwise. Only meaningful for SAP-sourced frames.
    /// </summary>
    public string SectionLibrary { get; init; } = string.Empty;

    public string RevitFamilyName { get; init; } = string.Empty;
    public string RevitTypeName { get; init; } = string.Empty;

    /// <summary>Set only when <see cref="Source"/> is <see cref="FrameSource.Revit"/>.</summary>
    public ElementReference? RevitElement { get; init; }

    public InsertionPointType InsertionPoint { get; init; } = InsertionPointType.Centroid;
    public double StartZOffset { get; init; }
    public double EndZOffset { get; init; }
    public bool IsNonPrismatic { get; init; }
}

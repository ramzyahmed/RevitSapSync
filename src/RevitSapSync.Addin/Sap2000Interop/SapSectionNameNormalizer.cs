using System.Globalization;
using SAP2000v1;

namespace RevitSapSync.Sap2000Interop;

/// <summary>
/// Reimplements <c>ChangeSapSectionNames.GetSectionsDimentions2</c> and its <c>reName*</c>
/// helpers: queries SAP2000's actual section geometry (via <c>PropFrame.GetXxx</c>) for
/// the shapes that need it, and builds the exact dimension-string naming convention the
/// Revit-side family lookup (<c>createRvtFamilySectionForBeam</c>/<c>ForColumn</c>) expects.
/// <para>
/// <b>Confirmed from the reference source</b>: <c>reNameChannelsections2</c>,
/// <c>reNameTsections2</c>, and <c>reNameSingleAngle2</c> are stubs that return an empty
/// list - Channel/Tee/Angle/DblAngle sections are never renamed in practice and pass
/// through with their original SAP catalog name, which works because the downstream
/// Revit-side parsers for those shapes extract a trailing numeric suffix from whatever
/// name they're given. This is reproduced faithfully, not "fixed" into behavior the
/// reference never had.
/// </para>
/// <para>
/// Catalog (non-built-up) I-sections also pass through unchanged by design - the
/// Revit-side dispatch matches those via <c>Sec.Contains("HE")</c>/<c>"IP"</c>/etc. plus
/// <see cref="SapSectionClassification.Library"/>, not a synthesized dimension string.
/// </para>
/// </summary>
public static class SapSectionNameNormalizer
{
    public static string Normalize(cSapModel model, SapSectionClassification classification, string originalSectionName)
    {
        return classification.Shape switch
        {
            "I" when classification.IsBuiltUp => NormalizeISection(model, classification.DimensionLookupName),
            "VARI" => NormalizeTaperedISection(model, classification),
            "Box" => NormalizeBoxSection(model, classification.DimensionLookupName),
            "VARBox" => NormalizeTaperedBoxSection(model, classification),
            "Pipe" => NormalizePipeSection(model, classification.DimensionLookupName),
            "VARPipe" => NormalizeTaperedPipeSection(model, classification),
            "Rectangular" => NormalizeSolidRectangle(model, classification.DimensionLookupName),
            "Circle" => NormalizeSolidCircle(model, classification.DimensionLookupName),
            _ => originalSectionName,
        };
    }

    private static string NormalizeISection(cSapModel model, string sectionName)
    {
        var (depth, width, flangeThick, webThick) = GetISectionDimensions(model, sectionName);
        double webDepth = depth - 2 * flangeThick;
        return $"2FL. PL. {F(width)}X{F(flangeThick)} +WEB PL. {F(webDepth)}X{F(webThick)}";
    }

    private static string NormalizeTaperedISection(cSapModel model, SapSectionClassification classification)
    {
        var start = GetISectionDimensions(model, classification.DimensionLookupName);
        var end = GetISectionDimensions(model, classification.TaperedEndLookupName!);

        double startWebDepth = start.Depth - 2 * start.FlangeThick;
        double endWebDepth = end.Depth - 2 * start.FlangeThick;
        double largeWebDepth = Math.Max(startWebDepth, endWebDepth);
        double smallWebDepth = Math.Min(startWebDepth, endWebDepth);

        // Reference's reNameITaperedsections2 takes flange width/thickness and web
        // thickness from the start segment only - only depth is treated as tapering.
        return $"2FL. PL. {F(start.Width)}X{F(start.FlangeThick)} +WEB PL. {F(largeWebDepth)}/{F(smallWebDepth)}X{F(start.WebThick)}";
    }

    private static string NormalizeBoxSection(cSapModel model, string sectionName)
    {
        var (depth, width, flangeThick, webThick) = GetTubeDimensions(model, sectionName);
        return flangeThick == webThick
            ? $"TUBE {F(depth)}X{F(width)}X{F(flangeThick)}"
            : $"TUBE {F(depth)}X{F(width)}X{F(webThick)}X{F(flangeThick)}";
    }

    private static string NormalizeTaperedBoxSection(cSapModel model, SapSectionClassification classification)
    {
        var start = GetTubeDimensions(model, classification.DimensionLookupName);
        var end = GetTubeDimensions(model, classification.TaperedEndLookupName!);

        bool constDepth = start.Depth == end.Depth;
        bool constWidth = start.Width == end.Width;
        double largeDepth = Math.Max(start.Depth, end.Depth);
        double smallDepth = Math.Min(start.Depth, end.Depth);
        double largeWidth = Math.Max(start.Width, end.Width);
        double smallWidth = Math.Min(start.Width, end.Width);

        string depthPart = constDepth ? F(start.Depth) : $"{F(largeDepth)}/{F(smallDepth)}";
        string widthPart = constWidth ? F(start.Width) : $"{F(largeWidth)}/{F(smallWidth)}";
        string thicknessPart = start.FlangeThick == start.WebThick
            ? F(start.FlangeThick)
            : $"{F(start.FlangeThick)}X{F(start.WebThick)}";

        return $"TUBE {depthPart}X{widthPart}X{thicknessPart}";
    }

    private static string NormalizePipeSection(cSapModel model, string sectionName)
    {
        var (diameter, thickness) = GetPipeDimensions(model, sectionName);
        return $"PIPE {F(diameter)}X{F(thickness)}";
    }

    private static string NormalizeTaperedPipeSection(cSapModel model, SapSectionClassification classification)
    {
        var start = GetPipeDimensions(model, classification.DimensionLookupName);
        var end = GetPipeDimensions(model, classification.TaperedEndLookupName!);

        if (start.Diameter == end.Diameter)
            return $"PIPE {F(start.Diameter)}X{F(start.Thickness)}";

        double largeDiameter = Math.Max(start.Diameter, end.Diameter);
        double smallDiameter = Math.Min(start.Diameter, end.Diameter);
        return $"PIPE {F(largeDiameter)}/{F(smallDiameter)}X{F(start.Thickness)}";
    }

    private static string NormalizeSolidRectangle(cSapModel model, string sectionName)
    {
        string? fileName = null, materialProperty = null, notes = null, guid = null;
        double depth = 0, width = 0;
        int color = 0;
        model.PropFrame.GetRectangle(sectionName, ref fileName, ref materialProperty, ref depth, ref width, ref color, ref notes, ref guid);
        return $"SolidRectangle {F(width)}X{F(depth)}";
    }

    private static string NormalizeSolidCircle(cSapModel model, string sectionName)
    {
        string? fileName = null, materialProperty = null, notes = null, guid = null;
        double diameter = 0;
        int color = 0;
        model.PropFrame.GetCircle(sectionName, ref fileName, ref materialProperty, ref diameter, ref color, ref notes, ref guid);
        return diameter > 200 ? $"BM Cir {F(diameter)}" : $"ROD {F(diameter)}";
    }

    private static (double Depth, double Width, double FlangeThick, double WebThick) GetISectionDimensions(cSapModel model, string sectionName)
    {
        string? fileName = null, materialProperty = null, notes = null, guid = null;
        double depth = 0, width = 0, flangeThick = 0, webThick = 0, bottomWidth = 0, bottomFlangeThick = 0;
        int color = 0;
        model.PropFrame.GetISection(sectionName, ref fileName, ref materialProperty, ref depth, ref width,
            ref flangeThick, ref webThick, ref bottomWidth, ref bottomFlangeThick, ref color, ref notes, ref guid);
        return (depth, width, flangeThick, webThick);
    }

    private static (double Depth, double Width, double FlangeThick, double WebThick) GetTubeDimensions(cSapModel model, string sectionName)
    {
        string? fileName = null, materialProperty = null, notes = null, guid = null;
        double depth = 0, width = 0, flangeThick = 0, webThick = 0;
        int color = 0;
        model.PropFrame.GetTube(sectionName, ref fileName, ref materialProperty, ref depth, ref width, ref flangeThick, ref webThick, ref color, ref notes, ref guid);
        return (depth, width, flangeThick, webThick);
    }

    private static (double Diameter, double Thickness) GetPipeDimensions(cSapModel model, string sectionName)
    {
        string? fileName = null, materialProperty = null, notes = null, guid = null;
        double diameter = 0, thickness = 0;
        int color = 0;
        model.PropFrame.GetPipe(sectionName, ref fileName, ref materialProperty, ref diameter, ref thickness, ref color, ref notes, ref guid);
        return (diameter, thickness);
    }

    // The reference concatenates raw OAPI doubles with no rounding at all, which leaks
    // binary floating-point noise into the name for any non-integer dimension (e.g. a
    // 139.7mm pipe OD comes back from the OAPI as 139.70000000000002) - confirmed against
    // a live model. That noise then breaks the Revit-side FamilySymbol.Name.Contains(...)
    // match outright, since no one names a family type "139.70000000000002x10". Rounding
    // to 3 decimal places (dimensions in this domain are never specified finer than that)
    // fixes the match without changing the intended value.
    private static string F(double value) => Math.Round(value, 3).ToString(CultureInfo.InvariantCulture);
}

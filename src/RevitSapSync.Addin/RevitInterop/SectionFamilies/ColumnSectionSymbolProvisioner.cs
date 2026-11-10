using Autodesk.Revit.DB;
using Frame = RevitSapSync.Core.Models.Frame;
using static RevitSapSync.RevitInterop.SectionFamilies.SectionSymbolProvisioningHelpers;

namespace RevitSapSync.RevitInterop.SectionFamilies;

/// <summary>
/// Reimplements <c>RevitUtils.createRvtFamilySectionForColumn</c>. Same shape coverage
/// as <see cref="BeamSectionSymbolProvisioner"/>, plus Concrete Circular (column-only in
/// the reference). Channel and Angle/DblAngle are not yet ported.
/// </summary>
public sealed class ColumnSectionSymbolProvisioner : ISectionSymbolProvisioner
{
    private const BuiltInCategory Category = BuiltInCategory.OST_StructuralColumns;

    public FamilySymbol? Provision(Document document, Frame frame, ForgeTypeId displayUnit) => frame.Shape switch
    {
        "Pipe" => ProvisionPipe(document, frame, displayUnit),
        "Box" => ProvisionTube(document, frame, displayUnit),
        "I" when frame.IsBuiltUp => ProvisionBuiltUpI(document, frame, displayUnit),
        "I" when !frame.IsBuiltUp => ProvisionCatalogI(document, frame),
        "VARI" => ProvisionTaperedI(document, frame, displayUnit),
        "VARBox" => ProvisionTaperedBox(document, frame, displayUnit),
        "VARPipe" => ProvisionTaperedPipe(document, frame, displayUnit),
        "Rectangular" => ProvisionConcreteRectangular(document, frame, displayUnit),
        "Circle" => ProvisionConcreteCircular(document, frame, displayUnit),
        _ => null,
    };

    /// <summary>Same lookup-only behavior as the beam version - see its remarks.</summary>
    private static FamilySymbol? ProvisionCatalogI(Document document, Frame frame)
    {
        string familyType = CatalogISectionClassifier.Classify(frame.SapSectionName, frame.SectionLibrary) ?? string.Empty;
        string digits = SectionDimensionStringParser.AllDigits(frame.SapSectionName);
        string suffixUpper = SectionDimensionStringParser.DimensionSuffix(frame.SapSectionName).ToUpperInvariant();

        return familyType switch
        {
            "HEAA" => FindSymbol(document, Category, ColumnFamilyNames.Heaa.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "HEA" => FindSymbol(document, Category, ColumnFamilyNames.Hea.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "HEB" => FindSymbol(document, Category, ColumnFamilyNames.Heb.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "HEM" => FindSymbol(document, Category, ColumnFamilyNames.Hem.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "IPE" => FindSymbol(document, Category, ColumnFamilyNames.Ipe.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "IPEA" => FindSymbol(document, Category, ColumnFamilyNames.Ipea.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "IPEO" => FindSymbol(document, Category, ColumnFamilyNames.Ipeo.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "UB" => FindSymbol(document, Category, ColumnFamilyNames.Ub.FamilyName, s => s.Name.ToUpperInvariant().Contains(suffixUpper)),
            "UC" => FindSymbol(document, Category, ColumnFamilyNames.Uc.FamilyName, s => s.Name.ToUpperInvariant().Contains(suffixUpper)),
            "W" => FindSymbol(document, Category, ColumnFamilyNames.W.FamilyName, s => s.Name.ToUpperInvariant().Contains(suffixUpper)),
            _ => null,
        };
    }

    /// <summary>
    /// Which physical end (Start/End point) is higher in elevation decides which named
    /// "Bottom"/"Top" parameter gets the first vs. second parsed depth/diameter value -
    /// mirrors the reference's <c>cl.StartPoint.Z &gt; cl.EndPoint.Z</c> parameter-name swap.
    /// </summary>
    private static FamilySymbol ProvisionTaperedBox(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        var existing = FindSymbol(document, Category, ColumnFamilyNames.BusTubeTapered.FamilyName, s => s.Name == frame.SapSectionName);
        if (existing is not null)
            return existing;

        var (firstDepth, secondDepth, firstWidth, secondWidth, flangeThick, webThick) =
            SectionDimensionStringParser.ParseTaperedBox(frame.SapSectionName);

        bool startIsHigher = frame.StartPoint.Z > frame.EndPoint.Z;
        string startWebDepthParam = startIsHigher ? ColumnFamilyNames.BusTubeTapered.EndWebDepth : ColumnFamilyNames.BusTubeTapered.StartWebDepth;
        string endWebDepthParam = startIsHigher ? ColumnFamilyNames.BusTubeTapered.StartWebDepth : ColumnFamilyNames.BusTubeTapered.EndWebDepth;

        var symbol = DuplicateAndRename(document, Category, ColumnFamilyNames.BusTubeTapered.FamilyName, frame.SapSectionName);
        // Fixed vs. the reference: it looked up "end web thickness" via the EndFlangeThick
        // parameter name ("Top tf") instead of the family's real, distinct EndWebThick
        // parameter ("Top tw"), so "Top tw" was never actually set. Now sets both
        // Start/EndWebThick correctly, matching StartWebThick's already-correct behavior.
        SetParameters(document, symbol, displayUnit,
            (startWebDepthParam, firstDepth),
            (endWebDepthParam, secondDepth),
            (ColumnFamilyNames.BusTubeTapered.StartFlangeWidth, firstWidth),
            (ColumnFamilyNames.BusTubeTapered.EndFlangeWidth, secondWidth),
            (ColumnFamilyNames.BusTubeTapered.StartWebThick, webThick),
            (ColumnFamilyNames.BusTubeTapered.EndWebThick, webThick),
            (ColumnFamilyNames.BusTubeTapered.StartFlangeThick, flangeThick),
            (ColumnFamilyNames.BusTubeTapered.EndFlangeThick, flangeThick));
        return symbol;
    }

    /// <summary>
    /// Fixed vs. the reference: it set "End Outer Diameter" from the just-computed "End
    /// Inner Diameter" value instead of the end outer-diameter value that was actually
    /// parsed (<c>pra_d_1.Set(pra_c_2)</c> in the original - a copy-paste slip). Now sets
    /// it from <c>secondOuterDiameter</c> as intended.
    /// </summary>
    private static FamilySymbol ProvisionTaperedPipe(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        var existing = FindSymbol(document, Category, ColumnFamilyNames.BusPipeTapered.FamilyName, s => s.Name == frame.SapSectionName);
        if (existing is not null)
            return existing;

        var (firstOuterDiameter, secondOuterDiameter, thickness) = SectionDimensionStringParser.ParseTaperedPipe(frame.SapSectionName);
        double firstInnerDiameter = firstOuterDiameter - 2 * thickness;
        double secondInnerDiameter = secondOuterDiameter - 2 * thickness;

        bool startIsHigher = frame.StartPoint.Z > frame.EndPoint.Z;
        string startInnerParam = startIsHigher ? ColumnFamilyNames.BusPipeTapered.EndInnerDiameter : ColumnFamilyNames.BusPipeTapered.StartInnerDiameter;
        string startOuterParam = startIsHigher ? ColumnFamilyNames.BusPipeTapered.EndOuterDiameter : ColumnFamilyNames.BusPipeTapered.StartOuterDiameter;
        string endInnerParam = startIsHigher ? ColumnFamilyNames.BusPipeTapered.StartInnerDiameter : ColumnFamilyNames.BusPipeTapered.EndInnerDiameter;
        string endOuterParam = startIsHigher ? ColumnFamilyNames.BusPipeTapered.StartOuterDiameter : ColumnFamilyNames.BusPipeTapered.EndOuterDiameter;

        var symbol = DuplicateAndRename(document, Category, ColumnFamilyNames.BusPipeTapered.FamilyName, frame.SapSectionName);
        SetParameters(document, symbol, displayUnit,
            (startInnerParam, firstInnerDiameter),
            (startOuterParam, firstOuterDiameter),
            (endInnerParam, secondInnerDiameter),
            (endOuterParam, secondOuterDiameter));
        return symbol;
    }

    private static FamilySymbol ProvisionPipe(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        string suffix = SectionDimensionStringParser.DimensionSuffix(frame.SapSectionName);
        var dimensions = SectionDimensionStringParser.SplitDimensions(frame.SapSectionName);

        var existing = FindSymbol(document, Category, ColumnFamilyNames.Pipe.FamilyName, s => s.Name.Contains(suffix))
            ?? FindSymbolCaseInsensitiveFallback(document, Category, ColumnFamilyNames.Pipe.FamilyName, suffix, requiredXCount: null);
        if (existing is not null)
            return existing;

        var symbol = DuplicateAndRename(document, Category, ColumnFamilyNames.Pipe.FamilyName, $"PIPE {suffix}");
        SetParameters(document, symbol, displayUnit,
            (ColumnFamilyNames.Pipe.OuterDiameter, SectionDimensionStringParser.ParseDouble(dimensions[0])),
            (ColumnFamilyNames.Pipe.Thickness, SectionDimensionStringParser.ParseDouble(dimensions[1])));
        return symbol;
    }

    private static FamilySymbol ProvisionTube(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        string suffix = SectionDimensionStringParser.DimensionSuffix(frame.SapSectionName);
        var dimensions = SectionDimensionStringParser.SplitDimensions(frame.SapSectionName);
        int xCount = SectionDimensionStringParser.CountX(frame.SapSectionName);

        var existing = FindSymbol(document, Category, ColumnFamilyNames.Tube.FamilyName, s => s.Name.Contains(suffix) && s.Name.Count(c => c == 'X') == xCount)
            ?? FindSymbolCaseInsensitiveFallback(document, Category, ColumnFamilyNames.Tube.FamilyName, suffix, xCount);
        if (existing is not null)
            return existing;

        var symbol = DuplicateAndRename(document, Category, ColumnFamilyNames.Tube.FamilyName, $"TUBE {suffix}");
        double flangeThick = SectionDimensionStringParser.ParseDouble(dimensions.Length == 4 ? dimensions[3] : dimensions[2]);
        SetParameters(document, symbol, displayUnit,
            (ColumnFamilyNames.Tube.WebDepth, SectionDimensionStringParser.ParseDouble(dimensions[0])),
            (ColumnFamilyNames.Tube.FlangeWidth, SectionDimensionStringParser.ParseDouble(dimensions[1])),
            (ColumnFamilyNames.Tube.WebThick, SectionDimensionStringParser.ParseDouble(dimensions[2])),
            (ColumnFamilyNames.Tube.FlangeThick, flangeThick),
            (ColumnFamilyNames.Tube.OuterRadius, ColumnFamilyNames.Tube.DefaultRadius),
            (ColumnFamilyNames.Tube.InnerRadius, ColumnFamilyNames.Tube.DefaultRadius));
        return symbol;
    }

    private static FamilySymbol ProvisionBuiltUpI(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        var existing = FindSymbol(document, Category, ColumnFamilyNames.BusI.FamilyName, s => s.Name == frame.SapSectionName);
        if (existing is not null)
            return existing;

        var (webThick, flangeThick, totalDepth, flangeWidth) = SectionDimensionStringParser.ParseBuiltUpI(frame.SapSectionName);

        var symbol = DuplicateAndRename(document, Category, ColumnFamilyNames.BusI.FamilyName, frame.SapSectionName);
        SetParameters(document, symbol, displayUnit,
            (ColumnFamilyNames.BusI.TotalDepth, totalDepth),
            (ColumnFamilyNames.BusI.FlangeWidth, flangeWidth),
            (ColumnFamilyNames.BusI.WebThick, webThick),
            (ColumnFamilyNames.BusI.FlangeThick, flangeThick));
        return symbol;
    }

    /// <summary>
    /// Unlike the beam version, the column family has separate Bott(om)/Top depth
    /// parameters, so which one gets the larger parsed depth depends on which physical
    /// end (Start/End point) is actually higher in elevation - mirrors the reference's
    /// <c>cl.StartPoint.Z &gt; cl.EndPoint.Z</c> parameter-name swap.
    /// </summary>
    private static FamilySymbol ProvisionTaperedI(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        var existing = FindSymbol(document, Category, ColumnFamilyNames.BusITapered.FamilyName, s => s.Name == frame.SapSectionName);
        if (existing is not null)
            return existing;

        var (webThick, flangeThick, largeWebDepth, smallWebDepth, flangeWidth) = SectionDimensionStringParser.ParseTaperedI(frame.SapSectionName);

        bool startIsHigher = frame.StartPoint.Z > frame.EndPoint.Z;
        string startWebDepthParam = startIsHigher ? ColumnFamilyNames.BusITapered.EndWebDepth : ColumnFamilyNames.BusITapered.StartWebDepth;
        string endWebDepthParam = startIsHigher ? ColumnFamilyNames.BusITapered.StartWebDepth : ColumnFamilyNames.BusITapered.EndWebDepth;

        var symbol = DuplicateAndRename(document, Category, ColumnFamilyNames.BusITapered.FamilyName, frame.SapSectionName);
        SetParameters(document, symbol, displayUnit,
            (startWebDepthParam, largeWebDepth),
            (endWebDepthParam, smallWebDepth),
            (ColumnFamilyNames.BusITapered.StartFlangeWidth, flangeWidth),
            (ColumnFamilyNames.BusITapered.EndFlangeWidth, flangeWidth),
            (ColumnFamilyNames.BusITapered.WebThick, webThick),
            (ColumnFamilyNames.BusITapered.FlangeThick, flangeThick));
        return symbol;
    }

    private static FamilySymbol ProvisionConcreteRectangular(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        string suffix = SectionDimensionStringParser.DimensionSuffix(frame.SapSectionName);
        var dimensions = SectionDimensionStringParser.SplitDimensions(frame.SapSectionName);

        var existing = FindSymbol(document, Category, ColumnFamilyNames.ConcreteRectangular.FamilyName, s => s.Name.Contains(suffix));
        if (existing is not null)
            return existing;

        var symbol = DuplicateAndRename(document, Category, ColumnFamilyNames.ConcreteRectangular.FamilyName, $"COL {suffix}");
        SetParameters(document, symbol, displayUnit,
            (ColumnFamilyNames.ConcreteRectangular.Width, SectionDimensionStringParser.ParseDouble(dimensions[0])),
            (ColumnFamilyNames.ConcreteRectangular.Depth, SectionDimensionStringParser.ParseDouble(dimensions[1])));
        return symbol;
    }

    private static FamilySymbol ProvisionConcreteCircular(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        string suffix = SectionDimensionStringParser.DimensionSuffix(frame.SapSectionName);
        var dimensions = SectionDimensionStringParser.SplitDimensions(frame.SapSectionName);

        var existing = FindSymbol(document, Category, ColumnFamilyNames.ConcreteCircular.FamilyName, s => s.Name.Contains(suffix));
        if (existing is not null)
            return existing;

        var symbol = DuplicateAndRename(document, Category, ColumnFamilyNames.ConcreteCircular.FamilyName, $"COL {suffix}");
        SetParameters(document, symbol, displayUnit,
            (ColumnFamilyNames.ConcreteCircular.Diameter, SectionDimensionStringParser.ParseDouble(dimensions[0])));
        return symbol;
    }
}

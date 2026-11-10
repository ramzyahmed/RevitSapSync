using Autodesk.Revit.DB;
using Frame = RevitSapSync.Core.Models.Frame;
using static RevitSapSync.RevitInterop.SectionFamilies.SectionSymbolProvisioningHelpers;

namespace RevitSapSync.RevitInterop.SectionFamilies;

/// <summary>
/// Reimplements <c>RevitUtils.createRvtFamilySectionForBeam</c>. Covers built-up I,
/// tapered I/Box/Pipe, Pipe, Box/Tube, catalog I-sections (HEA/HEAA/HEB/HEM/IPE/IPEA/IPEO/
/// UB/UC/W), and Concrete Rectangular. Channel and Angle/DblAngle are not yet ported -
/// <see cref="Provision"/> returns null for those shapes, and callers fall back to a
/// "NotFound" placeholder symbol exactly as the reference does, so nothing breaks - it's
/// simply not-yet-matched.
/// </summary>
public sealed class BeamSectionSymbolProvisioner : ISectionSymbolProvisioner
{
    private const BuiltInCategory Category = BuiltInCategory.OST_StructuralFraming;

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
        _ => null,
    };

    /// <summary>
    /// Catalog (non-built-up) I-sections are never created if missing in the reference -
    /// only looked up against families already loaded into the project - so this can
    /// legitimately return null even when the shape is recognized.
    /// </summary>
    private static FamilySymbol? ProvisionCatalogI(Document document, Frame frame)
    {
        string familyType = CatalogISectionClassifier.Classify(frame.SapSectionName, frame.SectionLibrary) ?? string.Empty;
        string digits = SectionDimensionStringParser.AllDigits(frame.SapSectionName);
        string suffixUpper = SectionDimensionStringParser.DimensionSuffix(frame.SapSectionName).ToUpperInvariant();

        return familyType switch
        {
            "HEAA" => FindSymbol(document, Category, BeamFamilyNames.Heaa.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "HEA" => FindSymbol(document, Category, BeamFamilyNames.Hea.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "HEB" => FindSymbol(document, Category, BeamFamilyNames.Heb.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "HEM" => FindSymbol(document, Category, BeamFamilyNames.Hem.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "IPE" => FindSymbol(document, Category, BeamFamilyNames.Ipe.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "IPEA" => FindSymbol(document, Category, BeamFamilyNames.Ipea.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "IPEO" => FindSymbol(document, Category, BeamFamilyNames.Ipeo.FamilyName, s => SectionDimensionStringParser.AllDigits(s.Name) == digits),
            "UB" => FindSymbol(document, Category, BeamFamilyNames.Ub.FamilyName, s => s.Name.ToUpperInvariant().Contains(suffixUpper)),
            "UC" => FindSymbol(document, Category, BeamFamilyNames.Uc.FamilyName, s => s.Name.ToUpperInvariant().Contains(suffixUpper)),
            "W" => FindSymbol(document, Category, BeamFamilyNames.W.FamilyName, s => s.Name.ToUpperInvariant().Contains(suffixUpper)),
            _ => null,
        };
    }

    private static FamilySymbol ProvisionTaperedBox(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        var existing = FindSymbol(document, Category, BeamFamilyNames.BusTubeFullyTapered.FamilyName, s => s.Name == frame.SapSectionName);
        if (existing is not null)
            return existing;

        var (firstDepth, secondDepth, firstWidth, seondWidth, flangeThick, webThick) =
            SectionDimensionStringParser.ParseTaperedBox(frame.SapSectionName);

        var symbol = DuplicateAndRename(document, Category, BeamFamilyNames.BusTubeFullyTapered.FamilyName, frame.SapSectionName);
        // Reference reconstructs total depth (webDepth + 2*flangeThick) here, same pattern
        // as the built-up-I case - but does NOT do so for the column tapered-tube family.
        SetParameters(document, symbol, displayUnit,
            (BeamFamilyNames.BusTubeFullyTapered.StartTotalDepth, firstDepth ),
            (BeamFamilyNames.BusTubeFullyTapered.EndTotalDepth, secondDepth),
            (BeamFamilyNames.BusTubeFullyTapered.StartFlangeWidth, firstWidth),
            (BeamFamilyNames.BusTubeFullyTapered.EndFlangeWidth, seondWidth), // reference sets both ends from the same (start) width value
            (BeamFamilyNames.BusTubeFullyTapered.WebThick, webThick),
            (BeamFamilyNames.BusTubeFullyTapered.FlangeThick, flangeThick));
        return symbol;
    }

    private static FamilySymbol ProvisionTaperedPipe(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        var existing = FindSymbol(document, Category, BeamFamilyNames.BusPipeTapered.FamilyName, s => s.Name == frame.SapSectionName);
        if (existing is not null)
            return existing;

        var (firstDiameter, secondDiameter, thickness) = SectionDimensionStringParser.ParseTaperedPipe(frame.SapSectionName);

        var symbol = DuplicateAndRename(document, Category, BeamFamilyNames.BusPipeTapered.FamilyName, frame.SapSectionName);
        SetParameters(document, symbol, displayUnit,
            (BeamFamilyNames.BusPipeTapered.StartDiameter, firstDiameter),
            (BeamFamilyNames.BusPipeTapered.EndDiameter, secondDiameter),
            (BeamFamilyNames.BusPipeTapered.StartThick, thickness),
            (BeamFamilyNames.BusPipeTapered.EndThick, thickness)); // reference uses the same single parsed thickness for both ends
        return symbol;
    }

    private static FamilySymbol ProvisionPipe(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        string suffix = SectionDimensionStringParser.DimensionSuffix(frame.SapSectionName);
        var dimensions = SectionDimensionStringParser.SplitDimensions(frame.SapSectionName);

        var existing = FindSymbol(document, Category, BeamFamilyNames.Pipe.FamilyName, s => s.Name.Contains(suffix))
            ?? FindSymbolCaseInsensitiveFallback(document, Category, BeamFamilyNames.Pipe.FamilyName, suffix, requiredXCount: null);
        if (existing is not null)
            return existing;

        var symbol = DuplicateAndRename(document, Category, BeamFamilyNames.Pipe.FamilyName, $"PIPE {suffix}");
        SetParameters(document, symbol, displayUnit,
            (BeamFamilyNames.Pipe.OuterDiameter, SectionDimensionStringParser.ParseDouble(dimensions[0])),
            (BeamFamilyNames.Pipe.Thickness, SectionDimensionStringParser.ParseDouble(dimensions[1])));
        return symbol;
    }

    private static FamilySymbol ProvisionTube(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        string suffix = SectionDimensionStringParser.DimensionSuffix(frame.SapSectionName);
        var dimensions = SectionDimensionStringParser.SplitDimensions(frame.SapSectionName);
        int xCount = SectionDimensionStringParser.CountX(frame.SapSectionName);

        var existing = FindSymbol(document, Category, BeamFamilyNames.Tube.FamilyName, s => s.Name.Contains(suffix) && s.Name.Count(c => c == 'X') == xCount)
            ?? FindSymbolCaseInsensitiveFallback(document, Category, BeamFamilyNames.Tube.FamilyName, suffix, xCount);
        if (existing is not null)
            return existing;

        var symbol = DuplicateAndRename(document, Category, BeamFamilyNames.Tube.FamilyName, $"TUBE {suffix}");
        double flangeThick = SectionDimensionStringParser.ParseDouble(dimensions.Length == 4 ? dimensions[3] : dimensions[2]);
        SetParameters(document, symbol, displayUnit,
            (BeamFamilyNames.Tube.WebDepth, SectionDimensionStringParser.ParseDouble(dimensions[0])),
            (BeamFamilyNames.Tube.FlangeWidth, SectionDimensionStringParser.ParseDouble(dimensions[1])),
            (BeamFamilyNames.Tube.WebThick, SectionDimensionStringParser.ParseDouble(dimensions[2])),
            (BeamFamilyNames.Tube.FlangeThick, flangeThick),
            (BeamFamilyNames.Tube.OuterRadius, BeamFamilyNames.Tube.DefaultRadius),
            (BeamFamilyNames.Tube.InnerRadius, BeamFamilyNames.Tube.DefaultRadius));
        return symbol;
    }

    private static FamilySymbol ProvisionBuiltUpI(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        var existing = FindSymbol(document, Category, BeamFamilyNames.BusI.FamilyName, s => s.Name == frame.SapSectionName);
        if (existing is not null)
            return existing;

        var (webThick, flangeThick, totalDepth, flangeWidth) = SectionDimensionStringParser.ParseBuiltUpI(frame.SapSectionName);

        var symbol = DuplicateAndRename(document, Category, BeamFamilyNames.BusI.FamilyName, frame.SapSectionName);
        SetParameters(document, symbol, displayUnit,
            (BeamFamilyNames.BusI.TotalDepth, totalDepth),
            (BeamFamilyNames.BusI.FlangeWidth, flangeWidth),
            (BeamFamilyNames.BusI.WebThick, webThick),
            (BeamFamilyNames.BusI.FlangeThick, flangeThick));
        return symbol;
    }

    private static FamilySymbol ProvisionTaperedI(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        var existing = FindSymbol(document, Category, BeamFamilyNames.BusITapered.FamilyName, s => s.Name == frame.SapSectionName);
        if (existing is not null)
            return existing;

        var (webThick, flangeThick, largeWebDepth, smallWebDepth, flangeWidth) = SectionDimensionStringParser.ParseTaperedI(frame.SapSectionName);

        var symbol = DuplicateAndRename(document, Category, BeamFamilyNames.BusITapered.FamilyName, frame.SapSectionName);
        SetParameters(document, symbol, displayUnit,
            (BeamFamilyNames.BusITapered.StartWebDepth, largeWebDepth),
            (BeamFamilyNames.BusITapered.EndWebDepth, smallWebDepth),
            (BeamFamilyNames.BusITapered.FlangeWidth, flangeWidth),
            (BeamFamilyNames.BusITapered.WebThick, webThick),
            (BeamFamilyNames.BusITapered.FlangeThick, flangeThick));
        return symbol;
    }

    private static FamilySymbol ProvisionConcreteRectangular(Document document, Frame frame, ForgeTypeId displayUnit)
    {
        string suffix = SectionDimensionStringParser.DimensionSuffix(frame.SapSectionName);
        var dimensions = SectionDimensionStringParser.SplitDimensions(frame.SapSectionName);

        var existing = FindSymbol(document, Category, BeamFamilyNames.ConcreteRectangular.FamilyName, s => s.Name.Contains(suffix));
        if (existing is not null)
            return existing;

        var symbol = DuplicateAndRename(document, Category, BeamFamilyNames.ConcreteRectangular.FamilyName, $"BM {suffix}");
        SetParameters(document, symbol, displayUnit,
            (BeamFamilyNames.ConcreteRectangular.Width, SectionDimensionStringParser.ParseDouble(dimensions[0])),
            (BeamFamilyNames.ConcreteRectangular.Depth, SectionDimensionStringParser.ParseDouble(dimensions[1])));
        return symbol;
    }
}

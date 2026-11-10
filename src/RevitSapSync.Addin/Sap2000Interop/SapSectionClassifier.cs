using SAP2000v1;

namespace RevitSapSync.Sap2000Interop;

/// <summary>
/// Classifies a SAP2000 frame section using the same two OAPI calls the reference
/// implementation uses (<c>PropFrame.GetTypeOAPI</c>, <c>PropFrame.GetNameInPropFile</c>).
/// </summary>
public static class SapSectionClassifier
{
    public static SapSectionClassification Classify(cSapModel model, string sectionName)
    {
        var propType = eFramePropType.Angle;
        model.PropFrame.GetTypeOAPI(sectionName, ref propType);

        string shape;
        string dimensionLookupName = sectionName;
        string? taperedEndLookupName = null;

        if (propType == eFramePropType.Variable)
        {
            // Non-prismatic (tapered) sections have no dimensions of their own - the
            // reference reads the first segment's underlying shape/dimensions and
            // prefixes "VAR", exactly mirrored here.
            int numberOfItems = 0;
            string[]? startSections = null;
            string[]? endSections = null;
            double[]? lengths = null;
            int[]? types = null;
            int[]? ei33 = null;
            int[]? ei22 = null;
            int color = 0;
            string? notes = null;
            string? guid = null;

            model.PropFrame.GetNonPrismatic(sectionName, ref numberOfItems, ref startSections, ref endSections,
                ref lengths, ref types, ref ei33, ref ei22, ref color, ref notes, ref guid);

            dimensionLookupName = startSections![0];
            taperedEndLookupName = endSections![0];

            var baseShape = eFramePropType.Angle;
            model.PropFrame.GetTypeOAPI(dimensionLookupName, ref baseShape);
            shape = baseShape switch
            {
                eFramePropType.I => "VARI",
                eFramePropType.Box => "VARBox",
                eFramePropType.Pipe => "VARPipe",
                _ => "VAR" + baseShape,
            };
        }
        else
        {
            shape = propType.ToString();
        }

        string? nameInFile = null;
        string? fileName = null;
        string? materialProperty = null;
        var propTypeOut = propType;
        model.PropFrame.GetNameInPropFile(dimensionLookupName, ref nameInFile, ref fileName, ref materialProperty, ref propTypeOut);

        bool isBuiltUp = string.IsNullOrEmpty(nameInFile);
        string library = isBuiltUp ? string.Empty : (fileName ?? string.Empty);

        return new SapSectionClassification(shape, isBuiltUp, library, dimensionLookupName, taperedEndLookupName);
    }
}

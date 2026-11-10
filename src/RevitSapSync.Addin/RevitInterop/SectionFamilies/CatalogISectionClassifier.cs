namespace RevitSapSync.RevitInterop.SectionFamilies;

/// <summary>
/// Reimplements the catalog-I-section family disambiguation from
/// <c>createRvtFamilySectionForBeam</c>/<c>ForColumn</c>'s "I_H" region: for a catalog
/// (non-built-up) I-section, decides which named family (HEA/HEB/HEM/IPE-variants/UB/UC/W)
/// it belongs to, from the raw SAP catalog section name plus its source library.
/// </summary>
internal static class CatalogISectionClassifier
{
    /// <summary>
    /// Returns one of "HEAA"/"HEA"/"HEB"/"HEM"/"IPE"/"IPEA"/"IPEO"/"UB"/"UC"/"W", or null
    /// if the section doesn't match any known catalog family.
    /// <para>
    /// Two labels the reference's own <c>if</c>/<c>else if</c> chain can assign -
    /// <c>"IPER"</c> and <c>"IPEV"</c> - have no matching Revit family or lookup case
    /// anywhere in the reference (its own <c>switch</c> only handles IPE/IPEA/IPEO), so
    /// they always fall through to <c>NotFound</c> there too. Reproduced here as null,
    /// not invented family names.
    /// </para>
    /// </summary>
    public static string? Classify(string sectionName, string library)
    {
        if (sectionName.Contains("HE"))
        {
            if (sectionName.Contains("M")) return "HEM";
            if (sectionName.Contains("B")) return "HEB";
            if (sectionName.Contains("A"))
                return sectionName.Count(c => c == 'A') == 2 ? "HEAA" : "HEA";
            return null;
        }

        if (sectionName.Contains("IP"))
        {
            if (sectionName.Contains("O")) return "IPEO";
            if (sectionName.Contains("A")) return "IPEA";
            if (sectionName.Contains("R")) return null; // "IPER" - dead label, see remarks above
            if (sectionName.Contains("V")) return null; // "IPEV" - dead label, see remarks above
            if (sectionName.Contains("E")) return "IPE";
            return null;
        }

        // Library checks are case-normalized on both sides here. The reference mixes
        // ToUpper()-normalized checks (AISC/AUSTRALIA) with un-normalized ones
        // (Contains("American")/("SECTIONS8")/("Canadian")) against a library value that,
        // in practice, is a lowercase file path (confirmed against a live model) - meaning
        // those three sub-checks are effectively dead in the reference. Normalizing all of
        // them is a strict superset of matches (never removes a match that worked before),
        // so it's applied uniformly here rather than reproducing what looks like an
        // unintentional inconsistency.
        string libraryUpper = library.ToUpperInvariant();

        if ((sectionName.Contains("UKC") || sectionName.Contains("UC")) &&
            (libraryUpper.Contains("BSSHAPES") || libraryUpper.Contains("BRITISH") || libraryUpper.Contains("AUSTRAILIAN")))
            return "UC";

        if ((sectionName.Contains("UKB") || sectionName.Contains("UB")) &&
            (libraryUpper.Contains("BSSHAPES") || libraryUpper.Contains("BRITISH") || libraryUpper.Contains("AUSTRAILIAN")))
            return "UB";

        if (sectionName.Contains("W") &&
            (libraryUpper.Contains("AISC") || libraryUpper.Contains("AMERICAN") || libraryUpper.Contains("SECTIONS8") || libraryUpper.Contains("CANADIAN")))
            return "W";

        return null;
    }
}

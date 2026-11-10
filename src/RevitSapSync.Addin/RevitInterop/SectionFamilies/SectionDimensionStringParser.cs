using System.Globalization;

namespace RevitSapSync.RevitInterop.SectionFamilies;

/// <summary>
/// Re-parses the normalized dimension strings <c>SapSectionNameNormalizer</c> builds
/// (e.g. <c>"TUBE 200X200X16"</c>, <c>"2FL. PL. 170X15 +WEB PL. 270X6"</c>), mirroring the
/// reference implementation's <c>Frame.RevitFamily.getXxxDimensionsFromAnalysisSectionNames</c>
/// methods. Shared by both beam and column provisioning since the reference's parsers are
/// shape-generic, not beam/column-specific.
/// <para>
/// One deliberate fix versus the reference: digit extraction here is decimal-aware
/// (<c>IsDigit(c) || c == '.'</c>). The reference's <c>TakeWhile(char.IsDigit)</c> silently
/// truncates any fractional dimension (e.g. "170.5" &#8594; "170") - harmless for the
/// AECOM library's whole-mm built-up sections, but a latent bug for any non-integer one.
/// Since this parser only ever reads back a string this project itself produced, fixing it
/// costs nothing and avoids reintroducing the same class of bug the pipe-dimension
/// rounding fix already addressed.
/// </para>
/// </summary>
internal static class SectionDimensionStringParser
{
    public static string DimensionSuffix(string sec)
    {
        int index = FirstDigitIndex(sec);
        return sec.Substring(index);
    }

    public static string[] SplitDimensions(string sec) => DimensionSuffix(sec).Split('X');

    public static int CountX(string sec) => sec.Count(c => c == 'X');

    /// <summary>
    /// Parses <c>"2FL. PL. {flgWidth}X{flgThick} +WEB PL. {webPlateHeight}X{webThick}"</c>,
    /// where <c>webPlateHeight</c> (what <see cref="SapSectionNameNormalizer"/> embedded
    /// in the string) equals <c>totalDepth - 2*flgThick</c>. This reconstructs the overall
    /// <c>TotalDepth</c> the Revit family's depth parameter ("H") actually expects by
    /// adding <c>2*flgThick</c> back - mirrors the reference's identical
    /// round-trip in <c>getBUS_IDimensionsFromAnalysisSectionNames</c>.
    /// </summary>
    public static (double WebThick, double FlangeThick, double TotalDepth, double FlangeWidth) ParseBuiltUpI(string sec)
    {
        int separatorIndex = sec.LastIndexOf("+WEB", StringComparison.Ordinal);
        string webString = sec.Substring(separatorIndex + 4);
        string flangeString = sec.Substring(0, separatorIndex - 1);

        var webParts = webString.Split('X');
        double webThick = ParseDouble(webParts[1]);

        var flangeParts = flangeString.Split('X');
        double flangeThick = ParseDouble(flangeParts[1]);

        double webPlateHeight = ParseLeadingNumber(webParts[0]);
        double totalDepth = webPlateHeight + 2 * flangeThick;

        double flangeWidth = ParseLeadingNumber(flangeParts[0].Substring(2));

        return (webThick, flangeThick, totalDepth, flangeWidth);
    }

    /// <summary>
    /// Parses <c>"2FL. PL. {width}X{flgThick} +WEB PL. {largeWebDepth}/{smallWebDepth}X{webThick}"</c>.
    /// </summary>
    public static (double WebThick, double FlangeThick, double LargeWebDepth, double SmallWebDepth, double FlangeWidth) ParseTaperedI(string sec)
    {
        int separatorIndex = sec.LastIndexOf("+WEB", StringComparison.Ordinal);
        string webString = sec.Substring(separatorIndex + 4);
        string flangeString = sec.Substring(0, separatorIndex - 1);

        var webParts = webString.Split('X');
        double webThick = ParseDouble(webParts[1]);

        var flangeParts = flangeString.Split('X');
        double flangeThick = ParseDouble(flangeParts[1]);
        double flangeWidth = ParseLeadingNumber(flangeParts[0].Substring(2));

        var webDepthParts = webParts[0].Split('/');
        double largeWebDepth = ParseLeadingNumber(webDepthParts[0]);
        double smallWebDepth = ParseLeadingNumber(webDepthParts[1]);

        return (webThick, flangeThick, largeWebDepth, smallWebDepth, flangeWidth);
    }

    /// <summary>All digit characters in the string, concatenated (not a contiguous run) - e.g. "HEA200" &#8594; "200", "UKB305X102X25" &#8594; "30510225". Used to key catalog I-section lookups by their numeric designation.</summary>
    public static string AllDigits(string sec) => new string(sec.Where(char.IsDigit).ToArray());

    /// <summary>
    /// Parses <c>"TUBE {depth}X{width}X{thick}"</c> or its tapered forms (either/both of
    /// depth/width as <c>"{a}/{b}"</c>, thickness as one value or <c>"{flg}X{web}"</c> when
    /// they differ). Returns the two depth-token values, two width-token values, and
    /// web/flange thickness positionally - which physical end (start vs. end point) each
    /// depth/width value belongs to is not encoded in the string (see
    /// <see cref="SapSectionNameNormalizer"/>'s "large/small" comment) and must be resolved
    /// by the caller from real geometry, exactly as the reference's Z-elevation swap does
    /// for columns.
    /// </summary>
    public static (double FirstDepth, double SecondDepth, double FirstWidth, double SecondWidth, double FlangeThick, double WebThick) ParseTaperedBox(string sec)
    {
        var tokens = SplitDimensions(sec);

        var (firstDepth, secondDepth) = ParseTaperedToken(tokens[0]);
        var (firstWidth, secondWidth) = ParseTaperedToken(tokens[1]);

        double flangeThick, webThick;
        if (tokens.Length == 3)
        {
            flangeThick = webThick = ParseDouble(tokens[2]);
        }
        else
        {
            flangeThick = ParseDouble(tokens[2]);
            webThick = ParseDouble(tokens[3]);
        }

        return (firstDepth, secondDepth, firstWidth, secondWidth, flangeThick, webThick);
    }

    /// <summary>Parses <c>"PIPE {diameter}X{thick}"</c> or the tapered <c>"PIPE {large}/{small}X{thick}"</c> form. Only one thickness value is ever embedded - the reference only ever captures the start segment's thickness for tapered pipes.</summary>
    public static (double FirstDiameter, double SecondDiameter, double Thickness) ParseTaperedPipe(string sec)
    {
        var tokens = SplitDimensions(sec);
        var (firstDiameter, secondDiameter) = ParseTaperedToken(tokens[0]);
        double thickness = ParseDouble(tokens[1]);
        return (firstDiameter, secondDiameter, thickness);
    }

    private static (double First, double Second) ParseTaperedToken(string token)
    {
        if (!token.Contains('/'))
        {
            double value = ParseDouble(token);
            return (value, value);
        }

        var parts = token.Split('/');
        return (ParseDouble(parts[0]), ParseDouble(parts[1]));
    }

    private static int FirstDigitIndex(string sec)
    {
        for (int i = 0; i < sec.Length; i++)
        {
            if (char.IsDigit(sec[i]))
                return i;
        }

        return 0;
    }

    private static double ParseLeadingNumber(string value)
    {
        var digits = value.SkipWhile(c => !char.IsDigit(c)).TakeWhile(c => char.IsDigit(c) || c == '.').ToArray();
        return ParseDouble(new string(digits));
    }

    public static double ParseDouble(string value) => double.Parse(value, CultureInfo.InvariantCulture);
}

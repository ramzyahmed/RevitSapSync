using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;

namespace RevitSapSync.Core.Services;

/// <summary>
/// Pure, unit-testable reimplementation of the reference project's two-phase mapping
/// algorithm (ID match, then geometry-proximity fallback), fixing two known defects
/// found in that reference:
/// <list type="bullet">
/// <item>the duplicate-ID-tolerant variant computed geometry-fallback candidates but
/// never assigned them - a dead code path that silently dropped good matches;</item>
/// <item>duplicate SAP IDs across multiple Revit elements were dropped with no record -
/// here they're reported via <see cref="MappingResult.DuplicateRevitSapIds"/>.</item>
/// </list>
/// Genuinely ambiguous geometry candidates are surfaced via
/// <see cref="AmbiguousMappingCandidate"/> rather than guessed.
/// </summary>
public sealed class FrameMappingEngine : IFrameMappingEngine
{
    public MappingResult Map(List<Frame> sapFrames, List<Frame> revitFrames, MappingOptions options)
    {
        if (sapFrames is null) throw new ArgumentNullException(nameof(sapFrames));
        if (revitFrames is null) throw new ArgumentNullException(nameof(revitFrames));
        if (options is null) throw new ArgumentNullException(nameof(options));

        var mappedPairs = new List<SapRevitFrame>();
        var duplicateSapIds = new List<string>();
        var categoryMismatchWarnings = new List<CategoryMismatchWarning>();

        var (revitById, unmatchedRevit) = BuildRevitIdLookup(revitFrames, duplicateSapIds);
        var unmatchedSap = new List<Frame>();

        foreach (var sapFrame in sapFrames)
        {
            if (!string.IsNullOrEmpty(sapFrame.Id)
                && revitById.TryGetValue(sapFrame.Id, out var revitFrame)
                && TryReconcileMemberType(sapFrame, revitFrame, options, categoryMismatchWarnings))
            {
                mappedPairs.Add(new SapRevitFrame { SapFrame = sapFrame, RevitFrame = revitFrame, MappedBy = MappingMethod.Id });
                unmatchedRevit.Remove(revitFrame);
            }
            else
            {
                unmatchedSap.Add(sapFrame);
            }
        }

        var ambiguous = new List<AmbiguousMappingCandidate>();
        var stillUnmatchedSap = new List<Frame>();

        foreach (var sapFrame in unmatchedSap)
        {
            var match = TryFindGeometryMatch(sapFrame, unmatchedRevit, options, out var ambiguousCandidate);
            if (match is not null)
            {
                mappedPairs.Add(new SapRevitFrame { SapFrame = sapFrame, RevitFrame = match, MappedBy = MappingMethod.Geometry });
                unmatchedRevit.Remove(match);
            }
            else
            {
                stillUnmatchedSap.Add(sapFrame);
                if (ambiguousCandidate is not null)
                    ambiguous.Add(ambiguousCandidate);
            }
        }

        return new MappingResult
        {
            MappedPairs = mappedPairs,
            UnmappedSapFrames = stillUnmatchedSap,
            UnmappedRevitFrames = unmatchedRevit,
            AmbiguousCandidates = ambiguous,
            DuplicateRevitSapIds = duplicateSapIds,
            CategoryMismatchWarnings = categoryMismatchWarnings,
        };
    }

    private static (Dictionary<string, Frame> RevitById, List<Frame> UnmatchedRevit) BuildRevitIdLookup(
        List<Frame> revitFrames, List<string> duplicateSapIds)
    {
        var revitById = new Dictionary<string, Frame>();
        foreach (var group in revitFrames.Where(f => !string.IsNullOrEmpty(f.Id)).GroupBy(f => f.Id))
        {
            revitById[group.Key] = group.First();
            if (group.Count() > 1)
                duplicateSapIds.Add(group.Key);
        }

        return (revitById, new List<Frame>(revitFrames));
    }

    /// <summary>
    /// Checks whether a SAP/Revit pair are compatible member types, reconciling the
    /// Column&#8596;SlantedColumn cross-match by adjusting the SAP frame's Beta the same
    /// way the reference implementation's <c>IsSameMemberType</c> does. SAP-side bracing
    /// classifications (which have no dedicated Revit family per the project brief) are
    /// treated as beam-compatible - a deliberate generalization of the reference's
    /// narrower Beam/VLBracing bucket to also cover HorizontalBracing.
    /// <para>
    /// A genuine Beam&#8596;Column category mismatch (SAP says one, the matched Revit
    /// element actually is the other) is resolved by a three-way priority cascade:
    /// <see cref="MappingOptions.AllowCategoryChangeForRecreation"/> (pass through
    /// unreconciled so geometry sync can recreate the element in the right category) takes
    /// priority over <see cref="MappingOptions.ReclassifyMemberTypeOnCategoryMismatch"/>
    /// (adopt the Revit element's real category, no model change) - if neither is on, the
    /// pair is rejected same as ever, but the mismatch is recorded in
    /// <paramref name="categoryMismatchWarnings"/> instead of vanishing silently into the
    /// generic unmapped-frames lists.
    /// </para>
    /// </summary>
    private static bool TryReconcileMemberType(Frame sapFrame, Frame revitFrame, MappingOptions options, List<CategoryMismatchWarning> categoryMismatchWarnings)
    {
        if (sapFrame.MemberType == revitFrame.MemberType)
            return true;

        bool isColumnSlantedCrossMatch =
            (sapFrame.MemberType == MemberType.SlantedColumn && revitFrame.MemberType == MemberType.Column) ||
            (sapFrame.MemberType == MemberType.Column && revitFrame.MemberType == MemberType.SlantedColumn);
        if (isColumnSlantedCrossMatch)
        {
            sapFrame.Beta = 90.0 - sapFrame.Beta;
            return true;
        }

        bool isBeamLike = sapFrame.MemberType is MemberType.Beam or MemberType.HorizontalBracing or MemberType.VerticalBracing;
        if (isBeamLike && revitFrame.MemberType == MemberType.Beam)
            return true;

        if (options.AllowCategoryChangeForRecreation)
            return true;

        if (options.ReclassifyMemberTypeOnCategoryMismatch)
        {
            sapFrame.MemberType = revitFrame.MemberType;
            return true;
        }

        categoryMismatchWarnings.Add(new CategoryMismatchWarning
        {
            SapId = sapFrame.Id,
            SapMemberType = sapFrame.MemberType,
            RevitMemberType = revitFrame.MemberType,
        });
        return false;
    }

    private Frame? TryFindGeometryMatch(Frame sapFrame, List<Frame> candidatePool, MappingOptions options, out AmbiguousMappingCandidate? ambiguousCandidate)
    {
        ambiguousCandidate = null;

        double maxDistance = options.LengthTolerancePercent * sapFrame.Length;
        var candidates = candidatePool
            .Where(revit => LengthWithinTolerance(sapFrame, revit, options.LengthTolerancePercent))
            .Select(revit => (Frame: revit, Distance: MidpointDistance(sapFrame, revit)))
            .Where(c => c.Distance <= maxDistance)
            .Where(c => IsAxisAligned(sapFrame, c.Frame, options.AxisAlignmentToleranceDegrees))
            .OrderBy(c => c.Distance)
            .ToList();

        if (candidates.Count == 0)
            return null;

        double bestDistance = candidates[0].Distance;
        double tieThreshold = bestDistance * (1.0 + options.TieBreakPercentThreshold / 100.0);
        var tied = candidates.Where(c => c.Distance <= tieThreshold).Select(c => c.Frame).ToList();

        if (tied.Count == 1)
            return tied[0];

        var bySecondaryMetric = tied
            .Select(revit => (Frame: revit, Delta: PerAxisEndpointDelta(sapFrame, revit)))
            .OrderBy(c => c.Delta)
            .ToList();

        double bestDelta = bySecondaryMetric[0].Delta;
        var stillTied = bySecondaryMetric.Where(c => c.Delta == bestDelta).Select(c => c.Frame).ToList();

        if (stillTied.Count == 1)
            return stillTied[0];

        ambiguousCandidate = new AmbiguousMappingCandidate
        {
            SapFrame = sapFrame,
            Candidates = stillTied,
            Reason = $"{stillTied.Count} Revit elements are equally plausible geometry matches (within {options.TieBreakPercentThreshold}% of the best distance and endpoint-delta tie-break).",
        };
        return null;
    }

    private static bool LengthWithinTolerance(Frame sapFrame, Frame revitFrame, double lengthTolerancePercent)
    {
        if (sapFrame.Length == 0)
            return revitFrame.Length == 0;

        double percentDifference = Math.Abs(revitFrame.Length - sapFrame.Length) / sapFrame.Length;
        return percentDifference <= lengthTolerancePercent;
    }

    private static double MidpointDistance(Frame sapFrame, Frame revitFrame)
    {
        double midpointDistance = Point3D.Distance(sapFrame.MidPoint, revitFrame.MidPoint);
        if (!sapFrame.IsNonPrismatic)
            return midpointDistance;

        double startDistance = Point3D.Distance(sapFrame.StartPoint, revitFrame.StartPoint);
        return (midpointDistance + startDistance) / 2.0;
    }

    private static bool IsAxisAligned(Frame a, Frame b, double toleranceDegrees)
    {
        var vectorA = (X: a.EndPoint.X - a.StartPoint.X, Y: a.EndPoint.Y - a.StartPoint.Y, Z: a.EndPoint.Z - a.StartPoint.Z);
        var vectorB = (X: b.EndPoint.X - b.StartPoint.X, Y: b.EndPoint.Y - b.StartPoint.Y, Z: b.EndPoint.Z - b.StartPoint.Z);

        double magnitudeA = Math.Sqrt(vectorA.X * vectorA.X + vectorA.Y * vectorA.Y + vectorA.Z * vectorA.Z);
        double magnitudeB = Math.Sqrt(vectorB.X * vectorB.X + vectorB.Y * vectorB.Y + vectorB.Z * vectorB.Z);
        if (magnitudeA == 0 || magnitudeB == 0)
            return false;

        double dot = vectorA.X * vectorB.X + vectorA.Y * vectorB.Y + vectorA.Z * vectorB.Z;
        double cosine = Clamp(dot / (magnitudeA * magnitudeB), -1.0, 1.0);
        double angleDegrees = Math.Acos(cosine) * 180.0 / Math.PI;

        // Collinear regardless of direction: accept near 0 degrees (same direction) or
        // near 180 degrees (opposite direction, e.g. SAP/Revit start-end order differs).
        return angleDegrees <= toleranceDegrees || (180.0 - angleDegrees) <= toleranceDegrees;
    }

    /// <summary>
    /// Sum of per-axis absolute endpoint deltas, tried both in original start/end order
    /// and swapped (in case the two models disagree on which end is "start") - the
    /// smaller of the two is used, since a large single-axis mismatch from an
    /// unrecognized reversal shouldn't count against an otherwise-good candidate.
    /// </summary>
    private static double PerAxisEndpointDelta(Frame sapFrame, Frame revitFrame)
    {
        double direct = AxisDelta(sapFrame.StartPoint, revitFrame.StartPoint) + AxisDelta(sapFrame.EndPoint, revitFrame.EndPoint);
        double swapped = AxisDelta(sapFrame.StartPoint, revitFrame.EndPoint) + AxisDelta(sapFrame.EndPoint, revitFrame.StartPoint);
        return Math.Min(direct, swapped);
    }

    private static double AxisDelta(Point3D a, Point3D b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    private static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}

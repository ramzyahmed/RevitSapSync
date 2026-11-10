using RevitSapSync.Core.Models;
using RevitSapSync.Core.Options;
using RevitSapSync.Core.Services;
using Xunit;

namespace RevitSapSync.Core.Tests;

public class FrameMappingEngineTests
{
    private readonly FrameMappingEngine _engine = new();
    private readonly MappingOptions _options = new();

    private static Frame MakeFrame(string id, Point3D start, Point3D end, MemberType memberType,
        FrameSource source, double beta = 0, bool isNonPrismatic = false) => new()
    {
        Id = id,
        StartPoint = start,
        EndPoint = end,
        Length = Point3D.Distance(start, end),
        MemberType = memberType,
        Source = source,
        Beta = beta,
        IsNonPrismatic = isNonPrismatic,
    };

    [Fact]
    public void Map_MatchesById_WhenIdsEqualAndTypesCompatible()
    {
        var sap = MakeFrame("F1", new Point3D(0, 0, 0), new Point3D(10, 0, 0), MemberType.Beam, FrameSource.Sap);
        var revit = MakeFrame("F1", new Point3D(0, 100, 0), new Point3D(10, 100, 0), MemberType.Beam, FrameSource.Revit);

        var result = _engine.Map([sap], [revit], _options);

        Assert.Single(result.MappedPairs);
        Assert.Equal(MappingMethod.Id, result.MappedPairs[0].MappedBy);
        Assert.Empty(result.UnmappedSapFrames);
        Assert.Empty(result.UnmappedRevitFrames);
    }

    [Fact]
    public void Map_MatchesByGeometry_WhenIdsAreMissing()
    {
        var sap = MakeFrame("", new Point3D(0, 0, 0), new Point3D(10, 0, 0), MemberType.Beam, FrameSource.Sap);
        var revit = MakeFrame("", new Point3D(0, 0, 0), new Point3D(10, 0, 0), MemberType.Beam, FrameSource.Revit);

        var result = _engine.Map([sap], [revit], _options);

        Assert.Single(result.MappedPairs);
        Assert.Equal(MappingMethod.Geometry, result.MappedPairs[0].MappedBy);
    }

    [Fact]
    public void Map_DoesNotMatchByGeometry_WhenBeyondLengthTolerance()
    {
        var sap = MakeFrame("", new Point3D(0, 0, 0), new Point3D(10, 0, 0), MemberType.Beam, FrameSource.Sap);
        var revit = MakeFrame("", new Point3D(0, 0, 0), new Point3D(20, 0, 0), MemberType.Beam, FrameSource.Revit);

        var result = _engine.Map([sap], [revit], _options);

        Assert.Empty(result.MappedPairs);
        Assert.Single(result.UnmappedSapFrames);
        Assert.Single(result.UnmappedRevitFrames);
    }

    [Fact]
    public void Map_ReconcilesBeta_WhenSapSlantedColumnMatchesRevitVerticalColumn()
    {
        var sap = MakeFrame("C1", new Point3D(0, 0, 0), new Point3D(1, 0, 10), MemberType.SlantedColumn, FrameSource.Sap, beta: 30);
        var revit = MakeFrame("C1", new Point3D(0, 0, 0), new Point3D(0, 0, 10), MemberType.Column, FrameSource.Revit);

        var result = _engine.Map([sap], [revit], _options);

        Assert.Single(result.MappedPairs);
        Assert.Equal(60.0, result.MappedPairs[0].SapFrame.Beta, precision: 6);
    }

    [Fact]
    public void Map_ReportsDuplicateSapIds_AndOnlyMapsFirstRevitElement()
    {
        var sap = MakeFrame("D1", new Point3D(0, 0, 0), new Point3D(10, 0, 0), MemberType.Beam, FrameSource.Sap);
        var revitPrimary = MakeFrame("D1", new Point3D(0, 0, 0), new Point3D(10, 0, 0), MemberType.Beam, FrameSource.Revit);
        var revitDuplicate = MakeFrame("D1", new Point3D(0, 50, 0), new Point3D(10, 50, 0), MemberType.Beam, FrameSource.Revit);

        var result = _engine.Map([sap], [revitPrimary, revitDuplicate], _options);

        Assert.Single(result.MappedPairs);
        Assert.Same(revitPrimary, result.MappedPairs[0].RevitFrame);
        Assert.Contains("D1", result.DuplicateRevitSapIds);
        Assert.Contains(revitDuplicate, result.UnmappedRevitFrames);
    }

    [Fact]
    public void Map_FlagsAmbiguous_WhenTwoGeometryCandidatesAreEquallyPlausible()
    {
        var sap = MakeFrame("", new Point3D(0, 0, 0), new Point3D(10, 0, 0), MemberType.Beam, FrameSource.Sap);
        var revitA = MakeFrame("", new Point3D(0, 1, 0), new Point3D(10, 1, 0), MemberType.Beam, FrameSource.Revit);
        var revitB = MakeFrame("", new Point3D(0, -1, 0), new Point3D(10, -1, 0), MemberType.Beam, FrameSource.Revit);

        var result = _engine.Map([sap], [revitA, revitB], _options);

        Assert.Empty(result.MappedPairs);
        Assert.Single(result.UnmappedSapFrames);
        Assert.Single(result.AmbiguousCandidates);
        Assert.Equal(2, result.AmbiguousCandidates[0].Candidates.Count);
    }

    [Fact]
    public void Map_DoesNotMatch_WhenMemberTypesAreIncompatible()
    {
        // Same SAP ID on both sides, but Column vs Beam isn't a reconcilable pair, so
        // the ID phase must decline the match. Geometry is deliberately made a poor fit
        // too (well outside length tolerance), so this also proves the ID-phase rejection
        // doesn't accidentally let the pair through via the geometry fallback phase.
        var sap = MakeFrame("X1", new Point3D(0, 0, 0), new Point3D(10, 0, 0), MemberType.Column, FrameSource.Sap);
        var revit = MakeFrame("X1", new Point3D(1000, 1000, 0), new Point3D(1005, 1000, 0), MemberType.Beam, FrameSource.Revit);

        var result = _engine.Map([sap], [revit], _options);

        Assert.Empty(result.MappedPairs);
        Assert.Single(result.UnmappedSapFrames);
    }

    [Fact]
    public void Map_Reclassifies_WhenSapBeamMatchesRevitColumn_AndOptionEnabled()
    {
        var options = new MappingOptions { ReclassifyMemberTypeOnCategoryMismatch = true };
        var sap = MakeFrame("B1", new Point3D(0, 0, 0), new Point3D(0, 0, 10), MemberType.Beam, FrameSource.Sap);
        var revit = MakeFrame("B1", new Point3D(0, 0, 0), new Point3D(0, 0, 10), MemberType.Column, FrameSource.Revit);

        var result = _engine.Map([sap], [revit], options);

        Assert.Single(result.MappedPairs);
        Assert.Equal(MemberType.Column, result.MappedPairs[0].SapFrame.MemberType);
    }

    [Fact]
    public void Map_DoesNotReclassify_WhenOptionDisabled()
    {
        // Geometry deliberately made a poor fit too (well outside length tolerance), so
        // this proves the ID-phase rejection doesn't get rescued via geometry fallback -
        // same reasoning as Map_DoesNotMatch_WhenMemberTypesAreIncompatible above.
        var sap = MakeFrame("B2", new Point3D(0, 0, 0), new Point3D(0, 0, 10), MemberType.Beam, FrameSource.Sap);
        var revit = MakeFrame("B2", new Point3D(1000, 1000, 0), new Point3D(1000, 1000, 10), MemberType.Column, FrameSource.Revit);

        var result = _engine.Map([sap], [revit], _options);

        Assert.Empty(result.MappedPairs);
        Assert.Single(result.UnmappedSapFrames);
        Assert.Equal(MemberType.Beam, result.UnmappedSapFrames[0].MemberType);
    }

    [Fact]
    public void Map_PassesThroughUnreconciled_WhenAllowCategoryChangeForRecreationEnabled()
    {
        var options = new MappingOptions { AllowCategoryChangeForRecreation = true };
        var sap = MakeFrame("B3", new Point3D(0, 0, 0), new Point3D(0, 0, 10), MemberType.Beam, FrameSource.Sap);
        var revit = MakeFrame("B3", new Point3D(0, 0, 0), new Point3D(0, 0, 10), MemberType.Column, FrameSource.Revit);

        var result = _engine.Map([sap], [revit], options);

        Assert.Single(result.MappedPairs);
        // Unlike reclassify, the SAP frame keeps its OWN classified type - geometry sync
        // needs to see the real mismatch to detect and recreate it.
        Assert.Equal(MemberType.Beam, result.MappedPairs[0].SapFrame.MemberType);
        Assert.Empty(result.CategoryMismatchWarnings);
    }

    [Fact]
    public void Map_RecordsCategoryMismatchWarning_WhenNeitherOptionEnabled()
    {
        // Geometry deliberately made a poor fit too, so the ID-phase rejection doesn't get
        // rescued via geometry fallback - same reasoning as the other rejection tests above.
        var sap = MakeFrame("B4", new Point3D(0, 0, 0), new Point3D(0, 0, 10), MemberType.Beam, FrameSource.Sap);
        var revit = MakeFrame("B4", new Point3D(1000, 1000, 0), new Point3D(1000, 1000, 10), MemberType.Column, FrameSource.Revit);

        var result = _engine.Map([sap], [revit], _options);

        Assert.Empty(result.MappedPairs);
        Assert.Single(result.CategoryMismatchWarnings);
        Assert.Equal("B4", result.CategoryMismatchWarnings[0].SapId);
        Assert.Equal(MemberType.Beam, result.CategoryMismatchWarnings[0].SapMemberType);
        Assert.Equal(MemberType.Column, result.CategoryMismatchWarnings[0].RevitMemberType);
    }
}

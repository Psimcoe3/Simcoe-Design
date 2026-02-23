using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Conduit.Core.Routing;

namespace ElectricalComponentSandbox.Tests.Conduit;

/// <summary>
/// Tests for smart bend deduct calculations and bend optimization.
/// </summary>
public class SmartBendServiceTests
{
    [Fact]
    public void LookupDeduct_ExactAngle_ReturnsEntry()
    {
        var svc = new SmartBendService();
        var entry = svc.LookupDeduct("1/2", 90);
        Assert.NotNull(entry);
        Assert.Equal(5.0, entry!.DeductInches);
    }

    [Fact]
    public void LookupDeduct_45Degree_ReturnsEntry()
    {
        var svc = new SmartBendService();
        var entry = svc.LookupDeduct("1/2", 45);
        Assert.NotNull(entry);
        Assert.Equal(2.5, entry!.DeductInches);
    }

    [Fact]
    public void LookupDeduct_Interpolated_ReturnsReasonableValue()
    {
        var svc = new SmartBendService();
        var entry = svc.LookupDeduct("1/2", 67.5); // Between 45 and 90 degrees
        Assert.NotNull(entry);
        Assert.True(entry!.DeductInches > 2.5);
        Assert.True(entry.DeductInches < 5.0);
    }

    [Fact]
    public void LookupDeduct_UnknownTradeSize_ReturnsNull()
    {
        var svc = new SmartBendService();
        Assert.Null(svc.LookupDeduct("99", 90));
    }

    [Fact]
    public void ComputeCutLength_WithBends_DeductsCorrectly()
    {
        var svc = new SmartBendService();
        // 120 inches raw, 90-degree bend at start, 45-degree at end
        double cut = svc.ComputeCutLength(120, "1/2", 90, 45);
        // Should deduct 5.0 + 2.5 = 7.5
        Assert.Equal(120 - 5.0 - 2.5, cut, 1);
    }

    [Fact]
    public void ComputeCutLength_NoBends_ReturnsRawLength()
    {
        var svc = new SmartBendService();
        double cut = svc.ComputeCutLength(120, "1/2", null, null);
        Assert.Equal(120, cut);
    }

    [Fact]
    public void ComputeCutLength_NeverNegative()
    {
        var svc = new SmartBendService();
        // Very short segment with large deducts
        double cut = svc.ComputeCutLength(2, "1/2", 90, 90);
        Assert.True(cut >= 0);
    }

    [Fact]
    public void MergeColinearSegments_ComputesCorrectly()
    {
        var svc = new SmartBendService();
        var segments = new List<ConduitSegment>
        {
            new() { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(5, 0, 0), TradeSize = "1/2" },
            new() { StartPoint = new XYZ(5, 0, 0), EndPoint = new XYZ(10, 0, 0), TradeSize = "1/2" }
        };

        var stick = svc.MergeColinearSegments(segments, new List<double> { 90 });

        Assert.Equal(2, stick.SegmentCount);
        Assert.Equal(1, stick.BendCount);
        Assert.Equal(120, stick.RawLengthInches, 1); // 10 ft = 120 inches
        Assert.True(stick.CutLengthInches < stick.RawLengthInches);
    }

    [Fact]
    public void ClassifyBend_VerticalStub90()
    {
        var svc = new SmartBendService();
        var dir1 = XYZ.BasisX;
        var dir2 = XYZ.BasisZ;
        var type = svc.ClassifyBend(dir1, dir2, XYZ.BasisZ);
        Assert.Equal(SmartBendType.Stub90, type);
    }

    [Fact]
    public void ClassifyBend_HorizontalKick90()
    {
        var svc = new SmartBendService();
        var dir1 = XYZ.BasisX;
        var dir2 = XYZ.BasisY;
        var type = svc.ClassifyBend(dir1, dir2, XYZ.BasisZ);
        Assert.Equal(SmartBendType.Kick90, type);
    }

    [Fact]
    public void ClassifyBend_SmallAngle_ReturnsOffset()
    {
        var svc = new SmartBendService();
        var dir1 = XYZ.BasisX;
        var dir2 = new XYZ(1, 0.2, 0).Normalize();
        var type = svc.ClassifyBend(dir1, dir2, XYZ.BasisZ);
        Assert.Equal(SmartBendType.Offset, type);
    }

    [Fact]
    public void LoadFromCsv_ParsesCorrectly()
    {
        var svc = new SmartBendService();
        var csv = new[]
        {
            "TradeSize,AngleDegrees,BendRadius,DeductInches,TangentLengthInches,GainInches",
            "3/4,90,4.5,6.0,4.5,3.375",
            "3/4,45,4.5,3.0,4.5,1.688"
        };

        svc.LoadFromCsv(csv);
        Assert.Equal(2, svc.BendTable.Count);

        var entry = svc.LookupDeduct("3/4", 90);
        Assert.NotNull(entry);
        Assert.Equal(6.0, entry!.DeductInches);
    }

    [Fact]
    public void CalculateStubMark_SubtractsTakeUp()
    {
        var svc = new SmartBendService();
        var mark = svc.CalculateStubMark(14.0, 6.0);
        Assert.Equal(8.0, mark, 3);
    }

    [Fact]
    public void CalculateOffsetSpacing_UsesMultiplierTable()
    {
        var svc = new SmartBendService();
        var spacing = svc.CalculateOffsetSpacing(6.0, 30.0);
        Assert.Equal(12.0, spacing, 3);
    }

    [Fact]
    public void CalculateOffsetSpacing_FallsBackToTrig()
    {
        var svc = new SmartBendService();
        var spacing = svc.CalculateOffsetSpacing(6.0, 20.0);
        var expected = 6.0 / Math.Sin(20.0 * Math.PI / 180.0);
        Assert.Equal(expected, spacing, 6);
    }

    [Fact]
    public void CalculateOffsetShrink_UsesShrinkTable()
    {
        var svc = new SmartBendService();
        var shrink = svc.CalculateOffsetShrink(6.0, 30.0);
        Assert.Equal(1.5, shrink, 3);
    }

    [Fact]
    public void CalculateOffsetMarksTowardObstruction_ComputesExpectedValues()
    {
        var svc = new SmartBendService();
        var result = svc.CalculateOffsetMarksTowardObstruction(40.0, 6.0, 30.0);

        Assert.Equal(12.0, result.SpacingInches, 3);
        Assert.Equal(1.5, result.ShrinkInches, 3);
        Assert.Equal(41.5, result.FirstMarkInches, 3);
        Assert.Equal(29.5, result.SecondMarkInches, 3);
    }

    [Fact]
    public void ThreePointSaddleHelpers_ReturnExpectedRuleOfThumb()
    {
        Assert.True(SmartBendService.IsSymmetricThreePointSaddle(45.0, 22.5));
        Assert.Equal(45.0, SmartBendService.CalculateThreePointSaddleCenterAngle(22.5), 3);

        var svc = new SmartBendService();
        var marks = svc.CalculateThreePointSaddleMarks45(4.0);
        Assert.Equal(8.0, marks.OuterMarkOffsetInches, 3);
        Assert.Equal(16.0, marks.TotalOutsideSpacingInches, 3);
    }
}

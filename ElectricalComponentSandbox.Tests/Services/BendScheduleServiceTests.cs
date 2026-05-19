using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class BendScheduleServiceTests
{
    private static ConduitModelStore CreateStore()
    {
        var store = new ConduitModelStore();
        store.AddType(new ConduitType { Id = "emt", Name = "EMT", Standard = ConduitMaterialType.EMT });
        store.Settings.DefaultConduitTypeId = "emt";
        return store;
    }

    private static ConduitSegment Seg(double x1, double x2, string size = "3/4")
        => new()
        {
            StartPoint = new XYZ(x1, 0, 0),
            EndPoint = new XYZ(x2, 0, 0),
            TradeSize = size,
        };

    private static ConduitRun AddRun(
        ConduitModelStore store,
        IEnumerable<ConduitSegment> segments,
        IEnumerable<ConduitFitting>? fittings = null,
        string size = "3/4",
        ConduitMaterialType material = ConduitMaterialType.EMT)
    {
        var run = new ConduitRun
        {
            RunId = store.GenerateRunId(),
            TradeSize = size,
            Material = material,
        };
        foreach (var s in segments)
        {
            store.AddSegment(s);
            run.SegmentIds.Add(s.Id);
        }
        foreach (var f in fittings ?? Enumerable.Empty<ConduitFitting>())
        {
            store.AddFitting(f);
            run.FittingIds.Add(f.Id);
        }
        store.AddRun(run);
        return run;
    }

    [Fact]
    public void ComputeForRun_NoBends_ReturnsStraightPattern()
    {
        var store = CreateStore();
        var run = AddRun(store, new[] { Seg(0, 20) });

        var schedule = BendScheduleService.ComputeForRun(store, run.Id);

        Assert.Equal(BendSchedulePattern.Straight, schedule.OverallPattern);
        Assert.Empty(schedule.Bends);
        Assert.Equal("3/4", schedule.TradeSize);
    }

    [Fact]
    public void ComputeForRun_SingleStub90_MarksPositionedAtCumulativeLengthMinusDeduct()
    {
        var store = CreateStore();
        var seg = Seg(0, 5); // 5 ft = 60 in
        var elbow = new ConduitFitting { Type = FittingType.Elbow90, AngleDegrees = 90, TradeSize = "3/4" };
        var run = AddRun(store, new[] { seg }, new[] { elbow });

        var schedule = BendScheduleService.ComputeForRun(store, run.Id);

        Assert.Single(schedule.Bends);
        var bend = schedule.Bends[0];
        Assert.Equal(BendSchedulePattern.Stub90, bend.Pattern);
        Assert.Equal(90, bend.Angle1Degrees);
        Assert.Equal(6.0, bend.DeductInches, 3); // 3/4" EMT 90° = 6"
        Assert.Equal(54.0, bend.Mark1Inches, 3); // 60 - 6 takeup
        Assert.Equal(60.0, bend.DimAInches, 3); // stub leg = segment length
    }

    [Fact]
    public void ComputeForRun_TwoConsecutiveOffsets_GroupsIntoSingleOffsetRow()
    {
        var store = CreateStore();
        var s1 = Seg(0, 2); // 24 in
        var s2 = Seg(2, 4); // 24 in
        var s3 = Seg(4, 6); // 24 in
        var off1 = new ConduitFitting { Type = FittingType.Offset, AngleDegrees = 4.0, TradeSize = "3/4" };
        var off2 = new ConduitFitting { Type = FittingType.Offset, AngleDegrees = 4.0, TradeSize = "3/4" };
        var run = AddRun(store, new[] { s1, s2, s3 }, new[] { off1, off2 });

        var schedule = BendScheduleService.ComputeForRun(store, run.Id);

        Assert.Single(schedule.Bends);
        Assert.Equal(BendSchedulePattern.Offset, schedule.Bends[0].Pattern);
        Assert.True(schedule.Bends[0].Mark1Inches > 0);
        Assert.True(schedule.Bends[0].Mark2Inches > schedule.Bends[0].Mark1Inches);
        Assert.True(schedule.Bends[0].DimBInches > 0); // spacing between marks
    }

    [Fact]
    public void ComputeForRun_ThreeConsecutiveOffsets_GroupsIntoSaddle3Point()
    {
        var store = CreateStore();
        var s1 = Seg(0, 2);
        var s2 = Seg(2, 4);
        var s3 = Seg(4, 6);
        var s4 = Seg(6, 8);
        var off1 = new ConduitFitting { Type = FittingType.Offset, AngleDegrees = 4.0, TradeSize = "3/4" };
        var off2 = new ConduitFitting { Type = FittingType.Offset, AngleDegrees = 4.0, TradeSize = "3/4" };
        var off3 = new ConduitFitting { Type = FittingType.Offset, AngleDegrees = 4.0, TradeSize = "3/4" };
        var run = AddRun(store, new[] { s1, s2, s3, s4 }, new[] { off1, off2, off3 });

        var schedule = BendScheduleService.ComputeForRun(store, run.Id);

        Assert.Single(schedule.Bends);
        Assert.Equal(BendSchedulePattern.Saddle3Point, schedule.Bends[0].Pattern);
        Assert.True(schedule.Bends[0].Mark3Inches > schedule.Bends[0].Mark2Inches);
        Assert.Contains("3-point saddle", schedule.Bends[0].Notes);
    }

    [Fact]
    public void ComputeForRun_FourOffsets_GroupsIntoSaddle4Point()
    {
        var store = CreateStore();
        var segs = Enumerable.Range(0, 5).Select(i => Seg(i, i + 1)).ToList();
        var offs = Enumerable.Range(0, 4)
            .Select(_ => new ConduitFitting { Type = FittingType.Offset, AngleDegrees = 4.0, TradeSize = "3/4" })
            .ToList();
        var run = AddRun(store, segs, offs);

        var schedule = BendScheduleService.ComputeForRun(store, run.Id);

        Assert.Single(schedule.Bends);
        Assert.Equal(BendSchedulePattern.Saddle4Point, schedule.Bends[0].Pattern);
        Assert.True(schedule.Bends[0].Mark4Inches > schedule.Bends[0].Mark3Inches);
    }

    [Fact]
    public void ComputeForRun_StoredDeductOverridesTable()
    {
        var store = CreateStore();
        var seg = Seg(0, 5);
        var elbow = new ConduitFitting
        {
            Type = FittingType.Elbow90,
            AngleDegrees = 90,
            TradeSize = "3/4",
            DeductLength = 7.25,
        };
        var run = AddRun(store, new[] { seg }, new[] { elbow });

        var schedule = BendScheduleService.ComputeForRun(store, run.Id);

        Assert.Single(schedule.Bends);
        Assert.Equal(7.25, schedule.Bends[0].DeductInches, 3);
        Assert.Equal(60.0 - 7.25, schedule.Bends[0].Mark1Inches, 3);
    }

    [Fact]
    public void ComputeForRun_BenderTypePicksByTradeSize()
    {
        var store = CreateStore();
        var seg = Seg(0, 5, "2");
        var elbow = new ConduitFitting { Type = FittingType.Elbow90, AngleDegrees = 90, TradeSize = "2" };
        var run = AddRun(store, new[] { seg }, new[] { elbow }, size: "2");

        var schedule = BendScheduleService.ComputeForRun(store, run.Id);

        Assert.Contains("hydraulic", schedule.Bends[0].BenderType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComputeForRun_NullStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BendScheduleService.ComputeForRun(null!, "anything"));
    }

    [Fact]
    public void ComputeForRun_UnknownRun_Throws()
    {
        var store = CreateStore();
        Assert.Throws<ArgumentException>(() =>
            BendScheduleService.ComputeForRun(store, "no-such-run"));
    }
}

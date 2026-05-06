using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ConduitTakeoffServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static ConduitModelStore CreateStore()
    {
        var store = new ConduitModelStore();
        var emt = new ConduitType
        {
            Id = "emt",
            Name = "EMT",
            Standard = ConduitMaterialType.EMT,
            IsWithFitting = true,
        };
        store.AddType(emt);
        store.Settings.DefaultConduitTypeId = emt.Id;
        return store;
    }

    private static ConduitSegment MakeSeg(double x1, double x2)
    {
        return new ConduitSegment
        {
            StartPoint = new XYZ(x1, 0, 0),
            EndPoint   = new XYZ(x2, 0, 0),
            TradeSize  = "3/4",
        };
    }

    private static ConduitRun MakeRun(
        ConduitModelStore store,
        IEnumerable<ConduitSegment> segments,
        IEnumerable<ConduitFitting>? fittings = null,
        ConduitMaterialType material = ConduitMaterialType.EMT,
        string tradeSize = "3/4")
    {
        var run = new ConduitRun
        {
            RunId      = store.GenerateRunId(),
            Material   = material,
            TradeSize  = tradeSize,
        };

        foreach (var seg in segments)
        {
            store.AddSegment(seg);
            run.SegmentIds.Add(seg.Id);
        }

        foreach (var fit in fittings ?? Enumerable.Empty<ConduitFitting>())
        {
            store.AddFitting(fit);
            run.FittingIds.Add(fit.Id);
        }

        store.AddRun(run);
        return run;
    }

    // ── GetDeduct90 ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("1/2",   ConduitMaterialType.EMT, 5.0)]
    [InlineData("3/4",   ConduitMaterialType.EMT, 6.0)]
    [InlineData("1",     ConduitMaterialType.EMT, 8.0)]
    [InlineData("1-1/4", ConduitMaterialType.EMT, 11.0)]
    [InlineData("2",     ConduitMaterialType.EMT, 16.0)]
    [InlineData("1/2",   ConduitMaterialType.RMC, 6.0)]
    [InlineData("3/4",   ConduitMaterialType.RMC, 8.0)]
    [InlineData("1",     ConduitMaterialType.RMC, 11.0)]
    [InlineData("2",     ConduitMaterialType.RMC, 21.0)]
    public void GetDeduct90_ReturnsExpectedInches(
        string tradeSize, ConduitMaterialType material, double expectedInches)
    {
        var result = ConduitTakeoffService.GetDeduct90(tradeSize, material);
        Assert.Equal(expectedInches, result, precision: 3);
    }

    [Fact]
    public void GetDeduct90_UnknownTradeSize_ReturnsZero()
    {
        var result = ConduitTakeoffService.GetDeduct90("99", ConduitMaterialType.EMT);
        Assert.Equal(0, result);
    }

    // ── GetDeduct45 ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("1/2",   ConduitMaterialType.EMT, 2.5)]
    [InlineData("3/4",   ConduitMaterialType.EMT, 3.0)]
    [InlineData("1",     ConduitMaterialType.EMT, 4.0)]
    [InlineData("2",     ConduitMaterialType.EMT, 8.0)]
    [InlineData("3/4",   ConduitMaterialType.RMC, 4.0)]
    [InlineData("1",     ConduitMaterialType.RMC, 5.5)]
    public void GetDeduct45_ReturnsExpectedInches(
        string tradeSize, ConduitMaterialType material, double expectedInches)
    {
        var result = ConduitTakeoffService.GetDeduct45(tradeSize, material);
        Assert.Equal(expectedInches, result, precision: 3);
    }

    // ── PVC falls back to EMT values ──────────────────────────────────────

    [Fact]
    public void GetDeduct90_PvcMaterial_FallsBackToEmtTable()
    {
        var emtValue = ConduitTakeoffService.GetDeduct90("3/4", ConduitMaterialType.EMT);
        var pvcValue = ConduitTakeoffService.GetDeduct90("3/4", ConduitMaterialType.PVC);
        Assert.Equal(emtValue, pvcValue);
    }

    // ── ComputeOffsetDeduct ──────────────────────────────────────────────

    [Theory]
    [InlineData(6.0, 45.0, 2.49)] // 6 × 0.41421 ≈ 2.4853
    [InlineData(6.0, 30.0, 1.61)] // 6 × 0.26795 ≈ 1.6077
    [InlineData(0.0, 45.0, 0.0)]  // zero offset
    public void ComputeOffsetDeduct_ReturnsExpected(
        double offsetIn, double angle, double expectedDeduct)
    {
        var result = ConduitTakeoffService.ComputeOffsetDeduct(offsetIn, angle);
        Assert.Equal(expectedDeduct, result, precision: 1);
    }

    [Fact]
    public void ComputeOffsetDeduct_NegativeOffset_ReturnsZero()
    {
        var result = ConduitTakeoffService.ComputeOffsetDeduct(-3.0, 45.0);
        Assert.Equal(0, result);
    }

    // ── GetSupportSpacing ────────────────────────────────────────────────

    [Theory]
    [InlineData("1/2",   ConduitMaterialType.EMT, 10.0)]
    [InlineData("2",     ConduitMaterialType.EMT, 10.0)]
    [InlineData("3/4",   ConduitMaterialType.RMC, 10.0)]
    [InlineData("1/2",   ConduitMaterialType.PVC, 3.0)]
    [InlineData("1",     ConduitMaterialType.PVC, 3.0)]
    [InlineData("1-1/4", ConduitMaterialType.PVC, 5.0)]
    [InlineData("2-1/2", ConduitMaterialType.PVC, 6.0)]
    [InlineData("3-1/2", ConduitMaterialType.PVC, 8.0)]
    public void GetSupportSpacing_ReturnsNecValue(
        string tradeSize, ConduitMaterialType material, double expectedFeet)
    {
        var result = ConduitTakeoffService.GetSupportSpacing(tradeSize, material);
        Assert.Equal(expectedFeet, result);
    }

    // ── ComputeRunTakeoff ────────────────────────────────────────────────

    [Fact]
    public void ComputeRunTakeoff_NoFittings_AdjustedEqualsGross()
    {
        var store = CreateStore();
        var run = MakeRun(store, [MakeSeg(0, 20)]);   // 20 ft segment

        var result = ConduitTakeoffService.ComputeRunTakeoff(store, run.Id);

        Assert.Equal(20.0, result.GrossLengthFeet, precision: 6);
        Assert.Equal(20.0, result.AdjustedLengthFeet, precision: 6);
        Assert.Equal(0,    result.TotalDeductInches);
        Assert.Empty(result.Fittings);
    }

    [Fact]
    public void ComputeRunTakeoff_OneElbow90_DeductsCorrectly()
    {
        var store = CreateStore();
        var fitting = new ConduitFitting
        {
            Type         = FittingType.Elbow90,
            AngleDegrees = 90,
            TradeSize    = "3/4",
        };
        var run = MakeRun(
            store,
            [MakeSeg(0, 20)],
            [fitting]);

        var result = ConduitTakeoffService.ComputeRunTakeoff(store, run.Id);

        // 3/4" EMT 90° deduct = 6 inches
        Assert.Equal(6.0, result.TotalDeductInches, precision: 3);
        Assert.Equal(20.0 - 6.0 / 12.0, result.AdjustedLengthFeet, precision: 6);
        Assert.Single(result.Fittings);
        Assert.Equal(TakeoffFittingCategory.Elbow90, result.Fittings[0].Category);
    }

    [Fact]
    public void ComputeRunTakeoff_OneElbow45_DeductsCorrectly()
    {
        var store = CreateStore();
        var fitting = new ConduitFitting
        {
            Type         = FittingType.Elbow45,
            AngleDegrees = 45,
            TradeSize    = "1",
        };
        var run = MakeRun(
            store,
            [MakeSeg(0, 10)],
            [fitting],
            tradeSize: "1");

        var result = ConduitTakeoffService.ComputeRunTakeoff(store, run.Id);

        // 1" EMT 45° deduct = 4 inches
        Assert.Equal(4.0, result.TotalDeductInches, precision: 3);
    }

    [Fact]
    public void ComputeRunTakeoff_StoredDeductOverridesTable()
    {
        var store = CreateStore();
        var fitting = new ConduitFitting
        {
            Type         = FittingType.Elbow90,
            AngleDegrees = 90,
            TradeSize    = "3/4",
            DeductLength = 7.5,   // explicit override
        };
        var run = MakeRun(store, [MakeSeg(0, 20)], [fitting]);

        var result = ConduitTakeoffService.ComputeRunTakeoff(store, run.Id);

        Assert.Equal(7.5, result.TotalDeductInches, precision: 3);
    }

    [Fact]
    public void ComputeRunTakeoff_MultipleFittings_SumsDeducts()
    {
        var store = CreateStore();
        var e90 = new ConduitFitting
            { Type = FittingType.Elbow90, AngleDegrees = 90, TradeSize = "3/4" };
        var e45 = new ConduitFitting
            { Type = FittingType.Elbow45, AngleDegrees = 45, TradeSize = "3/4" };
        var run = MakeRun(
            store,
            [MakeSeg(0, 20), MakeSeg(20, 30)],
            [e90, e45]);

        var result = ConduitTakeoffService.ComputeRunTakeoff(store, run.Id);

        // 3/4 EMT: 90° = 6", 45° = 3"  → total 9"
        Assert.Equal(9.0, result.TotalDeductInches, precision: 3);
        Assert.Equal(2, result.Fittings.Count);
    }

    [Fact]
    public void ComputeRunTakeoff_SupportCount_UsesNecSpacing()
    {
        var store = CreateStore();
        // 50 ft run, EMT 10 ft spacing → ceil(50/10)+1 = 6
        var run = MakeRun(store, [MakeSeg(0, 50)]);

        var result = ConduitTakeoffService.ComputeRunTakeoff(store, run.Id);

        Assert.Equal(10.0, result.SupportSpacingFeet);
        Assert.Equal(6, result.RecommendedSupportCount);
    }

    [Fact]
    public void ComputeRunTakeoff_PvcRun_UsesPvcSupportSpacing()
    {
        var store = CreateStore();
        var run = MakeRun(
            store,
            [MakeSeg(0, 15)],
            material: ConduitMaterialType.PVC,
            tradeSize: "1/2");

        var result = ConduitTakeoffService.ComputeRunTakeoff(store, run.Id);

        Assert.Equal(3.0, result.SupportSpacingFeet);
        // ceil(15/3)+1 = 6
        Assert.Equal(6, result.RecommendedSupportCount);
    }

    [Fact]
    public void ComputeRunTakeoff_UnknownRunId_Throws()
    {
        var store = CreateStore();
        Assert.Throws<ArgumentException>(() =>
            ConduitTakeoffService.ComputeRunTakeoff(store, "no-such-run"));
    }

    [Fact]
    public void ComputeRunTakeoff_NullStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ConduitTakeoffService.ComputeRunTakeoff(null!, "run-id"));
    }

    [Fact]
    public void ComputeRunTakeoff_OffsetFitting_UsesOffsetCategory()
    {
        var store = CreateStore();
        var fitting = new ConduitFitting
        {
            Type         = FittingType.Offset,
            AngleDegrees = 6.0,    // stored as the offset distance for default 45° calc
            TradeSize    = "3/4",
        };
        var run = MakeRun(store, [MakeSeg(0, 20)], [fitting]);

        var result = ConduitTakeoffService.ComputeRunTakeoff(store, run.Id);

        Assert.Single(result.Fittings);
        Assert.Equal(TakeoffFittingCategory.Offset, result.Fittings[0].Category);
    }
}

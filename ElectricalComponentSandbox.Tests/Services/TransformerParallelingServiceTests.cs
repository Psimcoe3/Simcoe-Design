using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class TransformerParallelingServiceTests
{
    private static TransformerParallelingService.TransformerParallelUnit MakeUnit(
        string id,
        double kva = 1000,
        double primary = 12470,
        double secondary = 480,
        double impedance = 5.75,
        double tap = 0,
        int phaseShift = 0,
        bool samePolarity = true) => new()
        {
            Id = id,
            RatedKVA = kva,
            PrimaryVoltage = primary,
            SecondaryVoltage = secondary,
            PercentImpedance = impedance,
            TapPercent = tap,
            PhaseShiftDegrees = phaseShift,
            SamePolarity = samePolarity,
        };

    [Fact]
    public void CalculateVoltageRatio_ReturnsExpectedValue()
    {
        double result = TransformerParallelingService.CalculateVoltageRatio(12470, 480);

        Assert.Equal(25.98, result, 2);
    }

    [Fact]
    public void CalculateRatioMismatchPercent_IdenticalUnits_ReturnsZero()
    {
        double result = TransformerParallelingService.CalculateRatioMismatchPercent(MakeUnit("T1"), MakeUnit("T2"));

        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateRatioMismatchPercent_TapDifference_IncreasesMismatch()
    {
        double result = TransformerParallelingService.CalculateRatioMismatchPercent(MakeUnit("T1"), MakeUnit("T2", tap: 2.5));

        Assert.True(result > 0);
    }

    [Fact]
    public void CalculateImpedanceMismatchPercent_IdenticalUnits_ReturnsZero()
    {
        double result = TransformerParallelingService.CalculateImpedanceMismatchPercent(MakeUnit("T1"), MakeUnit("T2"));

        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateImpedanceMismatchPercent_DifferentImpedance_IncreasesMismatch()
    {
        double result = TransformerParallelingService.CalculateImpedanceMismatchPercent(MakeUnit("T1", impedance: 5), MakeUnit("T2", impedance: 6));

        Assert.True(result > 0);
    }

    [Fact]
    public void EstimateCirculatingCurrentPercent_LargerMismatch_IncreasesCurrent()
    {
        double low = TransformerParallelingService.EstimateCirculatingCurrentPercent(0.2, 5.75);
        double high = TransformerParallelingService.EstimateCirculatingCurrentPercent(0.4, 5.75);

        Assert.True(high > low);
    }

    [Fact]
    public void CalculateLoadShares_EqualUnits_ShareEqually()
    {
        var shares = TransformerParallelingService.CalculateLoadShares(new[] { MakeUnit("T1"), MakeUnit("T2") }, 1200);

        Assert.Equal(600.0, shares.Single(x => x.Id == "T1").AssignedKVA, 1);
        Assert.Equal(600.0, shares.Single(x => x.Id == "T2").AssignedKVA, 1);
    }

    [Fact]
    public void CalculateLoadShares_LowerImpedance_PicksUpMoreLoad()
    {
        var shares = TransformerParallelingService.CalculateLoadShares(
            new[] { MakeUnit("T1", impedance: 5), MakeUnit("T2", impedance: 6) },
            1100);

        Assert.True(shares.Single(x => x.Id == "T1").AssignedKVA > shares.Single(x => x.Id == "T2").AssignedKVA);
    }

    [Fact]
    public void AnalyzeParalleling_MatchingUnits_AllowsParallel()
    {
        var result = TransformerParallelingService.AnalyzeParalleling(MakeUnit("T1"), MakeUnit("T2"), 1200);

        Assert.True(result.CanParallel);
        Assert.Equal(2, result.Shares.Count);
    }

    [Fact]
    public void AnalyzeParalleling_RatioMismatch_BlocksParallel()
    {
        var result = TransformerParallelingService.AnalyzeParalleling(MakeUnit("T1"), MakeUnit("T2", tap: 2.5), 1200);

        Assert.False(result.CanParallel);
        Assert.Contains("ratio", result.Issue, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeParalleling_ImpedanceMismatch_BlocksParallel()
    {
        var result = TransformerParallelingService.AnalyzeParalleling(MakeUnit("T1", impedance: 4), MakeUnit("T2", impedance: 7), 1200);

        Assert.False(result.CanParallel);
        Assert.Contains("impedance", result.Issue, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeParalleling_PhaseShiftMismatch_BlocksParallel()
    {
        var result = TransformerParallelingService.AnalyzeParalleling(MakeUnit("T1", phaseShift: 0), MakeUnit("T2", phaseShift: 30), 1200);

        Assert.False(result.CanParallel);
        Assert.Contains("phase shift", result.Issue, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeParalleling_PolarityMismatch_BlocksParallel()
    {
        var result = TransformerParallelingService.AnalyzeParalleling(MakeUnit("T1", samePolarity: true), MakeUnit("T2", samePolarity: false), 1200);

        Assert.False(result.CanParallel);
        Assert.Contains("polarity", result.Issue, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnalyzeParalleling_HeavyLoad_CanOverloadOneUnit()
    {
        var result = TransformerParallelingService.AnalyzeParalleling(MakeUnit("T1", impedance: 5), MakeUnit("T2", impedance: 5.5), 2100);

        Assert.Contains(result.Shares, share => share.Overloaded);
    }
}
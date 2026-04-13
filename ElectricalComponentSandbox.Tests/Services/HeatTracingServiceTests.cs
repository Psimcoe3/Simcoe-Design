using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class HeatTracingServiceTests
{
    // ── Heat Loss ────────────────────────────────────────────────────────────

    [Fact]
    public void CalculateHeatLoss_BasicFreeze_Positive()
    {
        var result = HeatTracingService.CalculateHeatLoss(2, 100, 40, 0,
            HeatTracingService.PipeInsulation.Fiberglass1Inch);

        Assert.True(result.HeatLossWattPerFt > 0);
        Assert.True(result.TotalHeatLossWatt > 0);
    }

    [Fact]
    public void CalculateHeatLoss_NoInsulation_HigherLoss()
    {
        var insulated = HeatTracingService.CalculateHeatLoss(2, 100, 40, 0,
            HeatTracingService.PipeInsulation.Fiberglass2Inch);
        var bare = HeatTracingService.CalculateHeatLoss(2, 100, 40, 0,
            HeatTracingService.PipeInsulation.None);

        Assert.True(bare.HeatLossWattPerFt > insulated.HeatLossWattPerFt);
    }

    [Fact]
    public void CalculateHeatLoss_HigherDeltaT_MoreLoss()
    {
        var low = HeatTracingService.CalculateHeatLoss(2, 100, 40, 20);
        var high = HeatTracingService.CalculateHeatLoss(2, 100, 150, 20);

        Assert.True(high.TotalHeatLossWatt > low.TotalHeatLossWatt);
    }

    [Fact]
    public void CalculateHeatLoss_TotalScalesWithLength()
    {
        var short1 = HeatTracingService.CalculateHeatLoss(3, 50, 60, 10);
        var long1 = HeatTracingService.CalculateHeatLoss(3, 100, 60, 10);

        // Double the length → approximately double the total (within rounding)
        double ratio = long1.TotalHeatLossWatt / short1.TotalHeatLossWatt;
        Assert.True(ratio > 1.99 && ratio < 2.01);
    }

    // ── Cable Selection ──────────────────────────────────────────────────────

    [Fact]
    public void SelectCable_FreezeProtection_SelfRegulating()
    {
        var result = HeatTracingService.SelectCable(3, 200, 40);

        Assert.Equal(HeatTracingService.HeatTraceType.SelfRegulating, result.CableType);
    }

    [Fact]
    public void SelectCable_HighTemp_MineralInsulated()
    {
        var result = HeatTracingService.SelectCable(10, 100, 600);

        Assert.Equal(HeatTracingService.HeatTraceType.MineralInsulated, result.CableType);
        Assert.True(result.MaxExposureTemperatureF >= 600);
    }

    [Fact]
    public void SelectCable_HighLoss_SpiralRequired()
    {
        // 20 W/ft required but self-reg only supplies 5 W/ft → spiral 4x
        var result = HeatTracingService.SelectCable(20, 100, 40);

        Assert.True(result.CableLengthFt > 100); // Must spiral
    }

    [Fact]
    public void SelectCable_LengthIncludesSafetyFactor()
    {
        // Low loss → no spiraling, cable = pipe length
        var result = HeatTracingService.SelectCable(2, 100, 40, 1.0);

        Assert.Equal(100, result.CableLengthFt);
    }

    [Fact]
    public void SelectCable_ProcessTemp_PowerLimiting()
    {
        var result = HeatTracingService.SelectCable(10, 100, 400);

        Assert.Equal(HeatTracingService.HeatTraceType.PowerLimiting, result.CableType);
    }

    // ── Circuit Sizing ───────────────────────────────────────────────────────

    [Fact]
    public void SizeCircuit_ContinuousLoad_125Percent()
    {
        // 1000W at 240V = 4.17A → 125% = 5.21A → 15A breaker
        var result = HeatTracingService.SizeCircuit(1000, 240);

        Assert.Equal(15, result.BreakerAmps);
    }

    [Fact]
    public void SizeCircuit_HighWattage_LargerBreaker()
    {
        // 5000W at 240V = 20.8A → 125% = 26A → 30A breaker
        var result = HeatTracingService.SizeCircuit(5000, 240);

        Assert.Equal(30, result.BreakerAmps);
    }

    [Fact]
    public void SizeCircuit_WireSize_MatchesBreaker()
    {
        var result = HeatTracingService.SizeCircuit(1000, 240);

        Assert.Equal("14", result.WireSize); // 15A breaker → 14 AWG
    }

    [Fact]
    public void SizeCircuit_MaxLength_HigherVoltage_LongerRun()
    {
        var result120 = HeatTracingService.SizeCircuit(500, 120);
        var result240 = HeatTracingService.SizeCircuit(500, 240);

        Assert.True(result240.MaxCircuitLengthFt > result120.MaxCircuitLengthFt);
    }

    [Fact]
    public void SizeCircuit_CircuitAmps_Correct()
    {
        var result = HeatTracingService.SizeCircuit(2400, 240);

        Assert.Equal(10, result.CircuitAmps);
    }
}

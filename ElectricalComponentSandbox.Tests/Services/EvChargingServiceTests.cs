using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class EvChargingServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static EvChargingService.EvseStation Level2Station(double kw = 7.7) => new()
    {
        Id = "EV-1",
        Level = EvChargingService.EvseLevel.Level2,
        RatedKW = kw,
        Voltage = 240,
        Phases = 1,
        IsContinuousLoad = true,
    };

    private static EvChargingService.EvseStation DcFastStation(double kw = 150) => new()
    {
        Id = "DCFC-1",
        Level = EvChargingService.EvseLevel.DcFast,
        RatedKW = kw,
        Voltage = 480,
        Phases = 3,
        IsContinuousLoad = true,
    };

    // ── Branch Circuit Sizing ────────────────────────────────────────────────

    [Fact]
    public void SizeBranchCircuit_Level2_7kW_Applies125Percent()
    {
        var station = Level2Station(7.7);
        var result = EvChargingService.SizeBranchCircuit(station);

        // 7700W / 240V = 32.08A load; × 1.25 = 40.1A continuous → 45A breaker
        Assert.Equal(32.08, result.LoadAmps, 1);
        Assert.True(result.ContinuousAmps > result.LoadAmps);
        Assert.True(result.SelectedBreakerAmps >= result.ContinuousAmps);
    }

    [Fact]
    public void SizeBranchCircuit_Level2_48A_SelectsCorrectBreaker()
    {
        // 48A station (11.52 kW) → 48 × 1.25 = 60A → 60A breaker
        var station = Level2Station(11.52);
        var result = EvChargingService.SizeBranchCircuit(station);

        Assert.Equal(60, result.SelectedBreakerAmps);
    }

    [Fact]
    public void SizeBranchCircuit_NonContinuous_NoUpsizeFactor()
    {
        var station = new EvChargingService.EvseStation
        {
            Level = EvChargingService.EvseLevel.Level2,
            RatedKW = 7.7,
            Voltage = 240,
            Phases = 1,
            IsContinuousLoad = false,
        };
        var result = EvChargingService.SizeBranchCircuit(station);

        Assert.Equal(result.LoadAmps, result.ContinuousAmps);
    }

    [Fact]
    public void SizeBranchCircuit_DcFast_ThreePhase()
    {
        var station = DcFastStation(50);
        var result = EvChargingService.SizeBranchCircuit(station);

        // 50kW / (480 × √3) ≈ 60.14A; × 1.25 ≈ 75.2A → 80A breaker
        Assert.True(result.LoadAmps > 60 && result.LoadAmps < 61);
        Assert.Equal(80, result.SelectedBreakerAmps);
    }

    [Fact]
    public void SizeBranchCircuit_ReturnsAdequateWireSize()
    {
        var station = Level2Station(7.7);
        var result = EvChargingService.SizeBranchCircuit(station);

        Assert.False(string.IsNullOrEmpty(result.MinWireSize));
    }

    // ── Demand Factor ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 1.00)]
    [InlineData(3, 1.00)]
    [InlineData(5, 0.95)]
    [InlineData(7, 0.90)]
    [InlineData(10, 0.85)]
    [InlineData(15, 0.80)]
    [InlineData(30, 0.75)]
    [InlineData(50, 0.70)]
    public void GetDemandFactor_ReturnsCorrectFactor(int count, double expected)
    {
        Assert.Equal(expected, EvChargingService.GetDemandFactor(count));
    }

    [Fact]
    public void CalculateDemand_MultipleStations_AppliesFactor()
    {
        var stations = Enumerable.Range(1, 5)
            .Select(i => Level2Station(7.7))
            .ToList();
        var result = EvChargingService.CalculateDemand(stations, 240, 1);

        Assert.Equal(5, result.StationCount);
        Assert.Equal(38.5, result.TotalConnectedKW, 1);
        Assert.Equal(0.95, result.DemandFactor);
        Assert.True(result.DemandKW < result.TotalConnectedKW);
        Assert.True(result.DemandAmps > 0);
    }

    [Fact]
    public void CalculateDemand_SingleStation_NoReduction()
    {
        var stations = new[] { Level2Station(9.6) };
        var result = EvChargingService.CalculateDemand(stations, 240, 1);

        Assert.Equal(1.0, result.DemandFactor);
        Assert.Equal(result.TotalConnectedKW, result.DemandKW);
    }

    // ── Power Sharing ────────────────────────────────────────────────────────

    [Fact]
    public void EvaluatePowerSharing_AdequateCapacity_IsAdequate()
    {
        var stations = Enumerable.Range(1, 4).Select(_ => Level2Station(7.7)).ToList();
        var result = EvChargingService.EvaluatePowerSharing(
            40, stations, EvChargingService.LoadManagementMode.DynamicPowerSharing);

        Assert.Equal(10, result.KWPerStation);
        Assert.True(result.IsAdequate);
    }

    [Fact]
    public void EvaluatePowerSharing_InsufficientCapacity_NotAdequate()
    {
        var stations = Enumerable.Range(1, 10).Select(_ => Level2Station(7.7)).ToList();
        var result = EvChargingService.EvaluatePowerSharing(
            10, stations, EvChargingService.LoadManagementMode.DynamicPowerSharing);

        Assert.Equal(1.0, result.KWPerStation);
        Assert.False(result.IsAdequate); // 1.0 < 1.4 minimum
    }

    [Fact]
    public void EvaluatePowerSharing_NoManagement_NotAdequate()
    {
        var stations = new[] { Level2Station(7.7) };
        var result = EvChargingService.EvaluatePowerSharing(
            50, stations, EvChargingService.LoadManagementMode.None);

        Assert.False(result.IsAdequate);
    }

    [Fact]
    public void EvaluatePowerSharing_DcFast_HigherMinimum()
    {
        var stations = new[] { DcFastStation(150) };
        var result = EvChargingService.EvaluatePowerSharing(
            8, stations, EvChargingService.LoadManagementMode.DynamicPowerSharing);

        Assert.Equal(10.0, result.MinKWPerStation);
        Assert.False(result.IsAdequate); // 8 < 10
    }

    // ── Infrastructure Summary ───────────────────────────────────────────────

    [Fact]
    public void Summarize_MixedFleet_CorrectCounts()
    {
        var stations = new EvChargingService.EvseStation[]
        {
            new() { Level = EvChargingService.EvseLevel.Level1, RatedKW = 1.9, Voltage = 120, Phases = 1 },
            Level2Station(7.7),
            Level2Station(7.7),
            DcFastStation(50),
        };

        var summary = EvChargingService.Summarize(stations, 480, 3);

        Assert.Equal(1, summary.Level1Count);
        Assert.Equal(2, summary.Level2Count);
        Assert.Equal(1, summary.DcFastCount);
        Assert.True(summary.TotalConnectedKW > 67);
        Assert.True(summary.TotalDemandKW <= summary.TotalConnectedKW);
        Assert.True(summary.EstimatedMonthlyKWh > 0);
    }

    [Fact]
    public void Summarize_EstimatesMonthlyEnergy()
    {
        var stations = new[] { Level2Station(7.7) };
        var summary = EvChargingService.Summarize(stations, 240, 1, avgDailyHoursPerStation: 4.0);

        // 7.7 kW × 4 hr/day × 30 days = 924 kWh
        Assert.Equal(924.0, summary.EstimatedMonthlyKWh, 1);
    }
}

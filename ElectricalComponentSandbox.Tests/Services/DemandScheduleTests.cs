using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class DemandScheduleTests
{
    // ── DemandSchedule.Apply tier math ───────────────────────────────────────

    [Fact]
    public void Apply_LightingUnderFirstTier_Returns100Percent()
    {
        var schedule = DemandSchedule.GetNec220Defaults()
            .First(s => s.Classification == LoadClassification.Lighting);

        double result = schedule.Apply(2_000);

        Assert.Equal(2_000, result, precision: 2);
    }

    [Fact]
    public void Apply_LightingExactlyAtFirstTier_Returns100Percent()
    {
        var schedule = DemandSchedule.GetNec220Defaults()
            .First(s => s.Classification == LoadClassification.Lighting);

        double result = schedule.Apply(3_000);

        Assert.Equal(3_000, result, precision: 2);
    }

    [Fact]
    public void Apply_LightingBeyondFirstTier_AppliesTieredFactors()
    {
        // NEC 220.42: first 3000 VA @ 100%, remainder @ 35%
        var schedule = DemandSchedule.GetNec220Defaults()
            .First(s => s.Classification == LoadClassification.Lighting);

        double result = schedule.Apply(10_000);

        // 3000 * 1.0 + 7000 * 0.35 = 3000 + 2450 = 5450
        Assert.Equal(5_450, result, precision: 2);
    }

    [Fact]
    public void Apply_ReceptacleBeyondFirstTier_AppliesTieredFactors()
    {
        // NEC 220.44: first 10,000 VA @ 100%, remainder @ 50%
        var schedule = DemandSchedule.GetNec220Defaults()
            .First(s => s.Classification == LoadClassification.Power);

        double result = schedule.Apply(30_000);

        // 10000 * 1.0 + 20000 * 0.5 = 10000 + 10000 = 20000
        Assert.Equal(20_000, result, precision: 2);
    }

    [Fact]
    public void Apply_HVAC_AlwaysFullLoad()
    {
        var schedule = DemandSchedule.GetNec220Defaults()
            .First(s => s.Classification == LoadClassification.HVAC);

        double result = schedule.Apply(50_000);

        Assert.Equal(50_000, result, precision: 2);
    }

    [Fact]
    public void Apply_Other_AlwaysFullLoad()
    {
        var schedule = DemandSchedule.GetNec220Defaults()
            .First(s => s.Classification == LoadClassification.Other);

        double result = schedule.Apply(25_000);

        Assert.Equal(25_000, result, precision: 2);
    }

    [Fact]
    public void Apply_ZeroLoad_ReturnsZero()
    {
        var schedule = DemandSchedule.GetNec220Defaults()
            .First(s => s.Classification == LoadClassification.Lighting);

        double result = schedule.Apply(0);

        Assert.Equal(0, result, precision: 2);
    }

    [Fact]
    public void Apply_NegativeLoad_ReturnsNegative()
    {
        var schedule = DemandSchedule.GetNec220Defaults()
            .First(s => s.Classification == LoadClassification.Lighting);

        double result = schedule.Apply(-100);

        Assert.Equal(-100, result, precision: 2);
    }

    [Fact]
    public void Apply_EmptyTiers_ReturnsConnectedLoad()
    {
        var schedule = new DemandSchedule { Tiers = { } };

        double result = schedule.Apply(5_000);

        Assert.Equal(5_000, result, precision: 2);
    }

    // ── Custom override schedule ─────────────────────────────────────────────

    [Fact]
    public void Apply_CustomThreeTierSchedule_CorrectResult()
    {
        // Custom: first 5kVA @ 100%, next 10kVA @ 60%, remainder @ 30%
        var schedule = new DemandSchedule
        {
            Classification = LoadClassification.Power,
            Tiers =
            {
                new DemandTier { ThresholdVA = 5_000, Factor = 1.0 },
                new DemandTier { ThresholdVA = 10_000, Factor = 0.6 },
                new DemandTier { ThresholdVA = double.MaxValue, Factor = 0.3 }
            }
        };

        double result = schedule.Apply(25_000);

        // 5000 * 1.0 + 10000 * 0.6 + 10000 * 0.3 = 5000 + 6000 + 3000 = 14000
        Assert.Equal(14_000, result, precision: 2);
    }

    // ── GetNec220Defaults verification ───────────────────────────────────────

    [Fact]
    public void GetNec220Defaults_ReturnsFourSchedules()
    {
        var defaults = DemandSchedule.GetNec220Defaults();

        Assert.Equal(4, defaults.Count);
    }

    [Fact]
    public void GetNec220Defaults_AllMarkedBuiltIn()
    {
        var defaults = DemandSchedule.GetNec220Defaults();

        Assert.All(defaults, s => Assert.True(s.IsBuiltIn));
    }

    [Fact]
    public void GetNec220Defaults_CoversAllClassifications()
    {
        var defaults = DemandSchedule.GetNec220Defaults();
        var classifications = defaults.Select(s => s.Classification).ToHashSet();

        Assert.Contains(LoadClassification.Lighting, classifications);
        Assert.Contains(LoadClassification.Power, classifications);
        Assert.Contains(LoadClassification.HVAC, classifications);
        Assert.Contains(LoadClassification.Other, classifications);
    }

    // ── CalculateDemandLoad integration ──────────────────────────────────────

    [Fact]
    public void CalculateDemandLoad_MixedClassificationPanel_AppliesCorrectTiers()
    {
        var circuits = new List<Circuit>
        {
            MakeCircuit("1", LoadClassification.Lighting, 5_000),
            MakeCircuit("2", LoadClassification.Power, 20_000),
            MakeCircuit("3", LoadClassification.HVAC, 10_000),
            MakeCircuit("4", LoadClassification.Other, 3_000),
        };

        var result = ElectricalCalculationService.CalculateDemandLoad(
            circuits, DemandSchedule.GetNec220Defaults());

        // Lighting: 3000*1.0 + 2000*0.35 = 3700
        Assert.Equal(3_700, result.ClassificationDetails[LoadClassification.Lighting].DemandVA, precision: 2);
        // Power: 10000*1.0 + 10000*0.5 = 15000
        Assert.Equal(15_000, result.ClassificationDetails[LoadClassification.Power].DemandVA, precision: 2);
        // HVAC: 10000 * 1.0 = 10000
        Assert.Equal(10_000, result.ClassificationDetails[LoadClassification.HVAC].DemandVA, precision: 2);
        // Other: 3000 * 1.0 = 3000
        Assert.Equal(3_000, result.ClassificationDetails[LoadClassification.Other].DemandVA, precision: 2);

        // Total: 3700 + 15000 + 10000 + 3000 = 31700
        Assert.Equal(31_700, result.TotalDemandVA, precision: 2);
        Assert.Equal(38_000, result.TotalConnectedVA, precision: 2);
    }

    [Fact]
    public void CalculateDemandLoad_AllLighting_AggregatesBeforeApplyingTier()
    {
        // Two lighting circuits; their connected VA should be summed before tier application
        var circuits = new List<Circuit>
        {
            MakeCircuit("1", LoadClassification.Lighting, 2_000),
            MakeCircuit("2", LoadClassification.Lighting, 6_000),
        };

        var result = ElectricalCalculationService.CalculateDemandLoad(
            circuits, DemandSchedule.GetNec220Defaults());

        // Combined: 8000 VA → 3000*1.0 + 5000*0.35 = 3000 + 1750 = 4750
        Assert.Equal(4_750, result.TotalDemandVA, precision: 2);
    }

    [Fact]
    public void CalculateDemandLoad_ExcludesSpareAndSpaceCircuits()
    {
        var circuits = new List<Circuit>
        {
            MakeCircuit("1", LoadClassification.Lighting, 5_000),
            MakeCircuit("S1", LoadClassification.Power, 0, CircuitSlotType.Spare),
            MakeCircuit("S2", LoadClassification.Power, 0, CircuitSlotType.Space),
        };

        var result = ElectricalCalculationService.CalculateDemandLoad(
            circuits, DemandSchedule.GetNec220Defaults());

        Assert.Single(result.ClassificationDetails);
        Assert.True(result.ClassificationDetails.ContainsKey(LoadClassification.Lighting));
    }

    [Fact]
    public void CalculateDemandLoad_NoMatchingSchedule_PassesThroughAt100Percent()
    {
        // Only provide a Lighting schedule, but circuit is HVAC
        var circuits = new List<Circuit>
        {
            MakeCircuit("1", LoadClassification.HVAC, 10_000),
        };
        var schedules = new[]
        {
            new DemandSchedule
            {
                Classification = LoadClassification.Lighting,
                Tiers = { new DemandTier { ThresholdVA = 3_000, Factor = 1.0 },
                          new DemandTier { ThresholdVA = double.MaxValue, Factor = 0.35 } }
            }
        };

        var result = ElectricalCalculationService.CalculateDemandLoad(circuits, schedules);

        Assert.Equal(10_000, result.TotalDemandVA, precision: 2);
        Assert.Equal(1.0, result.OverallFactor, precision: 4);
    }

    [Fact]
    public void CalculateDemandLoad_EmptyCircuits_ReturnsZeros()
    {
        var result = ElectricalCalculationService.CalculateDemandLoad(
            Array.Empty<Circuit>(), DemandSchedule.GetNec220Defaults());

        Assert.Equal(0, result.TotalConnectedVA);
        Assert.Equal(0, result.TotalDemandVA);
        Assert.Equal(1.0, result.OverallFactor);
        Assert.Empty(result.ClassificationDetails);
    }

    [Fact]
    public void CalculateDemandLoad_OverallFactor_IsCorrect()
    {
        var circuits = new List<Circuit>
        {
            MakeCircuit("1", LoadClassification.Lighting, 10_000),
        };

        var result = ElectricalCalculationService.CalculateDemandLoad(
            circuits, DemandSchedule.GetNec220Defaults());

        // Lighting: 3000*1.0 + 7000*0.35 = 5450 → factor = 5450/10000 = 0.545
        Assert.Equal(0.545, result.OverallFactor, precision: 4);
    }

    [Fact]
    public void CalculateDemandLoad_CustomOverrideSchedule_TakesPrecedence()
    {
        var circuits = new List<Circuit>
        {
            MakeCircuit("1", LoadClassification.Power, 20_000),
        };

        // Custom receptacle schedule: first 5kVA @ 100%, rest @ 25%
        var customSchedules = new[]
        {
            new DemandSchedule
            {
                Classification = LoadClassification.Power,
                Tiers =
                {
                    new DemandTier { ThresholdVA = 5_000, Factor = 1.0 },
                    new DemandTier { ThresholdVA = double.MaxValue, Factor = 0.25 }
                }
            }
        };

        var result = ElectricalCalculationService.CalculateDemandLoad(circuits, customSchedules);

        // 5000 * 1.0 + 15000 * 0.25 = 5000 + 3750 = 8750
        Assert.Equal(8_750, result.TotalDemandVA, precision: 2);
    }

    // ── Panel schedule footer with demand ────────────────────────────────────

    [Fact]
    public void GeneratePanelSchedule_WithDemandSchedules_HasDemandFooterRow()
    {
        var panel = new PanelSchedule
        {
            PanelName = "PANEL A",
            BusAmps = 200,
            MainBreakerAmps = 200,
            Circuits =
            {
                new Circuit
                {
                    CircuitNumber = "1",
                    SlotNumber = 1,
                    ConnectedLoadVA = 5_000,
                    LoadClassification = LoadClassification.Lighting,
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    Phase = "A",
                },
                new Circuit
                {
                    CircuitNumber = "2",
                    SlotNumber = 2,
                    ConnectedLoadVA = 20_000,
                    LoadClassification = LoadClassification.Power,
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    Phase = "B",
                }
            }
        };

        var svc = new ScheduleTableService();
        var table = svc.GeneratePanelSchedule(
            panel, demandSchedules: DemandSchedule.GetNec220Defaults());

        // The last row should be the NEC demand footer
        var lastRow = table.Rows[^1];
        var lastStyle = table.RowStyles[^1];

        Assert.Equal(ScheduleRowStyle.Footer, lastStyle);
        Assert.Contains("NEC Demand:", lastRow[1]);
        Assert.Contains("Connected:", lastRow[0]);
        Assert.Contains("Factor:", lastRow[2]);
    }

    [Fact]
    public void GeneratePanelSchedule_WithoutDemandSchedules_NoDemandFooterRow()
    {
        var panel = new PanelSchedule
        {
            PanelName = "PANEL B",
            BusAmps = 100,
            MainBreakerAmps = 100,
            Circuits =
            {
                new Circuit
                {
                    CircuitNumber = "1",
                    SlotNumber = 1,
                    ConnectedLoadVA = 1_000,
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    Phase = "A",
                }
            }
        };

        var svc = new ScheduleTableService();
        var table = svc.GeneratePanelSchedule(panel);

        // Should only have one footer row (the phase totals), no NEC demand row
        int footerCount = table.RowStyles.Count(s => s == ScheduleRowStyle.Footer);
        Assert.Equal(1, footerCount);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Circuit MakeCircuit(
        string number,
        LoadClassification classification,
        double connectedVA,
        CircuitSlotType slotType = CircuitSlotType.Circuit)
    {
        return new Circuit
        {
            CircuitNumber = number,
            ConnectedLoadVA = connectedVA,
            LoadClassification = classification,
            SlotType = slotType,
            Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
            Phase = "A",
        };
    }
}

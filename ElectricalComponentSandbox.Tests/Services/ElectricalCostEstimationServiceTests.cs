using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using static ElectricalComponentSandbox.Services.ElectricalCostEstimationService;

namespace ElectricalComponentSandbox.Tests.Services;

public class ElectricalCostEstimationServiceTests
{
    // ── Wire Costs ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("14", ConductorMaterial.Copper)]
    [InlineData("12", ConductorMaterial.Copper)]
    [InlineData("4/0", ConductorMaterial.Copper)]
    [InlineData("4/0", ConductorMaterial.Aluminum)]
    public void WireCostPerFoot_Positive(string size, ConductorMaterial mat)
    {
        double cost = ElectricalCostEstimationService.GetWireCostPerFoot(size, mat);
        Assert.True(cost > 0);
    }

    [Fact]
    public void AluminumCheaperThanCopper()
    {
        double cu = ElectricalCostEstimationService.GetWireCostPerFoot("4/0", ConductorMaterial.Copper);
        double al = ElectricalCostEstimationService.GetWireCostPerFoot("4/0", ConductorMaterial.Aluminum);
        Assert.True(al < cu, "Aluminum should be cheaper than copper");
    }

    // ── Conduit Costs ────────────────────────────────────────────────────────

    [Fact]
    public void ConduitCost_IncreasesWithSize()
    {
        double small = ElectricalCostEstimationService.GetConduitCostPerFoot("1/2");
        double large = ElectricalCostEstimationService.GetConduitCostPerFoot("4");
        Assert.True(large > small);
    }

    // ── Breaker Costs ────────────────────────────────────────────────────────

    [Fact]
    public void BreakerCost_IncreasesWithAmps()
    {
        double small = ElectricalCostEstimationService.GetBreakerCost(20);
        double large = ElectricalCostEstimationService.GetBreakerCost(200);
        Assert.True(large > small);
    }

    [Fact]
    public void BreakerCost_NonStandard_ReturnsClosest()
    {
        double cost = ElectricalCostEstimationService.GetBreakerCost(22);
        Assert.True(cost > 0);
    }

    // ── Full Estimate ────────────────────────────────────────────────────────

    [Fact]
    public void Estimate_Empty_ZeroCost()
    {
        var result = ElectricalCostEstimationService.Estimate(new CostEstimateInput());
        Assert.Equal(0, result.LineItems.Count);
        Assert.Equal(0, result.Subtotal);
    }

    [Fact]
    public void Estimate_WithPanels_IncludesPanelLineItem()
    {
        var input = new CostEstimateInput
        {
            PanelSchedules = new List<PanelSchedule>
            {
                new() { PanelId = "P1", PanelName = "LP-1", BusAmps = 200, VoltageConfig = PanelVoltageConfig.V120_208_3Ph },
            },
        };

        var result = ElectricalCostEstimationService.Estimate(input);
        Assert.Single(result.LineItems);
        Assert.Equal("Panels", result.LineItems[0].Category);
        Assert.True(result.TotalMaterialCost > 0);
        Assert.True(result.TotalLaborHours > 0);
    }

    [Fact]
    public void Estimate_WithBreakers_GroupsByType()
    {
        var circuits = new List<Circuit>
        {
            MakeCircuit("A", 20, 1), MakeCircuit("B", 20, 1), MakeCircuit("C", 30, 2),
        };

        var input = new CostEstimateInput { Circuits = circuits };
        var result = ElectricalCostEstimationService.Estimate(input);

        // Should have 2 breaker groups: 20A/1P and 30A/2P
        var breakerItems = result.LineItems.Where(i => i.Category == "Breakers").ToList();
        Assert.Equal(2, breakerItems.Count);
        Assert.Contains(breakerItems, i => i.Quantity == 2); // two 20A/1P
        Assert.Contains(breakerItems, i => i.Quantity == 1); // one 30A/2P
    }

    [Fact]
    public void Estimate_WithWireRuns_CalculatesTotal()
    {
        var input = new CostEstimateInput
        {
            WireRuns = new List<WireRunInput>
            {
                new() { WireSize = "12", Material = ConductorMaterial.Copper, LengthFeet = 100, ConductorCount = 3, Description = "Branch run" },
            },
        };

        var result = ElectricalCostEstimationService.Estimate(input);
        Assert.Single(result.LineItems);
        Assert.Equal("Wire", result.LineItems[0].Category);
        Assert.Equal(300, result.LineItems[0].Quantity); // 100 × 3 conductors
    }

    [Fact]
    public void Estimate_WithConduit_CalculatesTotal()
    {
        var input = new CostEstimateInput
        {
            ConduitRuns = new List<ConduitRunInput>
            {
                new() { TradeSize = "3/4", LengthFeet = 200, Description = "Main run" },
            },
        };

        var result = ElectricalCostEstimationService.Estimate(input);
        Assert.Single(result.LineItems);
        Assert.Equal("Conduit", result.LineItems[0].Category);
        Assert.Equal(200, result.LineItems[0].Quantity);
    }

    [Fact]
    public void Estimate_OverheadAndProfit_Applied()
    {
        var input = new CostEstimateInput
        {
            OverheadAndProfitPercent = 20,
            WireRuns = new List<WireRunInput>
            {
                new() { WireSize = "12", Material = ConductorMaterial.Copper, LengthFeet = 1000, ConductorCount = 3 },
            },
        };

        var result = ElectricalCostEstimationService.Estimate(input);
        Assert.True(result.OverheadAndProfit > 0);
        Assert.True(result.GrandTotal > result.Subtotal);
        double expectedOH = result.Subtotal * 0.20;
        Assert.InRange(result.OverheadAndProfit, expectedOH - 1, expectedOH + 1);
    }

    [Fact]
    public void Estimate_GrandTotal_IsSumOfSubtotalAndOverhead()
    {
        var input = new CostEstimateInput
        {
            OverheadAndProfitPercent = 15,
            PanelSchedules = new List<PanelSchedule>
            {
                new() { PanelId = "P1", PanelName = "LP-1", BusAmps = 200, VoltageConfig = PanelVoltageConfig.V120_208_3Ph },
            },
            Circuits = new List<Circuit>
            {
                MakeCircuit("A", 20, 1), MakeCircuit("B", 20, 1),
            },
            WireRuns = new List<WireRunInput>
            {
                new() { WireSize = "12", Material = ConductorMaterial.Copper, LengthFeet = 200, ConductorCount = 3 },
            },
            ConduitRuns = new List<ConduitRunInput>
            {
                new() { TradeSize = "3/4", LengthFeet = 200 },
            },
        };

        var result = ElectricalCostEstimationService.Estimate(input);
        Assert.Equal(result.Subtotal + result.OverheadAndProfit, result.GrandTotal, 0.01);
    }

    // ── Real-World Estimate ──────────────────────────────────────────────────

    [Fact]
    public void RealWorld_SmallOffice()
    {
        var circuits = Enumerable.Range(1, 24).Select(i =>
            MakeCircuit(i % 3 == 0 ? "C" : i % 2 == 0 ? "B" : "A", 20, 1)).ToList();

        var input = new CostEstimateInput
        {
            ProjectName = "Small Office TI",
            OverheadAndProfitPercent = 15,
            PanelSchedules = new List<PanelSchedule>
            {
                new() { PanelId = "LP1", PanelName = "Lighting Panel", BusAmps = 200, VoltageConfig = PanelVoltageConfig.V120_208_3Ph },
            },
            Circuits = circuits,
            WireRuns = Enumerable.Range(1, 24).Select(i =>
                new WireRunInput { WireSize = "12", Material = ConductorMaterial.Copper, LengthFeet = 75, ConductorCount = 3, Description = $"Ckt {i}" }).ToList(),
            ConduitRuns = new List<ConduitRunInput>
            {
                new() { TradeSize = "3/4", LengthFeet = 1800, Description = "EMT home runs" },
            },
        };

        var result = ElectricalCostEstimationService.Estimate(input);

        Assert.Equal("Small Office TI", result.ProjectName);
        Assert.True(result.GrandTotal > 2000, "24-circuit office should cost > $2000");
        Assert.True(result.TotalLaborHours > 10, "Should require meaningful labor hours");
        Assert.True(result.LineItems.Count >= 3, "Should have panel, breaker, wire, conduit items");
    }

    [Fact]
    public void LargerWire_HigherCostPerFoot()
    {
        double small = ElectricalCostEstimationService.GetWireCostPerFoot("12", ConductorMaterial.Copper);
        double large = ElectricalCostEstimationService.GetWireCostPerFoot("500", ConductorMaterial.Copper);
        Assert.True(large > small * 10, "500 kcmil should be much more expensive than #12");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Circuit MakeCircuit(string phase, int tripAmps, int poles) => new()
    {
        Id = Guid.NewGuid().ToString(),
        CircuitNumber = tripAmps.ToString(),
        Phase = phase,
        Poles = poles,
        ConnectedLoadVA = tripAmps * 120,
        DemandFactor = 1.0,
        SlotType = CircuitSlotType.Circuit,
        Breaker = new CircuitBreaker { TripAmps = tripAmps, Poles = poles },
        Wire = new WireSpec { Size = "12", Material = ConductorMaterial.Copper },
    };
}

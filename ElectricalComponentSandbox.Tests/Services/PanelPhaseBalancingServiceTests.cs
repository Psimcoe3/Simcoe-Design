using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class PanelPhaseBalancingServiceTests
{
    // ── Imbalance Calculation ────────────────────────────────────────────────

    [Fact]
    public void CalculateImbalancePercent_PerfectBalance_Zero()
    {
        double pct = PanelPhaseBalancingService.CalculateImbalancePercent(1000, 1000, 1000);
        Assert.Equal(0, pct);
    }

    [Fact]
    public void CalculateImbalancePercent_AllZero_Zero()
    {
        double pct = PanelPhaseBalancingService.CalculateImbalancePercent(0, 0, 0);
        Assert.Equal(0, pct);
    }

    [Fact]
    public void CalculateImbalancePercent_Moderate_CorrectValue()
    {
        // A=12000, B=10000, C=8000 → avg=10000, maxDev=2000, 20%
        double pct = PanelPhaseBalancingService.CalculateImbalancePercent(12000, 10000, 8000);
        Assert.Equal(20, pct, 0.1);
    }

    [Fact]
    public void CalculateImbalancePercent_SlightImbalance()
    {
        // A=10500, B=10000, C=9500 → avg=10000, maxDev=500, 5%
        double pct = PanelPhaseBalancingService.CalculateImbalancePercent(10500, 10000, 9500);
        Assert.Equal(5, pct, 0.1);
    }

    // ── Balanced Panel ───────────────────────────────────────────────────────

    [Fact]
    public void Analyze_BalancedPanel_Acceptable()
    {
        var schedule = MakeSchedule(
            ("1", "A", 3600),
            ("2", "B", 3600),
            ("3", "C", 3600));

        var result = PanelPhaseBalancingService.Analyze(schedule);

        Assert.True(result.IsAcceptable);
        Assert.Equal(0, result.ImbalancePercent);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public void Analyze_NearlyBalanced_Acceptable()
    {
        var schedule = MakeSchedule(
            ("1", "A", 3600),
            ("2", "B", 3500),
            ("3", "C", 3400));

        var result = PanelPhaseBalancingService.Analyze(schedule);

        Assert.True(result.IsAcceptable);
        Assert.True(result.ImbalancePercent <= 10);
    }

    // ── Imbalanced Panel ─────────────────────────────────────────────────────

    [Fact]
    public void Analyze_HeavyImbalance_NotAcceptable()
    {
        var schedule = MakeSchedule(
            ("1", "A", 10000),
            ("2", "A", 8000),
            ("3", "B", 2000),
            ("4", "C", 2000));

        var result = PanelPhaseBalancingService.Analyze(schedule);

        Assert.False(result.IsAcceptable);
        Assert.True(result.ImbalancePercent > 10);
    }

    [Fact]
    public void Analyze_Imbalanced_GeneratesRecommendations()
    {
        var schedule = MakeSchedule(
            ("1", "A", 10000),
            ("2", "A", 5000),
            ("3", "B", 2000),
            ("4", "C", 1000));

        var result = PanelPhaseBalancingService.Analyze(schedule);

        Assert.NotEmpty(result.Recommendations);
        Assert.True(result.OptimizedImbalancePercent < result.ImbalancePercent,
            "Optimized imbalance should be less than current");
    }

    [Fact]
    public void Analyze_Recommendations_MoveFromHeaviestToLightest()
    {
        var schedule = MakeSchedule(
            ("1", "A", 10000),
            ("2", "A", 5000),
            ("3", "B", 1000),
            ("4", "C", 1000));

        var result = PanelPhaseBalancingService.Analyze(schedule);

        // First recommendation should move from A (heaviest) to B or C (lightest)
        var first = result.Recommendations[0];
        Assert.Equal("A", first.CurrentPhase);
        Assert.Contains(first.ProposedPhase, new[] { "B", "C" });
    }

    [Fact]
    public void Analyze_Recommendations_EachSwapImproves()
    {
        var schedule = MakeSchedule(
            ("1", "A", 8000),
            ("2", "A", 6000),
            ("3", "A", 4000),
            ("4", "B", 1000),
            ("5", "C", 1000));

        var result = PanelPhaseBalancingService.Analyze(schedule);

        for (int i = 0; i < result.Recommendations.Count; i++)
        {
            var swap = result.Recommendations[i];
            Assert.True(swap.ImbalanceAfterVA < swap.ImbalanceBeforeVA,
                $"Swap {i} should reduce imbalance");
        }
    }

    // ── Multi-Pole Circuits ──────────────────────────────────────────────────

    [Fact]
    public void Analyze_MultiPole_NotMoved()
    {
        // Multi-pole circuits span phases and should not be swapped
        var schedule = new PanelSchedule
        {
            PanelId = "P1", PanelName = "Test", BusAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                MakeCircuit("1", "AB", 10000, 2),
                MakeCircuit("3", "C", 1000, 1),
            },
        };

        var result = PanelPhaseBalancingService.Analyze(schedule);

        // Should not recommend swapping the 2-pole circuit
        Assert.DoesNotContain(result.Recommendations, r => r.CircuitId == "1");
    }

    // ── Apply Recommendations ────────────────────────────────────────────────

    [Fact]
    public void ApplyRecommendations_UpdatesCircuitPhases()
    {
        var schedule = MakeSchedule(
            ("1", "A", 10000),
            ("2", "A", 5000),
            ("3", "B", 1000),
            ("4", "C", 1000));

        var analysis = PanelPhaseBalancingService.Analyze(schedule);
        Assert.NotEmpty(analysis.Recommendations);

        PanelPhaseBalancingService.ApplyRecommendations(schedule, analysis.Recommendations);

        // After applying, verify the circuit phases changed
        foreach (var swap in analysis.Recommendations)
        {
            var circuit = schedule.Circuits.First(c => c.Id == swap.CircuitId);
            Assert.Equal(swap.ProposedPhase, circuit.Phase);
        }
    }

    [Fact]
    public void ApplyRecommendations_ImprovesBalance()
    {
        var schedule = MakeSchedule(
            ("1", "A", 10000),
            ("2", "A", 5000),
            ("3", "B", 1000),
            ("4", "C", 1000));

        var before = PanelPhaseBalancingService.Analyze(schedule);
        PanelPhaseBalancingService.ApplyRecommendations(schedule, before.Recommendations);
        var after = PanelPhaseBalancingService.Analyze(schedule);

        Assert.True(after.ImbalancePercent < before.ImbalancePercent,
            $"After applying swaps: {after.ImbalancePercent}% should be < {before.ImbalancePercent}%");
    }

    // ── Neutral Current ──────────────────────────────────────────────────────

    [Fact]
    public void Analyze_Balanced_LowNeutralCurrent()
    {
        var schedule = MakeSchedule(
            ("1", "A", 3600),
            ("2", "B", 3600),
            ("3", "C", 3600));

        var result = PanelPhaseBalancingService.Analyze(schedule);
        Assert.Equal(0, result.EstimatedNeutralCurrentAmps, 0.1);
    }

    [Fact]
    public void Analyze_Unbalanced_HigherNeutralCurrent()
    {
        var schedule = MakeSchedule(
            ("1", "A", 10000),
            ("2", "B", 2000),
            ("3", "C", 2000));

        var result = PanelPhaseBalancingService.Analyze(schedule);
        Assert.True(result.EstimatedNeutralCurrentAmps > 0);
    }

    // ── Edge Cases ───────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_EmptyPanel_Acceptable()
    {
        var schedule = new PanelSchedule
        {
            PanelId = "P1", PanelName = "Empty", BusAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
        };

        var result = PanelPhaseBalancingService.Analyze(schedule);

        Assert.True(result.IsAcceptable);
        Assert.Equal(0, result.ImbalancePercent);
    }

    [Fact]
    public void Analyze_SinglePhaseOnly_TwoPhaseImbalance()
    {
        // All load on phase A only
        var schedule = MakeSchedule(
            ("1", "A", 5000),
            ("2", "A", 5000));

        var result = PanelPhaseBalancingService.Analyze(schedule);

        Assert.False(result.IsAcceptable);
        Assert.True(result.ImbalancePercent > 50);
    }

    [Fact]
    public void Analyze_SparesAndSpaces_Ignored()
    {
        var schedule = new PanelSchedule
        {
            PanelId = "P1", PanelName = "Test", BusAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                MakeCircuit("1", "A", 3000, 1),
                MakeCircuit("2", "B", 3000, 1),
                MakeCircuit("3", "C", 3000, 1),
                new Circuit { Id = "SP1", Phase = "A", SlotType = CircuitSlotType.Spare, ConnectedLoadVA = 0 },
                new Circuit { Id = "SP2", Phase = "B", SlotType = CircuitSlotType.Space, ConnectedLoadVA = 0 },
            },
        };

        var result = PanelPhaseBalancingService.Analyze(schedule);
        Assert.True(result.IsAcceptable);
    }

    // ── Real-World Scenario ──────────────────────────────────────────────────

    [Fact]
    public void RealWorld_CommercialLightingPanel()
    {
        // Typical lighting panel with poor initial balance
        var schedule = new PanelSchedule
        {
            PanelId = "LP1", PanelName = "Lighting Panel 1", BusAmps = 100,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                MakeCircuit("1", "A", 2400, 1, "Level 1 Lighting"),
                MakeCircuit("3", "A", 2400, 1, "Level 2 Lighting"),
                MakeCircuit("5", "A", 2400, 1, "Level 3 Lighting"),
                MakeCircuit("7", "A", 1800, 1, "Level 4 Lighting"),
                MakeCircuit("2", "B", 1200, 1, "Stairwell A"),
                MakeCircuit("4", "B", 1200, 1, "Stairwell B"),
                MakeCircuit("6", "C", 800, 1, "Exit Signs"),
                MakeCircuit("8", "C", 600, 1, "Exterior"),
            },
        };

        var result = PanelPhaseBalancingService.Analyze(schedule);

        Assert.False(result.IsAcceptable, "Initial panel should be imbalanced");
        Assert.NotEmpty(result.Recommendations);

        // After optimization, should be much better
        Assert.True(result.OptimizedImbalancePercent < result.ImbalancePercent);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PanelSchedule MakeSchedule(params (string num, string phase, double loadVA)[] circuits)
    {
        return new PanelSchedule
        {
            PanelId = "P1", PanelName = "Test Panel", BusAmps = 200,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = circuits.Select(c => MakeCircuit(c.num, c.phase, c.loadVA)).ToList(),
        };
    }

    private static Circuit MakeCircuit(string num, string phase, double loadVA, int poles = 1, string desc = "")
    {
        return new Circuit
        {
            Id = num,
            CircuitNumber = num,
            Phase = phase,
            Poles = poles,
            ConnectedLoadVA = loadVA,
            Voltage = 120,
            Description = desc.Length > 0 ? desc : $"Circuit {num}",
        };
    }
}

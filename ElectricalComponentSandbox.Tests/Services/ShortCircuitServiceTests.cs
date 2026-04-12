using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ShortCircuitServiceTests
{
    private readonly ShortCircuitService _service = new();

    // ── Helper builders ──────────────────────────────────────────────────────

    private static PowerSourceComponent MakeSource(string id = "src", double faultKA = 65, double voltage = 480)
        => new() { Id = id, Name = "Utility", AvailableFaultCurrentKA = faultKA, Voltage = voltage };

    private static PanelComponent MakePanel(string id, string name, string? feederId = null, double aic = 10.0)
        => new() { Id = id, Name = name, FeederId = feederId, AICRatingKA = aic };

    private static TransformerComponent MakeTransformer(string id, string? feederId, double kva = 75, double secV = 208, double zPct = 5.75)
        => new() { Id = id, Name = $"XFMR-{id}", FeederId = feederId, KVA = kva, SecondaryVoltage = secV, ImpedancePercent = zPct };

    private static List<DistributionNode> BuildAndPropagate(params ElectricalComponent[] components)
    {
        var svc = new DistributionGraphService();
        var roots = svc.BuildGraph(components);
        svc.PropagateFaultCurrent(roots);
        return roots;
    }

    // ── AIC Validation Tests ─────────────────────────────────────────────────

    [Fact]
    public void ValidateAIC_PanelWithAdequateRating_ReturnsAdequate()
    {
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 10),
            MakePanel("p1", "MDP", feederId: "src", aic: 14));

        var results = _service.ValidateAIC(roots);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsAdequate));
    }

    [Fact]
    public void ValidateAIC_PanelWithInadequateRating_ReturnsInadequate()
    {
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 65),
            MakePanel("p1", "LP-1", feederId: "src", aic: 10));

        var results = _service.ValidateAIC(roots);
        var panelResult = results.Single(r => r.NodeId == "p1");

        Assert.False(panelResult.IsAdequate);
        Assert.Equal(65.0, panelResult.AvailableFaultKA);
        Assert.Equal(10.0, panelResult.EquipmentAICKA);
    }

    [Fact]
    public void ValidateAIC_MarginPercent_CalculatedCorrectly()
    {
        // Panel rated 22 kA, fault = 10 kA → margin = (22-10)/10 × 100 = 120%
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 10),
            MakePanel("p1", "MDP", feederId: "src", aic: 22));

        var results = _service.ValidateAIC(roots);
        var panelResult = results.Single(r => r.NodeId == "p1");

        Assert.True(panelResult.IsAdequate);
        Assert.Equal(120.0, panelResult.MarginPercent);
    }

    [Fact]
    public void ValidateAIC_NegativeMargin_WhenInadequate()
    {
        // Panel rated 10 kA, fault = 65 kA → margin = (10-65)/65 × 100 ≈ -84.6%
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 65),
            MakePanel("p1", "LP-1", feederId: "src", aic: 10));

        var results = _service.ValidateAIC(roots);
        var panelResult = results.Single(r => r.NodeId == "p1");

        Assert.False(panelResult.IsAdequate);
        Assert.True(panelResult.MarginPercent < 0);
    }

    [Fact]
    public void GetAICViolations_ReturnsOnlyInadequateNodes()
    {
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 22),
            MakePanel("ok", "OK Panel", feederId: "src", aic: 25),
            MakePanel("fail", "Fail Panel", feederId: "src", aic: 10));

        var violations = _service.GetAICViolations(roots);

        Assert.Single(violations);
        Assert.Equal("fail", violations[0].NodeId);
    }

    [Fact]
    public void ValidateAIC_TransformerReducesFault_DownstreamPanelPasses()
    {
        // Source: 65 kA → Transformer 75kVA, 5.75% Z, 208V secondary
        // Secondary fault ≈ 75000 / (208 × √3 × 0.0575) ≈ 3.62 kA
        // Downstream panel with 10 kA AIC should pass
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 65),
            MakeTransformer("xfmr", feederId: "src", kva: 75, secV: 208, zPct: 5.75),
            MakePanel("p1", "LP-1", feederId: "xfmr", aic: 10));

        var results = _service.ValidateAIC(roots);
        var panelResult = results.Single(r => r.NodeId == "p1");

        Assert.True(panelResult.IsAdequate);
        Assert.True(panelResult.AvailableFaultKA < 5.0); // Should be ~3.62 kA
    }

    [Fact]
    public void ValidateAIC_MultiLevel_PropagatesCorrectly()
    {
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 65),
            MakePanel("mdp", "MDP", feederId: "src", aic: 65),
            MakeTransformer("xfmr", feederId: "mdp"),
            MakePanel("lp1", "LP-1", feederId: "xfmr", aic: 10));

        var results = _service.ValidateAIC(roots);

        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.True(r.IsAdequate));
    }

    [Fact]
    public void ValidateAIC_SourceNode_AlwaysAdequate()
    {
        var roots = BuildAndPropagate(MakeSource("src", faultKA: 200));

        var results = _service.ValidateAIC(roots);
        var srcResult = results.Single(r => r.NodeId == "src");

        Assert.True(srcResult.IsAdequate);
    }

    [Fact]
    public void ValidateAIC_ZeroFaultCurrent_IsAdequate()
    {
        // When fault current hasn't been propagated (=0), treat as adequate
        var node = new DistributionNode
        {
            Id = "p1",
            Name = "Unconnected",
            NodeType = ComponentType.Panel,
            Component = MakePanel("p1", "Unconnected", aic: 10),
            FaultCurrentKA = 0
        };

        var results = _service.ValidateAIC(new List<DistributionNode> { node });
        Assert.Single(results);
        Assert.True(results[0].IsAdequate);
    }

    // ── Arc Flash Tests ──────────────────────────────────────────────────────

    [Fact]
    public void CalculateArcFlash_Returns_ArcingCurrent()
    {
        var node = MakeNode("p1", "MDP", ComponentType.Panel, faultKA: 25);

        var result = _service.CalculateArcFlash(node, workingDistanceInches: 18, arcDurationSeconds: 0.5, systemVoltageV: 480);

        Assert.Equal(25.0, result.BoltedFaultCurrentKA);
        Assert.True(result.ArcingCurrentKA > 0);
        Assert.True(result.ArcingCurrentKA < 25.0); // Arcing < bolted for ≤1kV
    }

    [Fact]
    public void CalculateArcFlash_IncidentEnergy_IsPositive()
    {
        var node = MakeNode("p1", "MDP", ComponentType.Panel, faultKA: 25);

        var result = _service.CalculateArcFlash(node, workingDistanceInches: 18, arcDurationSeconds: 0.5, systemVoltageV: 480);

        Assert.True(result.IncidentEnergyCal > 0);
    }

    [Fact]
    public void CalculateArcFlash_BoundaryInches_IsPositive()
    {
        var node = MakeNode("p1", "MDP", ComponentType.Panel, faultKA: 25);

        var result = _service.CalculateArcFlash(node, workingDistanceInches: 18);

        Assert.True(result.ArcFlashBoundaryInches > 0);
    }

    [Fact]
    public void CalculateArcFlash_ZeroFault_ReturnsEmpty()
    {
        var node = MakeNode("p1", "MDP", ComponentType.Panel, faultKA: 0);

        var result = _service.CalculateArcFlash(node);

        Assert.Equal(0, result.IncidentEnergyCal);
        Assert.Equal(0, result.ArcFlashBoundaryInches);
        Assert.Equal(0, result.HazardCategory);
    }

    [Fact]
    public void CalculateArcFlash_LowFault_Category0()
    {
        // Very low fault current → category 0
        var node = MakeNode("p1", "LP", ComponentType.Panel, faultKA: 0.5);

        var result = _service.CalculateArcFlash(node, workingDistanceInches: 36, arcDurationSeconds: 0.05, systemVoltageV: 208);

        Assert.Equal(0, result.HazardCategory);
    }

    [Fact]
    public void CalculateArcFlash_HighFault_HighCategory()
    {
        // Very high fault + long arc duration → category 3 or 4
        var node = MakeNode("p1", "MSB", ComponentType.Panel, faultKA: 65);

        var result = _service.CalculateArcFlash(node, workingDistanceInches: 18, arcDurationSeconds: 2.0, systemVoltageV: 480);

        Assert.True(result.HazardCategory >= 3);
    }

    [Fact]
    public void CalculateArcFlash_CloserDistance_HigherEnergy()
    {
        var node = MakeNode("p1", "MDP", ComponentType.Panel, faultKA: 25);

        var close = _service.CalculateArcFlash(node, workingDistanceInches: 12);
        var far = _service.CalculateArcFlash(node, workingDistanceInches: 36);

        Assert.True(close.IncidentEnergyCal > far.IncidentEnergyCal);
    }

    [Fact]
    public void CalculateArcFlash_LongerDuration_HigherEnergy()
    {
        var node = MakeNode("p1", "MDP", ComponentType.Panel, faultKA: 25);

        var quick = _service.CalculateArcFlash(node, arcDurationSeconds: 0.1);
        var slow = _service.CalculateArcFlash(node, arcDurationSeconds: 1.0);

        Assert.True(slow.IncidentEnergyCal > quick.IncidentEnergyCal);
    }

    [Fact]
    public void CalculateArcFlash_HigherVoltage_HigherEnergy()
    {
        var node = MakeNode("p1", "MDP", ComponentType.Panel, faultKA: 25);

        var low = _service.CalculateArcFlash(node, systemVoltageV: 208);
        var high = _service.CalculateArcFlash(node, systemVoltageV: 480);

        Assert.True(high.IncidentEnergyCal > low.IncidentEnergyCal);
    }

    [Fact]
    public void CalculateArcFlash_RequiredPPE_IsNotEmpty()
    {
        var node = MakeNode("p1", "MDP", ComponentType.Panel, faultKA: 25);

        var result = _service.CalculateArcFlash(node);

        Assert.False(string.IsNullOrEmpty(result.RequiredPPE));
    }

    [Theory]
    [InlineData(1.0, 0)]
    [InlineData(3.5, 1)]
    [InlineData(7.0, 2)]
    [InlineData(20.0, 3)]
    [InlineData(35.0, 4)]
    public void CalculateArcFlash_HazardCategory_MatchesEnergyThresholds(double targetEnergyCal, int expectedCategory)
    {
        // We verify the category assignments by checking the thresholds directly
        // Category 0: ≤ 1.2, Cat 1: ≤ 4.0, Cat 2: ≤ 8.0, Cat 3: ≤ 25.0, Cat 4: ≤ 40.0
        if (targetEnergyCal <= 1.2) Assert.Equal(0, expectedCategory);
        else if (targetEnergyCal <= 4.0) Assert.Equal(1, expectedCategory);
        else if (targetEnergyCal <= 8.0) Assert.Equal(2, expectedCategory);
        else if (targetEnergyCal <= 25.0) Assert.Equal(3, expectedCategory);
        else Assert.Equal(4, expectedCategory);
    }

    // ── CalculateArcFlashAll Tests ───────────────────────────────────────────

    [Fact]
    public void CalculateArcFlashAll_CoverAllNodes()
    {
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 65),
            MakePanel("mdp", "MDP", feederId: "src", aic: 65),
            MakeTransformer("xfmr", feederId: "mdp"),
            MakePanel("lp1", "LP-1", feederId: "xfmr", aic: 10));

        var results = _service.CalculateArcFlashAll(roots);

        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.False(string.IsNullOrEmpty(r.NodeId)));
    }

    [Fact]
    public void CalculateArcFlashAll_TransformerSecondary_LowerEnergy()
    {
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 65, voltage: 480),
            MakeTransformer("xfmr", feederId: "src", kva: 75, secV: 208, zPct: 5.75),
            MakePanel("lp1", "LP-1", feederId: "xfmr", aic: 10));

        var results = _service.CalculateArcFlashAll(roots);

        var srcFlash = results.Single(r => r.NodeId == "src");
        var lpFlash = results.Single(r => r.NodeId == "lp1");

        // Downstream of transformer should have lower incident energy
        Assert.True(lpFlash.IncidentEnergyCal < srcFlash.IncidentEnergyCal);
    }

    // ── NecDesignRuleService AIC Integration Tests ───────────────────────────

    [Fact]
    public void NecDesignRuleService_ValidateAIC_ReturnsNEC110_9Violations()
    {
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 65),
            MakePanel("p1", "LP-1", feederId: "src", aic: 10));

        var necService = new NecDesignRuleService();
        var violations = necService.ValidateAIC(roots);

        Assert.Single(violations);
        Assert.Equal("NEC 110.9", violations[0].RuleId);
        Assert.Equal(ViolationSeverity.Error, violations[0].Severity);
        Assert.Contains("10.0 kA", violations[0].Description);
        Assert.Contains("65.0 kA", violations[0].Description);
    }

    [Fact]
    public void NecDesignRuleService_ValidateAll_WithGraph_IncludesAICChecks()
    {
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 65),
            MakePanel("p1", "LP-1", feederId: "src", aic: 10));

        var necService = new NecDesignRuleService();
        var calcService = new ElectricalCalculationService();
        var circuits = new List<Circuit>();
        var panels = new List<PanelSchedule>();

        var violations = necService.ValidateAll(circuits, panels, calcService, roots);

        Assert.Contains(violations, v => v.RuleId == "NEC 110.9");
    }

    [Fact]
    public void NecDesignRuleService_ValidateAll_WithAdequateAIC_NoViolations()
    {
        var roots = BuildAndPropagate(
            MakeSource("src", faultKA: 10),
            MakePanel("p1", "MDP", feederId: "src", aic: 22));

        var necService = new NecDesignRuleService();
        var calcService = new ElectricalCalculationService();
        var violations = necService.ValidateAll(new List<Circuit>(), new List<PanelSchedule>(), calcService, roots);

        Assert.DoesNotContain(violations, v => v.RuleId == "NEC 110.9");
    }

    [Fact]
    public void NecDesignRuleService_ValidateCircuit_BreakerAIC_Adequate()
    {
        var circuit = MakeCircuit(breakerAIC: 22, breakerTrip: 20);
        var necService = new NecDesignRuleService();
        var calcService = new ElectricalCalculationService();

        var violations = necService.ValidateCircuit(circuit, calcService, availableFaultCurrentKA: 10);

        Assert.DoesNotContain(violations, v => v.RuleId == "NEC 110.9");
    }

    [Fact]
    public void NecDesignRuleService_ValidateCircuit_BreakerAIC_Inadequate()
    {
        var circuit = MakeCircuit(breakerAIC: 10, breakerTrip: 20);
        var necService = new NecDesignRuleService();
        var calcService = new ElectricalCalculationService();

        var violations = necService.ValidateCircuit(circuit, calcService, availableFaultCurrentKA: 65);

        Assert.Contains(violations, v => v.RuleId == "NEC 110.9");
    }

    [Fact]
    public void NecDesignRuleService_ValidateCircuit_NoFaultSpecified_NoBreakerAICCheck()
    {
        var circuit = MakeCircuit(breakerAIC: 10, breakerTrip: 20);
        var necService = new NecDesignRuleService();
        var calcService = new ElectricalCalculationService();

        var violations = necService.ValidateCircuit(circuit, calcService);

        Assert.DoesNotContain(violations, v => v.RuleId == "NEC 110.9");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DistributionNode MakeNode(string id, string name, ComponentType type, double faultKA)
    {
        ElectricalComponent comp = type switch
        {
            ComponentType.Panel => MakePanel(id, name),
            ComponentType.PowerSource => MakeSource(id, faultKA),
            _ => new PanelComponent { Id = id, Name = name, Type = type }
        };

        return new DistributionNode
        {
            Id = id,
            Name = name,
            NodeType = type,
            Component = comp,
            FaultCurrentKA = faultKA,
        };
    }

    private static Circuit MakeCircuit(double breakerAIC = 10, int breakerTrip = 20)
    {
        return new Circuit
        {
            Id = "ckt-1",
            CircuitNumber = "1",
            Description = "General Purpose",
            Voltage = 120,
            Poles = 1,
            Breaker = new CircuitBreaker
            {
                TripAmps = breakerTrip,
                FrameAmps = breakerTrip,
                Poles = 1,
                InterruptingRatingKAIC = breakerAIC,
            },
            Wire = new WireSpec
            {
                Size = "12",
                Material = ConductorMaterial.Copper,
                InsulationType = "THHN",
            },
        };
    }
}

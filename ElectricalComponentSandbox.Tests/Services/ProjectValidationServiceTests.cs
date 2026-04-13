using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ProjectValidationServiceTests
{
    private static ProjectValidationService CreateService()
    {
        return new ProjectValidationService(
            new ElectricalCalculationService(),
            new NecDesignRuleService(),
            new ShortCircuitService(),
            new DistributionGraphService());
    }

    // ── Construction & empty input ───────────────────────────────────────────

    [Fact]
    public void EmptyProject_ReturnsValidReport()
    {
        var svc = CreateService();
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            ProjectName = "Empty Project",
        });
        Assert.True(report.IsValid);
        Assert.Equal(0, report.ErrorCount);
        Assert.Equal("Empty Project", report.ProjectName);
    }

    [Fact]
    public void Report_GeneratedUtcIsRecent()
    {
        var svc = CreateService();
        var before = DateTime.UtcNow;
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput());
        Assert.True(report.GeneratedUtc >= before);
        Assert.True(report.GeneratedUtc <= DateTime.UtcNow.AddSeconds(2));
    }

    // ── Voltage drop checks ─────────────────────────────────────────────────

    [Fact]
    public void Circuit_WithExcessiveVD_ReturnsWarning()
    {
        var svc = CreateService();
        var circuit = MakeCircuit("CKT-1", 20, 120, "12", 200); // long run
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            ProjectName = "VD Test",
            Circuits = new List<Circuit> { circuit },
        });
        var vdFindings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.VoltageDrop)
            .ToList();
        // Long run on #12 wire should trigger VD warning/error
        Assert.NotEmpty(vdFindings);
    }

    [Fact]
    public void Circuit_WithShortRun_NoVDFinding()
    {
        var svc = CreateService();
        var circuit = MakeCircuit("CKT-2", 20, 120, "10", 10); // short run
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            ProjectName = "Short Run",
            Circuits = new List<Circuit> { circuit },
        });
        var vdFindings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.VoltageDrop)
            .ToList();
        Assert.Empty(vdFindings);
    }

    [Fact]
    public void Circuit_WithZeroLength_SkipsVDCheck()
    {
        var svc = CreateService();
        var circuit = MakeCircuit("CKT-3", 20, 120, "12", 0);
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Circuits = new List<Circuit> { circuit },
        });
        var vdFindings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.VoltageDrop)
            .ToList();
        Assert.Empty(vdFindings);
    }

    [Fact]
    public void VDOverFivePercent_IsError()
    {
        var svc = CreateService();
        // #14 wire, 400ft, 15A @ 120V → huge VD
        var circuit = MakeCircuit("CKT-4", 15, 120, "14", 400);
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Circuits = new List<Circuit> { circuit },
        });
        var errors = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.VoltageDrop
                        && f.Severity == ProjectValidationService.FindingSeverity.Error)
            .ToList();
        Assert.NotEmpty(errors);
    }

    // ── Phase balance ────────────────────────────────────────────────────────

    [Fact]
    public void BalancedPanel_NoPhaseBalanceFinding()
    {
        var svc = CreateService();
        var schedule = MakeThreePhaseSchedule("P1", "Panel-1",
            new[] { 5000.0, 5000.0, 5100.0 });
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Schedules = new List<PanelSchedule> { schedule },
        });
        var pbFindings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.PhaseBalance)
            .ToList();
        Assert.Empty(pbFindings);
    }

    [Fact]
    public void ImbalancedPanel_ReturnsPhaseBalanceWarning()
    {
        var svc = CreateService();
        // Big imbalance: A=10000, B=2000, C=5000 → 80% imbalance
        var schedule = MakeThreePhaseSchedule("P2", "Panel-2",
            new[] { 10000.0, 2000.0, 5000.0 });
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Schedules = new List<PanelSchedule> { schedule },
        });
        var pbFindings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.PhaseBalance)
            .ToList();
        Assert.NotEmpty(pbFindings);
    }

    [Fact]
    public void SevereImbalance_IsError()
    {
        var svc = CreateService();
        // A=10000, B=1000, C=5000 → 90% imbalance
        var schedule = MakeThreePhaseSchedule("P3", "Panel-3",
            new[] { 10000.0, 1000.0, 5000.0 });
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Schedules = new List<PanelSchedule> { schedule },
        });
        var errors = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.PhaseBalance
                        && f.Severity == ProjectValidationService.FindingSeverity.Error)
            .ToList();
        Assert.NotEmpty(errors);
    }

    // ── NEC design rules ─────────────────────────────────────────────────────

    [Fact]
    public void OversizedBreaker_ForSmallWire_IsNecViolation()
    {
        var svc = CreateService();
        // #14 wire with a 30A breaker → NEC 240.4(D) violation
        var circuit = MakeCircuit("CKT-OV", 30, 120, "14", 50);
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Circuits = new List<Circuit> { circuit },
        });
        var necFindings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.NecCompliance)
            .ToList();
        Assert.NotEmpty(necFindings);
    }

    [Fact]
    public void ValidCircuit_NoNecViolation()
    {
        var svc = CreateService();
        // #12 wire with 20A breaker → valid
        var circuit = MakeCircuit("CKT-OK", 20, 120, "12", 50);
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Circuits = new List<Circuit> { circuit },
        });
        var necErrors = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.NecCompliance
                        && f.Severity == ProjectValidationService.FindingSeverity.Error)
            .ToList();
        Assert.Empty(necErrors);
    }

    // ── Distribution topology ────────────────────────────────────────────────

    [Fact]
    public void ComponentsWithCycle_ReportsTopologyError()
    {
        var svc = CreateService();
        // A feeds B, B feeds A → cycle
        var components = new List<ElectricalComponent>
        {
            new PanelComponent { Id = "A", Name = "Panel-A", FeederId = "B", Subtype = PanelSubtype.Panelboard },
            new PanelComponent { Id = "B", Name = "Panel-B", FeederId = "A", Subtype = PanelSubtype.Panelboard },
        };
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Components = components,
        });
        var cycleFindings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.DistributionTopology)
            .ToList();
        Assert.NotEmpty(cycleFindings);
        Assert.All(cycleFindings, f =>
            Assert.Equal(ProjectValidationService.FindingSeverity.Error, f.Severity));
    }

    [Fact]
    public void NoCycles_NoTopologyFinding()
    {
        var svc = CreateService();
        var components = new List<ElectricalComponent>
        {
            new PowerSourceComponent { Id = "SRC", Name = "Utility", Voltage = 480, KVA = 500, AvailableFaultCurrentKA = 22 },
            new PanelComponent { Id = "MDP", Name = "MDP", FeederId = "SRC", Subtype = PanelSubtype.Switchboard, AICRatingKA = 65 },
        };
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Components = components,
        });
        var cycleFindings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.DistributionTopology)
            .ToList();
        Assert.Empty(cycleFindings);
    }

    // ── Short circuit / AIC ──────────────────────────────────────────────────

    [Fact]
    public void AICAdequate_NoShortCircuitFinding()
    {
        var svc = CreateService();
        var components = new List<ElectricalComponent>
        {
            new PowerSourceComponent { Id = "SRC", Name = "Utility", Voltage = 480, KVA = 500, AvailableFaultCurrentKA = 10 },
            new PanelComponent { Id = "MDP", Name = "MDP", FeederId = "SRC", Subtype = PanelSubtype.Switchboard, AICRatingKA = 65 },
        };
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Components = components,
        });
        var scFindings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.ShortCircuit)
            .ToList();
        Assert.Empty(scFindings);
    }

    [Fact]
    public void AICInadequate_ReportsShortCircuitError()
    {
        var svc = CreateService();
        var components = new List<ElectricalComponent>
        {
            new PowerSourceComponent { Id = "SRC", Name = "Utility", Voltage = 480, KVA = 5000, AvailableFaultCurrentKA = 100 },
            new PanelComponent { Id = "MDP", Name = "MDP", FeederId = "SRC", Subtype = PanelSubtype.Switchboard, AICRatingKA = 10 },
        };
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Components = components,
        });
        var scErrors = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.ShortCircuit
                        && f.Severity == ProjectValidationService.FindingSeverity.Error)
            .ToList();
        Assert.NotEmpty(scErrors);
    }

    // ── Report aggregation ───────────────────────────────────────────────────

    [Fact]
    public void ErrorCount_MatchesNumberOfErrors()
    {
        var svc = CreateService();
        // #14 with 30A breaker + 400ft → multiple errors
        var circuit = MakeCircuit("CKT-AGG", 30, 120, "14", 400);
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Circuits = new List<Circuit> { circuit },
        });
        int expectedErrors = report.Findings.Count(f => f.Severity == ProjectValidationService.FindingSeverity.Error);
        Assert.Equal(expectedErrors, report.ErrorCount);
    }

    [Fact]
    public void WarningCount_MatchesNumberOfWarnings()
    {
        var svc = CreateService();
        var circuit = MakeCircuit("CKT-W", 20, 120, "12", 200);
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Circuits = new List<Circuit> { circuit },
        });
        int expectedWarnings = report.Findings.Count(f => f.Severity == ProjectValidationService.FindingSeverity.Warning);
        Assert.Equal(expectedWarnings, report.WarningCount);
    }

    [Fact]
    public void FindingsByCategory_SumsCorrectly()
    {
        var svc = CreateService();
        var circuit = MakeCircuit("CKT-BC", 30, 120, "14", 400);
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Circuits = new List<Circuit> { circuit },
        });
        int totalByCategory = report.FindingsByCategory.Values.Sum();
        Assert.Equal(report.Findings.Count, totalByCategory);
    }

    [Fact]
    public void IsValid_FalseWhenErrors()
    {
        var svc = CreateService();
        var circuit = MakeCircuit("CKT-INV", 30, 120, "14", 400);
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Circuits = new List<Circuit> { circuit },
        });
        Assert.False(report.IsValid);
    }

    [Fact]
    public void TotalChecksRun_IsPositive()
    {
        var svc = CreateService();
        var circuit = MakeCircuit("CKT-TC", 20, 120, "12", 50);
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Circuits = new List<Circuit> { circuit },
        });
        Assert.True(report.TotalChecksRun > 0);
    }

    [Fact]
    public void FindingIds_AreUnique()
    {
        var svc = CreateService();
        var circuit = MakeCircuit("CKT-UID", 30, 120, "14", 400);
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Circuits = new List<Circuit> { circuit },
        });
        var ids = report.Findings.Select(f => f.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void CustomVDThreshold_Respected()
    {
        var svc = CreateService();
        var circuit = MakeCircuit("CKT-TH", 20, 120, "12", 100);
        // Very tight threshold → should trigger
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Circuits = new List<Circuit> { circuit },
            MaxVoltageDropPercent = 0.5,
        });
        var vdFindings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.VoltageDrop)
            .ToList();
        Assert.NotEmpty(vdFindings);
    }

    [Fact]
    public void CustomImbalanceThreshold_Respected()
    {
        var svc = CreateService();
        // Slightly imbalanced: A=5000, B=4000, C=4500 → 20% imbalance
        var schedule = MakeThreePhaseSchedule("P-TH", "Panel-TH",
            new[] { 5000.0, 4000.0, 4500.0 });
        var report = svc.Validate(new ProjectValidationService.ProjectValidationInput
        {
            Schedules = new List<PanelSchedule> { schedule },
            MaxPhaseImbalancePercent = 5.0, // tight threshold
        });
        var pbFindings = report.Findings
            .Where(f => f.Category == ProjectValidationService.FindingCategory.PhaseBalance)
            .ToList();
        Assert.NotEmpty(pbFindings);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Circuit MakeCircuit(string id, int breakerAmps, int voltage, string wireSize, double lengthFt)
    {
        return new Circuit
        {
            CircuitNumber = id,
            Breaker = new CircuitBreaker { TripAmps = breakerAmps, Poles = 1 },
            Voltage = voltage,
            Poles = 1,
            WireLengthFeet = lengthFt,
            ConnectedLoadVA = breakerAmps * voltage * 0.8,
            Wire = new WireSpec
            {
                Size = wireSize,
                Conductors = 2,
                GroundSize = "12",
                InsulationType = "THWN-2",
                Material = ConductorMaterial.Copper,
            },
            SlotType = CircuitSlotType.Circuit,
            LoadClassification = LoadClassification.Power,
        };
    }

    private static PanelSchedule MakeThreePhaseSchedule(string panelId, string panelName, double[] phaseLoads)
    {
        // Build circuits to create desired phase loading
        var circuits = new List<Circuit>();
        // Phase A
        circuits.Add(new Circuit
        {
            CircuitNumber = "1",
            Voltage = 277,
            Poles = 1,
            Phase = "A",
            Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
            ConnectedLoadVA = phaseLoads[0],
            DemandFactor = 1.0,
            Wire = new WireSpec { Size = "12", Conductors = 2, GroundSize = "12", Material = ConductorMaterial.Copper },
            SlotType = CircuitSlotType.Circuit,
            LoadClassification = LoadClassification.Lighting,
        });
        // Phase B
        circuits.Add(new Circuit
        {
            CircuitNumber = "2",
            Voltage = 277,
            Poles = 1,
            Phase = "B",
            Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
            ConnectedLoadVA = phaseLoads[1],
            DemandFactor = 1.0,
            Wire = new WireSpec { Size = "12", Conductors = 2, GroundSize = "12", Material = ConductorMaterial.Copper },
            SlotType = CircuitSlotType.Circuit,
            LoadClassification = LoadClassification.Lighting,
        });
        // Phase C
        circuits.Add(new Circuit
        {
            CircuitNumber = "3",
            Voltage = 277,
            Poles = 1,
            Phase = "C",
            Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
            ConnectedLoadVA = phaseLoads[2],
            DemandFactor = 1.0,
            Wire = new WireSpec { Size = "12", Conductors = 2, GroundSize = "12", Material = ConductorMaterial.Copper },
            SlotType = CircuitSlotType.Circuit,
            LoadClassification = LoadClassification.Lighting,
        });

        return new PanelSchedule
        {
            PanelId = panelId,
            PanelName = panelName,
            VoltageConfig = PanelVoltageConfig.V277_480_3Ph,
            MainBreakerAmps = 100,
            BusAmps = 100,
            Circuits = circuits,
        };
    }
}

using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using static ElectricalComponentSandbox.Services.NecComplianceReportService;

namespace ElectricalComponentSandbox.Tests.Services;

public class NecComplianceReportServiceTests
{
    // ── Empty Input ──────────────────────────────────────────────────────────

    [Fact]
    public void EmptyInput_Pass_NoChecks()
    {
        var report = NecComplianceReportService.GenerateReport(new ComplianceInput());

        Assert.Equal(ComplianceStatus.Pass, report.OverallStatus);
        Assert.Equal(0, report.TotalChecksRun);
        Assert.Contains(report.SummaryNotes, n => n.Contains("No items"));
    }

    // ── Design Rule Violations Propagated ────────────────────────────────────

    [Fact]
    public void CircuitWithUndersizedBreaker_ReportsError()
    {
        // A circuit with 20A load on a 15A breaker should produce a violation
        var circuit = new Circuit
        {
            Id = "C1",
            CircuitNumber = "1",
            Description = "Test",
            Phase = "A",
            Poles = 1,
            ConnectedLoadVA = 3600, // 30A @ 120V > 15A breaker
            DemandFactor = 1.0,
            SlotType = CircuitSlotType.Circuit,
            Breaker = new CircuitBreaker { TripAmps = 15 },
            Wire = new WireSpec { Size = "14", Material = ConductorMaterial.Copper },
        };

        var panel = new PanelSchedule
        {
            PanelId = "P1",
            PanelName = "Test Panel",
            BusAmps = 100,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit> { circuit },
        };

        var input = new ComplianceInput
        {
            ProjectName = "Test",
            Circuits = new List<Circuit> { circuit },
            PanelSchedules = new List<PanelSchedule> { panel },
        };

        var report = NecComplianceReportService.GenerateReport(input);

        Assert.True(report.TotalChecksRun > 0);
        Assert.True(report.TotalErrors > 0 || report.TotalWarnings > 0,
            "Overloaded circuit should generate violations");
    }

    // ── Conduit Fill Violation ───────────────────────────────────────────────

    [Fact]
    public void OverfilledConduit_ReportsError()
    {
        var input = new ComplianceInput
        {
            ConduitFillInputs = new List<ConduitFillInput>
            {
                new()
                {
                    RunId = "CR-001",
                    TradeSize = "1/2",
                    Material = ConduitMaterialType.EMT,
                    WireSizes = new List<string> { "6", "6", "6", "6", "6", "6" }, // way overfilled
                },
            },
        };

        var report = NecComplianceReportService.GenerateReport(input);

        Assert.Equal(ComplianceStatus.Fail, report.OverallStatus);
        Assert.True(report.TotalErrors > 0);
        Assert.Contains(report.Sections, s => s.ArticleNumber.Contains("344"));
    }

    [Fact]
    public void ProperlyFilledConduit_NoProblem()
    {
        var input = new ComplianceInput
        {
            ConduitFillInputs = new List<ConduitFillInput>
            {
                new()
                {
                    RunId = "CR-002",
                    TradeSize = "2",
                    Material = ConduitMaterialType.EMT,
                    WireSizes = new List<string> { "12", "12", "12" },
                },
            },
        };

        var report = NecComplianceReportService.GenerateReport(input);

        Assert.Equal(0, report.TotalErrors);
    }

    // ── Feeder Voltage Drop ──────────────────────────────────────────────────

    [Fact]
    public void ExcessiveVD_ReportsWarning()
    {
        var input = new ComplianceInput
        {
            FeederSegments = new List<FeederSegment>
            {
                new()
                {
                    FromNodeId = "MAIN",
                    ToNodeId = "MDP",
                    WireSize = "10",
                    Material = ConductorMaterial.Copper,
                    LengthFeet = 800,
                    Voltage = 208,
                    Poles = 1,
                    LoadAmps = 80,
                },
            },
        };

        var report = NecComplianceReportService.GenerateReport(input);

        Assert.True(report.TotalWarnings > 0);
        Assert.Contains(report.Sections, s => s.ArticleNumber.Contains("215"));
    }

    [Fact]
    public void ShortFeeder_NoWarning()
    {
        var input = new ComplianceInput
        {
            FeederSegments = new List<FeederSegment>
            {
                new()
                {
                    FromNodeId = "MAIN",
                    ToNodeId = "MDP",
                    WireSize = "4/0",
                    Material = ConductorMaterial.Copper,
                    LengthFeet = 10,
                    Voltage = 480,
                    Poles = 3,
                    LoadAmps = 100,
                },
            },
        };

        var report = NecComplianceReportService.GenerateReport(input);

        Assert.Equal(0, report.TotalWarnings);
        Assert.Equal(0, report.TotalErrors);
    }

    // ── Grounding Violation ──────────────────────────────────────────────────

    [Fact]
    public void UndersizedGround_ReportsError()
    {
        var circuit = new Circuit
        {
            Id = "C1",
            CircuitNumber = "1",
            Phase = "A",
            Poles = 1,
            ConnectedLoadVA = 1200,
            DemandFactor = 1.0,
            SlotType = CircuitSlotType.Circuit,
            Breaker = new CircuitBreaker { TripAmps = 60 },
            Wire = new WireSpec
            {
                Size = "6",
                Material = ConductorMaterial.Copper,
                GroundSize = "14", // undersized for 60A OCPD
            },
        };

        var input = new ComplianceInput
        {
            Circuits = new List<Circuit> { circuit },
        };

        var report = NecComplianceReportService.GenerateReport(input);

        Assert.True(report.TotalErrors > 0);
        Assert.Contains(report.Sections, s => s.ArticleNumber.Contains("250"));
    }

    // ── Report Structure ─────────────────────────────────────────────────────

    [Fact]
    public void Report_HasProjectName()
    {
        var input = new ComplianceInput { ProjectName = "My Project" };
        var report = NecComplianceReportService.GenerateReport(input);
        Assert.Equal("My Project", report.ProjectName);
    }

    [Fact]
    public void Report_HasTimestamp()
    {
        var before = DateTime.UtcNow;
        var report = NecComplianceReportService.GenerateReport(new ComplianceInput());
        Assert.True(report.GeneratedUtc >= before);
    }

    [Fact]
    public void SectionsOrganizedByArticle()
    {
        // Combine multiple violation types
        var input = new ComplianceInput
        {
            ConduitFillInputs = new List<ConduitFillInput>
            {
                new()
                {
                    RunId = "OVF", TradeSize = "1/2", Material = ConduitMaterialType.EMT,
                    WireSizes = new List<string> { "6", "6", "6", "6", "6", "6" },
                },
            },
            FeederSegments = new List<FeederSegment>
            {
                new()
                {
                    FromNodeId = "A", ToNodeId = "B",
                    WireSize = "10", Material = ConductorMaterial.Copper,
                    LengthFeet = 800, Voltage = 208, Poles = 1, LoadAmps = 80,
                },
            },
        };

        var report = NecComplianceReportService.GenerateReport(input);

        Assert.True(report.Sections.Count >= 2, "Should have at least 2 sections");
        // Sections should be ordered
        for (int i = 1; i < report.Sections.Count; i++)
        {
            Assert.True(
                string.Compare(report.Sections[i - 1].ArticleNumber, report.Sections[i].ArticleNumber, StringComparison.Ordinal) <= 0,
                "Sections should be ordered by article number");
        }
    }

    // ── Overall Status Logic ─────────────────────────────────────────────────

    [Fact]
    public void ErrorsProduce_Fail()
    {
        var input = new ComplianceInput
        {
            ConduitFillInputs = new List<ConduitFillInput>
            {
                new()
                {
                    RunId = "X", TradeSize = "1/2", Material = ConduitMaterialType.EMT,
                    WireSizes = new List<string> { "6", "6", "6", "6", "6", "6" },
                },
            },
        };

        var report = NecComplianceReportService.GenerateReport(input);
        Assert.Equal(ComplianceStatus.Fail, report.OverallStatus);
    }

    [Fact]
    public void OnlyWarnings_PassWithWarnings()
    {
        var input = new ComplianceInput
        {
            FeederSegments = new List<FeederSegment>
            {
                new()
                {
                    FromNodeId = "A", ToNodeId = "B",
                    WireSize = "10", Material = ConductorMaterial.Copper,
                    LengthFeet = 800, Voltage = 208, Poles = 1, LoadAmps = 80,
                },
            },
        };

        var report = NecComplianceReportService.GenerateReport(input);
        Assert.Equal(ComplianceStatus.PassWithWarnings, report.OverallStatus);
    }

    [Fact]
    public void CleanDesign_Pass()
    {
        var input = new ComplianceInput
        {
            ConduitFillInputs = new List<ConduitFillInput>
            {
                new()
                {
                    RunId = "OK", TradeSize = "2", Material = ConduitMaterialType.EMT,
                    WireSizes = new List<string> { "12", "12", "12" },
                },
            },
            FeederSegments = new List<FeederSegment>
            {
                new()
                {
                    FromNodeId = "A", ToNodeId = "B",
                    WireSize = "4/0", Material = ConductorMaterial.Copper,
                    LengthFeet = 10, Voltage = 480, Poles = 3, LoadAmps = 100,
                },
            },
        };

        var report = NecComplianceReportService.GenerateReport(input);
        Assert.Equal(ComplianceStatus.Pass, report.OverallStatus);
        Assert.Contains(report.SummaryNotes, n => n.Contains("comply"));
    }

    // ── Real-World ───────────────────────────────────────────────────────────

    [Fact]
    public void RealWorld_CommercialOffice_MixedResults()
    {
        var circuits = new List<Circuit>
        {
            new()
            {
                Id = "1", CircuitNumber = "1", Description = "Receptacles",
                Phase = "A", Poles = 1, ConnectedLoadVA = 1800, DemandFactor = 1.0,
                SlotType = CircuitSlotType.Circuit,
                Breaker = new CircuitBreaker { TripAmps = 20 },
                Wire = new WireSpec { Size = "12", Material = ConductorMaterial.Copper },
            },
            new()
            {
                Id = "2", CircuitNumber = "2", Description = "Lighting",
                Phase = "B", Poles = 1, ConnectedLoadVA = 1200, DemandFactor = 1.0,
                SlotType = CircuitSlotType.Circuit,
                Breaker = new CircuitBreaker { TripAmps = 20 },
                Wire = new WireSpec { Size = "12", Material = ConductorMaterial.Copper },
            },
        };

        var panels = new List<PanelSchedule>
        {
            new()
            {
                PanelId = "LP1", PanelName = "Lighting Panel 1",
                BusAmps = 200, VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
                Circuits = circuits,
            },
        };

        var input = new ComplianceInput
        {
            ProjectName = "Commercial Office",
            Circuits = circuits,
            PanelSchedules = panels,
            ConduitFillInputs = new List<ConduitFillInput>
            {
                new()
                {
                    RunId = "CR-001", TradeSize = "3/4", Material = ConduitMaterialType.EMT,
                    WireSizes = new List<string> { "12", "12", "12" },
                },
            },
        };

        var report = NecComplianceReportService.GenerateReport(input);

        Assert.Equal("Commercial Office", report.ProjectName);
        Assert.True(report.TotalChecksRun > 0);
    }
}

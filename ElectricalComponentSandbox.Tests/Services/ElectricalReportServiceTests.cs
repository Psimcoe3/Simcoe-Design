using System.IO;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Export;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ElectricalReportServiceTests
{
    private readonly ElectricalReportService _sut = new();
    private readonly ElectricalCalculationService _calc = new();

    private static List<Circuit> CreateTestCircuits()
    {
        return new List<Circuit>
        {
            new()
            {
                CircuitNumber = "1", Description = "Lighting", PanelId = "p1",
                Phase = "A", Voltage = 120, ConnectedLoadVA = 1800, DemandFactor = 1.0,
                WireLengthFeet = 75,
                Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                Wire = new WireSpec { Size = "12", Material = ConductorMaterial.Copper }
            },
            new()
            {
                CircuitNumber = "2", Description = "Receptacles", PanelId = "p1",
                Phase = "B", Voltage = 120, ConnectedLoadVA = 2400, DemandFactor = 0.8,
                WireLengthFeet = 100,
                Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                Wire = new WireSpec { Size = "12", Material = ConductorMaterial.Copper }
            }
        };
    }

    private static List<PanelComponent> CreateTestPanels()
    {
        return new List<PanelComponent>
        {
            new() { Name = "Panel A" }
        };
    }

    [Fact]
    public void GenerateVoltageDropReport_ContainsHeader()
    {
        var report = _sut.GenerateVoltageDropReport(
            CreateTestCircuits(), CreateTestPanels(), _calc);
        Assert.Contains("VOLTAGE DROP", report);
    }

    [Fact]
    public void GenerateVoltageDropReport_ContainsCircuitData()
    {
        var report = _sut.GenerateVoltageDropReport(
            CreateTestCircuits(), CreateTestPanels(), _calc);
        Assert.Contains("Lighting", report);
        Assert.Contains("Receptacles", report);
    }

    [Fact]
    public void GenerateWireSizeReport_ContainsRecommendations()
    {
        var report = _sut.GenerateWireSizeReport(CreateTestCircuits(), _calc);
        Assert.Contains("WIRE SIZE", report);
        Assert.Contains("Recommended", report);
    }

    [Fact]
    public void GeneratePanelLoadReport_ContainsPhaseData()
    {
        var schedules = new List<PanelSchedule>
        {
            new()
            {
                PanelId = "p1", PanelName = "Panel A", BusAmps = 200,
                Circuits = CreateTestCircuits()
            }
        };
        var report = _sut.GeneratePanelLoadReport(schedules, _calc);
        Assert.Contains("Phase A", report);
        Assert.Contains("Phase B", report);
    }

    [Fact]
    public void ExportVoltageDropCsv_HasCsvHeaders()
    {
        var csv = _sut.ExportVoltageDropCsv(
            CreateTestCircuits(), CreateTestPanels(), _calc);
        Assert.Contains("Circuit", csv);
        Assert.Contains(",", csv);
    }

    [Fact]
    public void ExportPanelLoadCsv_HasCsvHeaders()
    {
        var schedules = new List<PanelSchedule>
        {
            new()
            {
                PanelId = "p1", PanelName = "Panel A", BusAmps = 200,
                Circuits = CreateTestCircuits()
            }
        };
        var csv = _sut.ExportPanelLoadCsv(schedules, _calc);
        Assert.Contains("Panel", csv);
    }

    [Fact]
    public void SaveReport_WritesFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_report_{Guid.NewGuid()}.txt");
        try
        {
            _sut.SaveReport("Test content", tempFile);
            Assert.True(File.Exists(tempFile));
            Assert.Equal("Test content", File.ReadAllText(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void GenerateProtectionProgramReport_ContainsHeaderAndOverview()
    {
        var report = _sut.GenerateProtectionProgramReport(CreateProtectionProgramReport());

        Assert.Contains("PROTECTION PROGRAM SUMMARY", report);
        Assert.Contains("Readiness Score:", report);
        Assert.Contains("Relay Critical Findings:", report);
    }

    [Fact]
    public void GenerateProtectionProgramReport_ContainsActionsAndUpgrades()
    {
        var report = _sut.GenerateProtectionProgramReport(CreateProtectionProgramReport());

        Assert.Contains("PRIORITY ACTIONS", report);
        Assert.Contains("[Critical] Interrupting duty", report);
        Assert.Contains("Replace main breaker", report);
    }

    [Fact]
    public void ExportProtectionProgramCsv_HasSummaryAndSectionHeaders()
    {
        var csv = _sut.ExportProtectionProgramCsv(CreateProtectionProgramReport());

        Assert.Contains("Metric,Value", csv);
        Assert.Contains("Action Priority,Category,Description", csv);
        Assert.Contains("Upgrade Name,Type,Priority Score,Benefit-Cost Ratio,Reason", csv);
    }

    [Fact]
    public void ExportProtectionProgramCsv_ContainsRecommendationRows()
    {
        var csv = _sut.ExportProtectionProgramCsv(CreateProtectionProgramReport());

        Assert.Contains("Readiness Score,42", csv);
        Assert.Contains("Critical,Interrupting duty,Replace or re-rate equipment at 1 critical location(s).", csv);
        Assert.Contains("Replace main breaker,BreakerReplacement,92.00,0.0077", csv);
    }

    private static ProtectionProgramService.ProgramReport CreateProtectionProgramReport()
    {
        return new ProtectionProgramService.ProgramReport
        {
            ReadinessScore = 42,
            RelayCriticalCount = 3,
            CoordinationViolationCount = 2,
            DutyViolationCount = 1,
            AverageArcFlashReductionPercent = 27.5,
            Actions =
            {
                new ProtectionProgramService.ProgramAction
                {
                    Priority = ProtectionProgramService.ProgramPriority.Critical,
                    Category = "Interrupting duty",
                    Description = "Replace or re-rate equipment at 1 critical location(s).",
                },
                new ProtectionProgramService.ProgramAction
                {
                    Priority = ProtectionProgramService.ProgramPriority.Medium,
                    Category = "Arc flash",
                    Description = "Apply maintenance switch at BUS-1 to reduce incident energy by 27.5%.",
                },
            },
            RecommendedUpgrades =
            {
                new ProtectionUpgradePlannerService.UpgradeRecommendation
                {
                    Name = "Replace main breaker",
                    Type = ProtectionUpgradePlannerService.UpgradeType.BreakerReplacement,
                    PriorityScore = 92,
                    BenefitCostRatio = 0.0077,
                    Reason = "reduces interrupting-duty risk by 3 level(s)",
                },
            },
        };
    }
}

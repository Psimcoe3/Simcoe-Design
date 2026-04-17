using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ProtectionProgramServiceTests
{
    [Fact]
    public void BuildReport_AggregatesFindingsIntoReadinessScore()
    {
        var report = ProtectionProgramService.BuildReport(
            new[]
            {
                new RelaySettingsAuditService.RelayAuditResult { CriticalCount = 2 },
                new RelaySettingsAuditService.RelayAuditResult { CriticalCount = 1 },
            },
            new[]
            {
                new CoordinationSweepService.SweepSummary
                {
                    Violations =
                    {
                        new CoordinationSweepService.SweepPoint(),
                        new CoordinationSweepService.SweepPoint(),
                    },
                },
            },
            new InterruptingDutyAuditService.DutyAuditSummary { ViolationCount = 2, CriticalCount = 1 },
            new[]
            {
                new ArcFlashMitigationService.MitigationSummary
                {
                    NodeId = "BUS-1",
                    BestScenario = new ArcFlashMitigationService.ScenarioResult { Name = "Maintenance switch", EnergyReductionPercent = 35 },
                },
            },
            new[]
            {
                new ProtectionUpgradePlannerService.UpgradeRecommendation { Name = "Replace breaker", PriorityScore = 90, Reason = "reduces interrupting-duty risk" },
            });

        Assert.Equal(3, report.RelayCriticalCount);
        Assert.Equal(2, report.CoordinationViolationCount);
        Assert.Equal(2, report.DutyViolationCount);
        Assert.Equal(35, report.AverageArcFlashReductionPercent);
        Assert.Equal(31, report.ReadinessScore);
    }

    [Fact]
    public void BuildReport_CreatesPriorityActionsFromFindings()
    {
        var report = ProtectionProgramService.BuildReport(
            new[] { new RelaySettingsAuditService.RelayAuditResult { CriticalCount = 1 } },
            new[] { new CoordinationSweepService.SweepSummary { Violations = { new CoordinationSweepService.SweepPoint() } } },
            new InterruptingDutyAuditService.DutyAuditSummary { ViolationCount = 1, CriticalCount = 1 },
            new[]
            {
                new ArcFlashMitigationService.MitigationSummary
                {
                    NodeId = "BUS-1",
                    BestScenario = new ArcFlashMitigationService.ScenarioResult { Name = "Remote racking", EnergyReductionPercent = 25 },
                },
            },
            new[]
            {
                new ProtectionUpgradePlannerService.UpgradeRecommendation { Name = "Retune relay", PriorityScore = 70, Reason = "eliminates coordination issues" },
            });

        Assert.Contains(report.Actions, action => action.Priority == ProtectionProgramService.ProgramPriority.Critical && action.Category == "Interrupting duty");
        Assert.Contains(report.Actions, action => action.Category == "Arc flash" && action.Description.Contains("Remote racking"));
        Assert.Contains(report.Actions, action => action.Category == "Capital plan" && action.Description.Contains("Retune relay"));
    }

    [Fact]
    public void BuildReport_EmptyInputsReturnHealthyBaseline()
    {
        var report = ProtectionProgramService.BuildReport(
            System.Array.Empty<RelaySettingsAuditService.RelayAuditResult>(),
            System.Array.Empty<CoordinationSweepService.SweepSummary>(),
            new InterruptingDutyAuditService.DutyAuditSummary(),
            System.Array.Empty<ArcFlashMitigationService.MitigationSummary>(),
            System.Array.Empty<ProtectionUpgradePlannerService.UpgradeRecommendation>());

        Assert.Equal(100, report.ReadinessScore);
        Assert.Empty(report.Actions);
        Assert.Empty(report.RecommendedUpgrades);
    }
}
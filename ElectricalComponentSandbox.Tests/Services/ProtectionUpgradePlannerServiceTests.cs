using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ProtectionUpgradePlannerServiceTests
{
    [Fact]
    public void CalculatePriorityScore_WeightsRiskReductions()
    {
        var candidate = new ProtectionUpgradePlannerService.UpgradeCandidate
        {
            RelayCriticalFindingsResolved = 1,
            RelayWarningFindingsResolved = 2,
            CoordinationViolationsResolved = 1,
            DutySeverityLevelsReduced = 2,
            ArcFlashReductionPercent = 50,
        };

        Assert.Equal(114, ProtectionUpgradePlannerService.CalculatePriorityScore(candidate));
    }

    [Fact]
    public void RankCandidates_SortsByPriorityThenBenefitCost()
    {
        var ranked = ProtectionUpgradePlannerService.RankCandidates(new[]
        {
            new ProtectionUpgradePlannerService.UpgradeCandidate { Name = "Low", EstimatedCost = 1000, RelayWarningFindingsResolved = 1 },
            new ProtectionUpgradePlannerService.UpgradeCandidate { Name = "High", EstimatedCost = 5000, DutySeverityLevelsReduced = 2, CoordinationViolationsResolved = 1 },
            new ProtectionUpgradePlannerService.UpgradeCandidate { Name = "Middle", EstimatedCost = 1000, ArcFlashReductionPercent = 40 },
        });

        Assert.Equal(new[] { "High", "Middle", "Low" }, ranked.Select(item => item.Name).ToArray());
    }

    [Fact]
    public void CreateSettingsChangeCandidate_UsesRelayAuditAndSweepFindings()
    {
        var candidate = ProtectionUpgradePlannerService.CreateSettingsChangeCandidate(
            "Retune relay",
            new RelaySettingsAuditService.RelayAuditResult { CriticalCount = 2, WarningCount = 1 },
            new CoordinationSweepService.SweepSummary
            {
                Violations =
                {
                    new CoordinationSweepService.SweepPoint(),
                    new CoordinationSweepService.SweepPoint(),
                },
            },
            estimatedCost: 3500,
            arcFlashReductionPercent: 15);

        Assert.Equal(2, candidate.RelayCriticalFindingsResolved);
        Assert.Equal(2, candidate.CoordinationViolationsResolved);
        Assert.Equal(ProtectionUpgradePlannerService.UpgradeType.SettingsChange, candidate.Type);
    }

    [Fact]
    public void CreateEquipmentUpgradeCandidate_MapsDutySeverityToReductionLevels()
    {
        var candidate = ProtectionUpgradePlannerService.CreateEquipmentUpgradeCandidate(
            "Replace breaker",
            new InterruptingDutyAuditService.DutyExposure { Severity = InterruptingDutyAuditService.DutySeverity.Critical },
            new ArcFlashMitigationService.ScenarioResult { EnergyReductionPercent = 35 },
            estimatedCost: 12000);

        Assert.Equal(3, candidate.DutySeverityLevelsReduced);
        Assert.Equal(35, candidate.ArcFlashReductionPercent);
        Assert.Equal(ProtectionUpgradePlannerService.UpgradeType.BreakerReplacement, candidate.Type);
    }
}
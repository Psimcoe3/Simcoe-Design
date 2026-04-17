using System.Collections.Generic;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ProjectProtectionProgramServiceTests
{
    [Fact]
    public void BuildReport_NoComponents_ReturnsEmptyReport()
    {
        var service = new ProjectProtectionProgramService();

        var report = service.BuildReport(System.Array.Empty<ElectricalComponent>());

        Assert.False(ProjectProtectionProgramService.HasMeaningfulContent(report));
        Assert.Empty(report.Actions);
        Assert.Empty(report.RecommendedUpgrades);
    }

    [Fact]
    public void BuildReport_AdequateDistribution_ProducesExportableSummary()
    {
        var service = new ProjectProtectionProgramService();

        var report = service.BuildReport(new ElectricalComponent[]
        {
            new PowerSourceComponent { Id = "SRC", Name = "Utility", Voltage = 480, AvailableFaultCurrentKA = 22 },
            new PanelComponent { Id = "MDP", Name = "Main Distribution", FeederId = "SRC", AICRatingKA = 65, Subtype = PanelSubtype.Switchboard },
        });

        Assert.True(ProjectProtectionProgramService.HasMeaningfulContent(report));
        Assert.Equal(100, report.ReadinessScore);
        Assert.Equal(0, report.DutyViolationCount);
    }

    [Fact]
    public void BuildReport_AicViolation_ProducesDutyActionAndUpgradeRecommendation()
    {
        var service = new ProjectProtectionProgramService();

        var report = service.BuildReport(new ElectricalComponent[]
        {
            new PowerSourceComponent { Id = "SRC", Name = "Utility", Voltage = 480, AvailableFaultCurrentKA = 85 },
            new PanelComponent { Id = "SWBD", Name = "Service Switchboard", FeederId = "SRC", AICRatingKA = 22, Subtype = PanelSubtype.Switchboard },
        });

        Assert.True(report.DutyViolationCount > 0);
        Assert.Contains(report.Actions, action => action.Category == "Interrupting duty");
        Assert.Contains(report.RecommendedUpgrades, recommendation => recommendation.Type == ProtectionUpgradePlannerService.UpgradeType.BreakerReplacement);
    }

    [Fact]
    public void BuildReport_HigherArcFlashExposure_ProducesArcFlashMitigationAction()
    {
        var service = new ProjectProtectionProgramService();

        var report = service.BuildReport(new ElectricalComponent[]
        {
            new PowerSourceComponent { Id = "SRC", Name = "Utility", Voltage = 480, AvailableFaultCurrentKA = 65 },
            new BusComponent { Id = "BUS-1", Name = "Main Bus", FeederId = "SRC", Voltage = 480 },
        });

        Assert.True(report.AverageArcFlashReductionPercent > 0);
        Assert.Contains(report.Actions, action => action.Category == "Arc flash");
        Assert.Contains(report.RecommendedUpgrades, recommendation => recommendation.Type == ProtectionUpgradePlannerService.UpgradeType.MaintenanceSwitch);
    }

    [Fact]
    public void BuildReport_WithPanelSchedules_DerivesCoordinationActionAndSettingsUpgrade()
    {
        var service = new ProjectProtectionProgramService();

        var report = service.BuildReport(
            new ElectricalComponent[]
            {
                new PowerSourceComponent { Id = "SRC", Name = "Utility", Voltage = 480, AvailableFaultCurrentKA = 42 },
                new PanelComponent { Id = "MDP", Name = "Main Distribution", FeederId = "SRC", AICRatingKA = 65, Subtype = PanelSubtype.Switchboard, Amperage = 800, BusAmpacity = 800 },
                new PanelComponent { Id = "LP-1", Name = "Lighting Panel", FeederId = "MDP", AICRatingKA = 22, Subtype = PanelSubtype.Panelboard, Amperage = 225, BusAmpacity = 225 },
            },
            new[]
            {
                new PanelSchedule { PanelId = "LP-1", PanelName = "Lighting Panel", BusAmps = 225, MainBreakerAmps = 225, Circuits = new List<Circuit> { new() { CircuitNumber = "1", Description = "Lighting", Voltage = 277, Poles = 1, Phase = "A", ConnectedLoadVA = 18000, DemandFactor = 1.0 } } },
            });

        Assert.True(report.CoordinationViolationCount > 0);
        Assert.Contains(report.Actions, action => action.Category == "Coordination");
        Assert.Contains(report.RecommendedUpgrades, recommendation => recommendation.Type == ProtectionUpgradePlannerService.UpgradeType.SettingsChange);
    }

    [Fact]
    public void BuildReport_WithStoredRelaySettings_UsesComponentRelayConfiguration()
    {
        var service = new ProjectProtectionProgramService();

        var report = service.BuildReport(new ElectricalComponent[]
        {
            new PowerSourceComponent { Id = "SRC", Name = "Utility", Voltage = 480, AvailableFaultCurrentKA = 22 },
            new PanelComponent
            {
                Id = "MDP",
                Name = "Main Distribution",
                FeederId = "SRC",
                AICRatingKA = 65,
                Subtype = PanelSubtype.Switchboard,
                ProtectionSettings = new ComponentProtectionSettings
                {
                    StudyRelay = new StoredProtectiveRelaySettings
                    {
                        Function = ProtectiveRelayService.RelayFunction.Function51,
                        Curve = ProtectiveRelayService.CurveType.VeryInverse,
                        CtRatio = 400,
                        PickupAmps = 300,
                        TimeDial = 0.4,
                        InstantaneousAmps = 1400,
                    },
                    FieldRelay = new StoredProtectiveRelaySettings
                    {
                        Function = ProtectiveRelayService.RelayFunction.Function51,
                        Curve = ProtectiveRelayService.CurveType.ExtremelyInverse,
                        CtRatio = 400,
                        PickupAmps = 300,
                        TimeDial = 0.4,
                        InstantaneousAmps = 1400,
                    },
                },
            },
        });

        Assert.True(report.RelayCriticalCount > 0);
        Assert.Contains(report.Actions, action => action.Category == "Relay settings");
    }
}
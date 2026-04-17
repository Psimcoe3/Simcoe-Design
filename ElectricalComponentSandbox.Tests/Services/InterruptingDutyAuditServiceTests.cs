using System.Collections.Generic;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class InterruptingDutyAuditServiceTests
{
    [Theory]
    [InlineData(true, 20, InterruptingDutyAuditService.DutySeverity.Low)]
    [InlineData(true, 5, InterruptingDutyAuditService.DutySeverity.Moderate)]
    [InlineData(false, -10, InterruptingDutyAuditService.DutySeverity.High)]
    [InlineData(false, -30, InterruptingDutyAuditService.DutySeverity.Critical)]
    public void ClassifySeverity_UsesAdequacyAndMargin(bool isAdequate, double marginPercent, InterruptingDutyAuditService.DutySeverity expected)
    {
        var result = new ShortCircuitResult
        {
            IsAdequate = isAdequate,
            MarginPercent = marginPercent,
        };

        Assert.Equal(expected, InterruptingDutyAuditService.ClassifySeverity(result));
    }

    [Fact]
    public void CreateExposure_AssignsRecommendedAction()
    {
        var exposure = InterruptingDutyAuditService.CreateExposure(new ShortCircuitResult
        {
            NodeId = "MCC-1",
            NodeName = "Main MCC",
            AvailableFaultKA = 42,
            EquipmentAICKA = 30,
            IsAdequate = false,
            MarginPercent = -28.6,
        });

        Assert.Equal(InterruptingDutyAuditService.DutySeverity.Critical, exposure.Severity);
        Assert.Contains("Replace", exposure.RecommendedAction);
    }

    [Fact]
    public void AuditResults_SortsHighestExposureFirst()
    {
        var summary = InterruptingDutyAuditService.AuditResults(new[]
        {
            new ShortCircuitResult { NodeId = "A", NodeName = "Adequate", IsAdequate = true, MarginPercent = 15, AvailableFaultKA = 18, EquipmentAICKA = 21 },
            new ShortCircuitResult { NodeId = "B", NodeName = "Marginal", IsAdequate = true, MarginPercent = 4, AvailableFaultKA = 18, EquipmentAICKA = 18.7 },
            new ShortCircuitResult { NodeId = "C", NodeName = "Underrated", IsAdequate = false, MarginPercent = -30, AvailableFaultKA = 52, EquipmentAICKA = 36 },
        });

        Assert.Equal(3, summary.TotalNodeCount);
        Assert.Equal(1, summary.ViolationCount);
        Assert.Equal(1, summary.CriticalCount);
        Assert.Equal("C", summary.HighestExposure.NodeId);
    }

    [Fact]
    public void AuditDistribution_UsesShortCircuitValidation()
    {
        var roots = new List<DistributionNode>
        {
            new()
            {
                Id = "SRC",
                Name = "Source",
                NodeType = ComponentType.PowerSource,
                FaultCurrentKA = 65,
                Component = new PowerSourceComponent { Voltage = 480, AvailableFaultCurrentKA = 65 },
                Children =
                {
                    new DistributionNode
                    {
                        Id = "PNL",
                        Name = "Panel A",
                        NodeType = ComponentType.Panel,
                        FaultCurrentKA = 35,
                        Component = new PanelComponent { AICRatingKA = 22 },
                    },
                },
            },
        };

        var summary = InterruptingDutyAuditService.AuditDistribution(roots, new ShortCircuitService());

        Assert.Equal(2, summary.TotalNodeCount);
        Assert.Equal("PNL", summary.HighestExposure.NodeId);
        Assert.Contains(summary.Exposures, exposure => exposure.NodeId == "PNL" && exposure.Severity == InterruptingDutyAuditService.DutySeverity.Critical);
    }
}
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class HostingCapacityServiceTests
{
    [Fact]
    public void CalculateThermalCapacityKw_ReturnsHeadroom()
    {
        double result = HostingCapacityService.CalculateThermalCapacityKw(900, 650);

        Assert.Equal(250, result);
    }

    [Fact]
    public void CalculateThermalCapacityKw_FloorsAtZero()
    {
        double result = HostingCapacityService.CalculateThermalCapacityKw(500, 620);

        Assert.Equal(0, result);
    }

    [Fact]
    public void EstimateVoltageRisePercent_ScalesLinearly()
    {
        double result = HostingCapacityService.EstimateVoltageRisePercent(250, 0.8);

        Assert.Equal(2.0, result);
    }

    [Fact]
    public void CalculateVoltageLimitedCapacityKw_UsesRiseAllowance()
    {
        double result = HostingCapacityService.CalculateVoltageLimitedCapacityKw(3.0, 0.75);

        Assert.Equal(400, result);
    }

    [Fact]
    public void CalculateExportCapacityKw_AddsMinimumLoadAndAllowedExport()
    {
        double result = HostingCapacityService.CalculateExportCapacityKw(180, 120);

        Assert.Equal(300, result);
    }

    [Fact]
    public void AnalyzeNode_ThermalConstraint_CanBeLimiting()
    {
        var node = new HostingCapacityService.HostingNode
        {
            Name = "Node A",
            ExistingPeakLoadKw = 780,
            MinimumLoadKw = 250,
            ThermalRatingKw = 900,
            UpstreamExportLimitKw = 250,
            VoltageRisePercentPer100Kw = 0.4,
        };

        var result = HostingCapacityService.AnalyzeNode(node);

        Assert.Equal(120, result.RecommendedHostingCapacityKw);
        Assert.Equal("Thermal headroom", result.LimitingConstraint);
        Assert.False(result.RequiresDetailedStudy);
    }

    [Fact]
    public void AnalyzeNode_VoltageConstraint_CanBeLimiting()
    {
        var node = new HostingCapacityService.HostingNode
        {
            Name = "Node B",
            ExistingPeakLoadKw = 400,
            MinimumLoadKw = 300,
            ThermalRatingKw = 1200,
            UpstreamExportLimitKw = 300,
            VoltageRisePercentPer100Kw = 1.5,
        };

        var result = HostingCapacityService.AnalyzeNode(node);

        Assert.Equal(200, result.RecommendedHostingCapacityKw);
        Assert.Equal("Voltage rise", result.LimitingConstraint);
        Assert.True(result.RequiresDetailedStudy);
    }

    [Fact]
    public void AnalyzeNode_ExportConstraint_CanBeLimiting()
    {
        var node = new HostingCapacityService.HostingNode
        {
            Name = "Node C",
            ExistingPeakLoadKw = 300,
            MinimumLoadKw = 90,
            ThermalRatingKw = 900,
            UpstreamExportLimitKw = 60,
            VoltageRisePercentPer100Kw = 0.4,
        };

        var result = HostingCapacityService.AnalyzeNode(node);

        Assert.Equal(150, result.RecommendedHostingCapacityKw);
        Assert.Equal("Minimum load and export allowance", result.LimitingConstraint);
        Assert.True(result.RequiresDetailedStudy);
    }

    [Fact]
    public void AnalyzePortfolio_TracksMostConstrainedNode()
    {
        var result = HostingCapacityService.AnalyzePortfolio(new[]
        {
            new HostingCapacityService.HostingNode
            {
                Name = "Node Wide",
                ExistingPeakLoadKw = 450,
                MinimumLoadKw = 250,
                ThermalRatingKw = 1000,
                UpstreamExportLimitKw = 250,
                VoltageRisePercentPer100Kw = 0.5,
            },
            new HostingCapacityService.HostingNode
            {
                Name = "Node Tight",
                ExistingPeakLoadKw = 780,
                MinimumLoadKw = 150,
                ThermalRatingKw = 860,
                UpstreamExportLimitKw = 90,
                VoltageRisePercentPer100Kw = 0.9,
            },
        });

        Assert.Equal(2, result.NodeCount);
        Assert.Equal("Node Tight", result.MostConstrainedNode);
        Assert.True(result.MinimumHostingCapacityKw < 200);
    }
}
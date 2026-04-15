using System.Collections.Generic;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class OutageCostServiceTests
{
    [Theory]
    [InlineData(OutageImpactService.CustomerClass.Residential, 15, 2)]
    [InlineData(OutageImpactService.CustomerClass.Commercial, 150, 6)]
    [InlineData(OutageImpactService.CustomerClass.Industrial, 450, 12)]
    [InlineData(OutageImpactService.CustomerClass.Critical, 900, 18)]
    public void GetDefaultRate_ReturnsExpectedRates(OutageImpactService.CustomerClass customerClass, double customerHourRate, double energyRate)
    {
        var rate = OutageCostService.GetDefaultRate(customerClass);

        Assert.Equal(customerHourRate, rate.DollarsPerCustomerHour);
        Assert.Equal(energyRate, rate.DollarsPerUnservedKWh);
    }

    [Fact]
    public void CalculateCustomerInterruptionCost_ComputesCustomerHourCost()
    {
        double result = OutageCostService.CalculateCustomerInterruptionCost(
            10,
            120,
            new OutageCostService.InterruptionCostRate { DollarsPerCustomerHour = 20, DollarsPerUnservedKWh = 0 },
            isCriticalFacility: false);

        Assert.Equal(400, result);
    }

    [Fact]
    public void CalculateCustomerInterruptionCost_AppliesCriticalMultiplier()
    {
        double standard = OutageCostService.CalculateCustomerInterruptionCost(
            1,
            60,
            new OutageCostService.InterruptionCostRate { DollarsPerCustomerHour = 100, DollarsPerUnservedKWh = 0, CriticalFacilityMultiplier = 4 },
            isCriticalFacility: false);
        double critical = OutageCostService.CalculateCustomerInterruptionCost(
            1,
            60,
            new OutageCostService.InterruptionCostRate { DollarsPerCustomerHour = 100, DollarsPerUnservedKWh = 0, CriticalFacilityMultiplier = 4 },
            isCriticalFacility: true);

        Assert.Equal(400, critical);
        Assert.Equal(100, standard);
    }

    [Fact]
    public void EstimateScenarioCost_SumsCustomerAndEnergyComponents()
    {
        var result = OutageCostService.EstimateScenarioCost(new OutageImpactService.OutageScenario
        {
            Name = "Mixed",
            DurationMinutes = 60,
            Segments =
            {
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Residential, CustomerCount = 10, AverageDemandKw = 1 },
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Commercial, CustomerCount = 2, AverageDemandKw = 20 },
            },
        });

        Assert.True(result.CustomerInterruptionCost > 0);
        Assert.True(result.EnergyNotServedCost > 0);
        Assert.Equal(result.CustomerInterruptionCost + result.EnergyNotServedCost, result.TotalCost, 2);
    }

    [Fact]
    public void EstimateScenarioCost_CarriesImpactSummary()
    {
        var result = OutageCostService.EstimateScenarioCost(new OutageImpactService.OutageScenario
        {
            Name = "Substation",
            DurationMinutes = 45,
            Segments =
            {
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Commercial, CustomerCount = 8, AverageDemandKw = 15 },
            },
        });

        Assert.Equal(8, result.ImpactSummary.TotalCustomersAffected);
        Assert.Equal("Substation", result.ImpactSummary.ScenarioName);
    }

    [Fact]
    public void EstimateScenarioCost_CriticalFacilityCostsMoreThanStandardCriticalClass()
    {
        var standard = OutageCostService.EstimateScenarioCost(new OutageImpactService.OutageScenario
        {
            Name = "Standard Critical",
            DurationMinutes = 30,
            Segments =
            {
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Critical, CustomerCount = 1, AverageDemandKw = 100 },
            },
        });
        var criticalFacility = OutageCostService.EstimateScenarioCost(new OutageImpactService.OutageScenario
        {
            Name = "Hospital",
            DurationMinutes = 30,
            Segments =
            {
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Critical, CustomerCount = 1, AverageDemandKw = 100, IsCriticalFacility = true },
            },
        });

        Assert.True(criticalFacility.TotalCost > standard.TotalCost);
    }

    [Fact]
    public void EstimateScenarioCost_CustomRatesOverrideDefaults()
    {
        var result = OutageCostService.EstimateScenarioCost(
            new OutageImpactService.OutageScenario
            {
                Name = "Custom",
                DurationMinutes = 60,
                Segments =
                {
                    new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Residential, CustomerCount = 10, AverageDemandKw = 1 },
                },
            },
            new Dictionary<OutageImpactService.CustomerClass, OutageCostService.InterruptionCostRate>
            {
                [OutageImpactService.CustomerClass.Residential] = new OutageCostService.InterruptionCostRate { DollarsPerCustomerHour = 100, DollarsPerUnservedKWh = 10 },
            });

        Assert.Equal(1100, result.TotalCost);
    }

    [Fact]
    public void EstimateScenarioCost_ReportsHighestCostClass()
    {
        var result = OutageCostService.EstimateScenarioCost(new OutageImpactService.OutageScenario
        {
            Name = "Priority Mix",
            DurationMinutes = 60,
            Segments =
            {
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Residential, CustomerCount = 100, AverageDemandKw = 1 },
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Industrial, CustomerCount = 2, AverageDemandKw = 500 },
            },
        });

        Assert.Equal("Industrial", result.HighestCostClass);
    }

    [Fact]
    public void EstimateScenarioCost_EmptyScenarioReturnsZeroCost()
    {
        var result = OutageCostService.EstimateScenarioCost(new OutageImpactService.OutageScenario
        {
            Name = "Empty",
            DurationMinutes = 30,
        });

        Assert.Equal(0, result.TotalCost);
        Assert.Empty(result.SegmentDetails);
    }
}
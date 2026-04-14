using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class OutageImpactServiceTests
{
    [Fact]
    public void CalculateCustomerMinutesInterrupted_MultipliesCustomersAndDuration()
    {
        double result = OutageImpactService.CalculateCustomerMinutesInterrupted(120, 45);

        Assert.Equal(5400, result);
    }

    [Fact]
    public void CalculateUnservedEnergyMWh_ConvertsKwMinutesToMwh()
    {
        double result = OutageImpactService.CalculateUnservedEnergyMWh(600, 30);

        Assert.Equal(0.3, result, 4);
    }

    [Theory]
    [InlineData(OutageImpactService.CustomerClass.Residential, false, 1.0)]
    [InlineData(OutageImpactService.CustomerClass.Commercial, false, 2.0)]
    [InlineData(OutageImpactService.CustomerClass.Industrial, false, 3.0)]
    [InlineData(OutageImpactService.CustomerClass.Residential, true, 5.0)]
    public void GetImpactWeight_ReturnsExpectedWeights(OutageImpactService.CustomerClass customerClass, bool criticalFacility, double expected)
    {
        double result = OutageImpactService.GetImpactWeight(customerClass, criticalFacility);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void AnalyzeScenario_SumsCustomersAndCustomerMinutes()
    {
        var result = OutageImpactService.AnalyzeScenario(new OutageImpactService.OutageScenario
        {
            Name = "Feeder Trip",
            DurationMinutes = 60,
            Segments =
            {
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Residential, CustomerCount = 100, AverageDemandKw = 1.5 },
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Commercial, CustomerCount = 20, AverageDemandKw = 10 },
            },
        });

        Assert.Equal(120, result.TotalCustomersAffected);
        Assert.Equal(7200, result.CustomerMinutesInterrupted);
    }

    [Fact]
    public void AnalyzeScenario_TracksCriticalCustomersAndMinutes()
    {
        var result = OutageImpactService.AnalyzeScenario(new OutageImpactService.OutageScenario
        {
            Name = "Hospital Feeder",
            DurationMinutes = 30,
            Segments =
            {
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Critical, CustomerCount = 2, AverageDemandKw = 250, IsCriticalFacility = true },
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Residential, CustomerCount = 50, AverageDemandKw = 1.2 },
            },
        });

        Assert.Equal(2, result.CriticalCustomersAffected);
        Assert.Equal(60, result.CriticalCustomerMinutesInterrupted);
    }

    [Fact]
    public void AnalyzeScenario_ComputesUnservedEnergy()
    {
        var result = OutageImpactService.AnalyzeScenario(new OutageImpactService.OutageScenario
        {
            Name = "Industrial Outage",
            DurationMinutes = 120,
            Segments =
            {
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Industrial, CustomerCount = 3, AverageDemandKw = 500 },
            },
        });

        Assert.Equal(3.0, result.UnservedEnergyMWh, 4);
    }

    [Fact]
    public void AnalyzeScenario_WeightedImpactPenalizesCriticalAndIndustrialLoads()
    {
        var residential = OutageImpactService.AnalyzeScenario(new OutageImpactService.OutageScenario
        {
            Name = "Residential",
            DurationMinutes = 60,
            Segments =
            {
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Residential, CustomerCount = 10, AverageDemandKw = 1.5 },
            },
        });
        var industrial = OutageImpactService.AnalyzeScenario(new OutageImpactService.OutageScenario
        {
            Name = "Industrial",
            DurationMinutes = 60,
            Segments =
            {
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Industrial, CustomerCount = 10, AverageDemandKw = 1.5 },
            },
        });

        Assert.True(industrial.WeightedImpactScore > residential.WeightedImpactScore);
    }

    [Fact]
    public void AnalyzeScenario_ShortOutage_IsMomentary()
    {
        var result = OutageImpactService.AnalyzeScenario(new OutageImpactService.OutageScenario
        {
            Name = "Blink",
            DurationMinutes = 3,
            Segments =
            {
                new OutageImpactService.AffectedSegment { CustomerClass = OutageImpactService.CustomerClass.Residential, CustomerCount = 100, AverageDemandKw = 1.5 },
            },
        });

        Assert.True(result.IsMomentary);
    }

    [Fact]
    public void AnalyzeScenario_EmptySegments_ReturnsZeroImpact()
    {
        var result = OutageImpactService.AnalyzeScenario(new OutageImpactService.OutageScenario
        {
            Name = "Empty",
            DurationMinutes = 45,
        });

        Assert.Equal(0, result.TotalCustomersAffected);
        Assert.Equal(0, result.UnservedEnergyMWh);
    }
}
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class GovernorDroopServiceTests
{
    [Fact]
    public void CalculateOperatingFrequency_HigherLoad_LowersFrequency()
    {
        double low = GovernorDroopService.CalculateOperatingFrequency(60, 25);
        double high = GovernorDroopService.CalculateOperatingFrequency(60, 75);

        Assert.True(high < low);
    }

    [Fact]
    public void CalculateOperatingFrequency_HigherDroop_LowersFrequencyMore()
    {
        double lowDroop = GovernorDroopService.CalculateOperatingFrequency(60, 50, droopPercent: 3);
        double highDroop = GovernorDroopService.CalculateOperatingFrequency(60, 50, droopPercent: 5);

        Assert.True(highDroop < lowDroop);
    }

    [Fact]
    public void CalculateOperatingFrequency_SpeedBias_RaisesReferenceFrequency()
    {
        double unbiased = GovernorDroopService.CalculateOperatingFrequency(60, 50, speedBiasPercent: 0);
        double biased = GovernorDroopService.CalculateOperatingFrequency(60, 50, speedBiasPercent: 1);

        Assert.True(biased > unbiased);
    }

    [Fact]
    public void CalculateLoadPercent_InvertsOperatingFrequency()
    {
        double frequency = GovernorDroopService.CalculateOperatingFrequency(60, 50, droopPercent: 5);
        double load = GovernorDroopService.CalculateLoadPercent(60, frequency, droopPercent: 5);

        Assert.Equal(50.0, load, 1);
    }

    [Fact]
    public void ShareLoad_EqualUnits_ShareEqually()
    {
        var units = new[]
        {
            new GovernorDroopService.GovernorUnit { Id = "G1", RatedKW = 100 },
            new GovernorDroopService.GovernorUnit { Id = "G2", RatedKW = 100 },
        };

        var result = GovernorDroopService.ShareLoad(units, 120);

        Assert.Equal(60.0, result.UnitShares.Single(share => share.Id == "G1").AssignedKW, 1);
        Assert.Equal(60.0, result.UnitShares.Single(share => share.Id == "G2").AssignedKW, 1);
    }

    [Fact]
    public void ShareLoad_LargerUnit_PicksUpMoreLoad()
    {
        var units = new[]
        {
            new GovernorDroopService.GovernorUnit { Id = "G1", RatedKW = 100 },
            new GovernorDroopService.GovernorUnit { Id = "G2", RatedKW = 50 },
        };

        var result = GovernorDroopService.ShareLoad(units, 90);

        Assert.True(result.UnitShares.Single(share => share.Id == "G1").AssignedKW >
            result.UnitShares.Single(share => share.Id == "G2").AssignedKW);
    }

    [Fact]
    public void ShareLoad_LowerDroop_PicksUpMoreLoad()
    {
        var units = new[]
        {
            new GovernorDroopService.GovernorUnit { Id = "G1", RatedKW = 100, DroopPercent = 3 },
            new GovernorDroopService.GovernorUnit { Id = "G2", RatedKW = 100, DroopPercent = 5 },
        };

        var result = GovernorDroopService.ShareLoad(units, 120);

        Assert.True(result.UnitShares.Single(share => share.Id == "G1").AssignedKW >
            result.UnitShares.Single(share => share.Id == "G2").AssignedKW);
    }

    [Fact]
    public void ShareLoad_SpeedBias_PicksUpMoreLoad()
    {
        var units = new[]
        {
            new GovernorDroopService.GovernorUnit { Id = "G1", RatedKW = 100, SpeedBiasPercent = 1 },
            new GovernorDroopService.GovernorUnit { Id = "G2", RatedKW = 100, SpeedBiasPercent = 0 },
        };

        var result = GovernorDroopService.ShareLoad(units, 120);

        Assert.True(result.UnitShares.Single(share => share.Id == "G1").AssignedKW >
            result.UnitShares.Single(share => share.Id == "G2").AssignedKW);
    }

    [Fact]
    public void ShareLoad_SystemFrequencyFallsBelowNominalUnderLoad()
    {
        var units = new[]
        {
            new GovernorDroopService.GovernorUnit { Id = "G1", RatedKW = 100 },
            new GovernorDroopService.GovernorUnit { Id = "G2", RatedKW = 100 },
        };

        var result = GovernorDroopService.ShareLoad(units, 120);

        Assert.True(result.SystemFrequencyHz < 60.0);
    }

    [Fact]
    public void ShareLoad_TotalAssignedMatchesDemand()
    {
        var units = new[]
        {
            new GovernorDroopService.GovernorUnit { Id = "G1", RatedKW = 100 },
            new GovernorDroopService.GovernorUnit { Id = "G2", RatedKW = 100 },
            new GovernorDroopService.GovernorUnit { Id = "G3", RatedKW = 50 },
        };

        var result = GovernorDroopService.ShareLoad(units, 150);

        Assert.Equal(150.0, result.UnitShares.Sum(share => share.AssignedKW), 1);
    }

    [Fact]
    public void ShareLoad_InsufficientCapacity_Fails()
    {
        var units = new[]
        {
            new GovernorDroopService.GovernorUnit { Id = "G1", RatedKW = 50 },
            new GovernorDroopService.GovernorUnit { Id = "G2", RatedKW = 50 },
        };

        var result = GovernorDroopService.ShareLoad(units, 120);

        Assert.False(result.IsAdequate);
        Assert.Contains("below demand", result.Issue, System.StringComparison.OrdinalIgnoreCase);
    }
}
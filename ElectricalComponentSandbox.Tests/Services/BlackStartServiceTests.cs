using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class BlackStartServiceTests
{
    [Fact]
    public void GetTypicalAuxiliaryLoadPercent_GasTurbineExceedsHydro()
    {
        double gas = BlackStartService.GetTypicalAuxiliaryLoadPercent(BlackStartService.GenerationType.GasTurbine);
        double hydro = BlackStartService.GetTypicalAuxiliaryLoadPercent(BlackStartService.GenerationType.Hydro);

        Assert.True(gas > hydro);
    }

    [Fact]
    public void GetTypicalStartTimeMinutes_BatteryStorageIsFastest()
    {
        double battery = BlackStartService.GetTypicalStartTimeMinutes(BlackStartService.GenerationType.BatteryStorage);
        double turbine = BlackStartService.GetTypicalStartTimeMinutes(BlackStartService.GenerationType.GasTurbine);

        Assert.True(battery < turbine);
    }

    [Fact]
    public void CalculateStartPower_HigherRatingNeedsMorePower()
    {
        double small = BlackStartService.CalculateStartPowerKW(100, BlackStartService.GenerationType.Diesel);
        double large = BlackStartService.CalculateStartPowerKW(200, BlackStartService.GenerationType.Diesel);

        Assert.True(large > small);
    }

    [Fact]
    public void CreateRestorationPlan_BlackStartCapableUnitCanStartWithoutSeed()
    {
        var units = new[]
        {
            new BlackStartService.BlackStartUnit
            {
                Id = "BS1",
                Type = BlackStartService.GenerationType.Diesel,
                RatedKW = 150,
                IsBlackStartCapable = true,
            },
        };

        var result = BlackStartService.CreateRestorationPlan(units, 0);

        Assert.True(result.IsFeasible);
        Assert.Equal(1, result.RestoredUnits);
        Assert.Equal(0, result.Steps[0].ExternalStartPowerKW);
    }

    [Fact]
    public void CreateRestorationPlan_SeedSourceCanStartNonBlackStartUnit()
    {
        var units = new[]
        {
            new BlackStartService.BlackStartUnit
            {
                Id = "G1",
                Type = BlackStartService.GenerationType.Diesel,
                RatedKW = 200,
                IsBlackStartCapable = false,
            },
        };

        var result = BlackStartService.CreateRestorationPlan(units, 20);

        Assert.True(result.IsFeasible);
        Assert.Equal(1, result.RestoredUnits);
        Assert.True(result.Steps[0].ExternalStartPowerKW <= 20);
    }

    [Fact]
    public void CreateRestorationPlan_StartedUnitAddsNetContribution()
    {
        var units = new[]
        {
            new BlackStartService.BlackStartUnit
            {
                Id = "G1",
                Type = BlackStartService.GenerationType.Diesel,
                RatedKW = 100,
                IsBlackStartCapable = true,
            },
        };

        var result = BlackStartService.CreateRestorationPlan(units, 0);

        Assert.True(result.AvailableRestorationPowerKW > 0);
    }

    [Fact]
    public void CreateRestorationPlan_CanChainAdditionalUnits()
    {
        var units = new[]
        {
            new BlackStartService.BlackStartUnit
            {
                Id = "Seed",
                Type = BlackStartService.GenerationType.Diesel,
                RatedKW = 100,
                IsBlackStartCapable = true,
                Priority = 1,
            },
            new BlackStartService.BlackStartUnit
            {
                Id = "GT1",
                Type = BlackStartService.GenerationType.GasTurbine,
                RatedKW = 500,
                IsBlackStartCapable = false,
                Priority = 2,
            },
        };

        var result = BlackStartService.CreateRestorationPlan(units, 0);

        Assert.True(result.IsFeasible);
        Assert.Equal(2, result.RestoredUnits);
    }

    [Fact]
    public void CreateRestorationPlan_InsufficientSeedSourceFails()
    {
        var units = new[]
        {
            new BlackStartService.BlackStartUnit
            {
                Id = "GT1",
                Type = BlackStartService.GenerationType.GasTurbine,
                RatedKW = 500,
                IsBlackStartCapable = false,
            },
        };

        var result = BlackStartService.CreateRestorationPlan(units, 10);

        Assert.False(result.IsFeasible);
        Assert.NotNull(result.Issue);
    }

    [Fact]
    public void CreateRestorationPlan_PriorityControlsOrder()
    {
        var units = new[]
        {
            new BlackStartService.BlackStartUnit
            {
                Id = "G1",
                Type = BlackStartService.GenerationType.Diesel,
                RatedKW = 120,
                IsBlackStartCapable = true,
                Priority = 2,
            },
            new BlackStartService.BlackStartUnit
            {
                Id = "G2",
                Type = BlackStartService.GenerationType.Diesel,
                RatedKW = 90,
                IsBlackStartCapable = true,
                Priority = 1,
            },
        };

        var result = BlackStartService.CreateRestorationPlan(units, 0);

        Assert.Equal("G2", result.Steps.First().UnitId);
    }
}
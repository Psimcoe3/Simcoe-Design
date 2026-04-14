using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class MicrogridDispatchServiceTests
{
    [Fact]
    public void CalculateRequiredCapacity_AddsReserve()
    {
        double result = MicrogridDispatchService.CalculateRequiredCapacity(100, 15);

        Assert.Equal(115.0, result);
    }

    [Fact]
    public void CreateDispatchPlan_RenewablePriority_CommitsRenewableFirst()
    {
        var units = new[]
        {
            new MicrogridDispatchService.DispatchUnit { Id = "PV", RatedKW = 50, Priority = MicrogridDispatchService.DispatchPriority.Renewable, CostPerKWh = 0 },
            new MicrogridDispatchService.DispatchUnit { Id = "GEN", RatedKW = 100, Priority = MicrogridDispatchService.DispatchPriority.Economic, CostPerKWh = 0.2 },
        };

        var result = MicrogridDispatchService.CreateDispatchPlan(units, 80, reservePercent: 0);

        Assert.Equal(50.0, result.Allocations.Single(x => x.Id == "PV").AssignedKW, 1);
    }

    [Fact]
    public void CreateDispatchPlan_LowCostEconomicUnit_DispatchedBeforePeaker()
    {
        var units = new[]
        {
            new MicrogridDispatchService.DispatchUnit { Id = "ECO", RatedKW = 100, CostPerKWh = 0.10, Priority = MicrogridDispatchService.DispatchPriority.Economic },
            new MicrogridDispatchService.DispatchUnit { Id = "PEAK", RatedKW = 100, CostPerKWh = 0.40, Priority = MicrogridDispatchService.DispatchPriority.Peaking },
        };

        var result = MicrogridDispatchService.CreateDispatchPlan(units, 80, reservePercent: 0);

        Assert.Equal(80.0, result.Allocations.Single(x => x.Id == "ECO").AssignedKW, 1);
        Assert.DoesNotContain(result.Allocations, x => x.Id == "PEAK");
    }

    [Fact]
    public void CreateDispatchPlan_ReserveRequirement_CommitsExtraCapacity()
    {
        var units = new[]
        {
            new MicrogridDispatchService.DispatchUnit { Id = "G1", RatedKW = 60, CostPerKWh = 0.1 },
            new MicrogridDispatchService.DispatchUnit { Id = "G2", RatedKW = 60, CostPerKWh = 0.12 },
        };

        var result = MicrogridDispatchService.CreateDispatchPlan(units, 100, reservePercent: 15);

        Assert.Equal(120.0, result.OnlineCapacityKW);
        Assert.True(result.SpinningReserveKW >= 20);
    }

    [Fact]
    public void CreateDispatchPlan_UnavailableUnit_IsSkipped()
    {
        var units = new[]
        {
            new MicrogridDispatchService.DispatchUnit { Id = "OFF", RatedKW = 100, CostPerKWh = 0.05, IsAvailable = false },
            new MicrogridDispatchService.DispatchUnit { Id = "ON", RatedKW = 100, CostPerKWh = 0.10 },
        };

        var result = MicrogridDispatchService.CreateDispatchPlan(units, 50, reservePercent: 0);

        Assert.DoesNotContain(result.Allocations, allocation => allocation.Id == "OFF");
    }

    [Fact]
    public void CreateDispatchPlan_MinimumLoadingIsApplied()
    {
        var units = new[]
        {
            new MicrogridDispatchService.DispatchUnit { Id = "GEN", RatedKW = 100, MinimumKW = 20, CostPerKWh = 0.1 },
        };

        var result = MicrogridDispatchService.CreateDispatchPlan(units, 40, reservePercent: 0);

        Assert.Equal(40.0, result.Allocations.Single().AssignedKW, 1);
    }

    [Fact]
    public void CreateDispatchPlan_TooMuchMinimumGeneration_FlagsIssue()
    {
        var units = new[]
        {
            new MicrogridDispatchService.DispatchUnit { Id = "G1", RatedKW = 60, MinimumKW = 40, CostPerKWh = 0.1 },
            new MicrogridDispatchService.DispatchUnit { Id = "G2", RatedKW = 60, MinimumKW = 40, CostPerKWh = 0.2 },
        };

        var result = MicrogridDispatchService.CreateDispatchPlan(units, 50, reservePercent: 100);

        Assert.False(result.IsAdequate);
        Assert.Contains("minimum", result.Issue, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateDispatchPlan_TotalAssignedMatchesDemand()
    {
        var units = new[]
        {
            new MicrogridDispatchService.DispatchUnit { Id = "G1", RatedKW = 100, CostPerKWh = 0.1 },
            new MicrogridDispatchService.DispatchUnit { Id = "G2", RatedKW = 100, CostPerKWh = 0.2 },
        };

        var result = MicrogridDispatchService.CreateDispatchPlan(units, 130, reservePercent: 0);

        Assert.Equal(130.0, result.Allocations.Sum(x => x.AssignedKW), 1);
    }

    [Fact]
    public void CreateDispatchPlan_TotalCostReflectsDispatch()
    {
        var units = new[]
        {
            new MicrogridDispatchService.DispatchUnit { Id = "G1", RatedKW = 50, CostPerKWh = 0.1 },
            new MicrogridDispatchService.DispatchUnit { Id = "G2", RatedKW = 50, CostPerKWh = 0.2 },
        };

        var result = MicrogridDispatchService.CreateDispatchPlan(units, 75, reservePercent: 0);

        Assert.Equal(10.0, result.TotalOperatingCostPerHour, 1);
    }

    [Fact]
    public void CreateDispatchPlan_InsufficientCapacity_Fails()
    {
        var units = new[]
        {
            new MicrogridDispatchService.DispatchUnit { Id = "G1", RatedKW = 40, CostPerKWh = 0.1 },
            new MicrogridDispatchService.DispatchUnit { Id = "G2", RatedKW = 40, CostPerKWh = 0.2 },
        };

        var result = MicrogridDispatchService.CreateDispatchPlan(units, 100, reservePercent: 0);

        Assert.False(result.IsAdequate);
        Assert.Contains("below demand", result.Issue, System.StringComparison.OrdinalIgnoreCase);
    }
}
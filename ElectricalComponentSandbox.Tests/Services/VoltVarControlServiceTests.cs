using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class VoltVarControlServiceTests
{
    [Fact]
    public void CalculateRequiredKvarForPowerFactor_ImprovingPf_NeedsSupport()
    {
        double result = VoltVarControlService.CalculateRequiredKvarForPowerFactor(1000, 0.8, 0.95);

        Assert.True(result > 0);
    }

    [Fact]
    public void CalculateRequiredKvarForPowerFactor_HigherTargetPf_RequiresMoreKvarReduction()
    {
        double low = VoltVarControlService.CalculateRequiredKvarForPowerFactor(1000, 0.8, 0.9);
        double high = VoltVarControlService.CalculateRequiredKvarForPowerFactor(1000, 0.8, 0.95);

        Assert.True(high > low);
    }

    [Fact]
    public void DetermineMode_LowVoltage_InjectsReactivePower()
    {
        var mode = VoltVarControlService.DetermineMode(0.97);

        Assert.Equal(VoltVarControlService.VoltVarMode.InjectReactive, mode);
    }

    [Fact]
    public void DetermineMode_HighVoltage_AbsorbsReactivePower()
    {
        var mode = VoltVarControlService.DetermineMode(1.03);

        Assert.Equal(VoltVarControlService.VoltVarMode.AbsorbReactive, mode);
    }

    [Fact]
    public void DetermineMode_InsideDeadband_Holds()
    {
        var mode = VoltVarControlService.DetermineMode(1.005);

        Assert.Equal(VoltVarControlService.VoltVarMode.Hold, mode);
    }

    [Fact]
    public void CalculateRequiredKvarForVoltage_LargerError_NeedsMoreKvar()
    {
        double low = VoltVarControlService.CalculateRequiredKvarForVoltage(0.99);
        double high = VoltVarControlService.CalculateRequiredKvarForVoltage(0.95);

        Assert.True(high > low);
    }

    [Fact]
    public void CreateVoltVarPlan_InjectMode_AssignsPositiveKvar()
    {
        var devices = new[]
        {
            new VoltVarControlService.ReactiveDevice { Id = "INV1", MaxInjectKvar = 300, MaxAbsorbKvar = 300, Priority = 1 },
            new VoltVarControlService.ReactiveDevice { Id = "INV2", MaxInjectKvar = 300, MaxAbsorbKvar = 300, Priority = 2 },
        };

        var result = VoltVarControlService.CreateVoltVarPlan(devices, 400, VoltVarControlService.VoltVarMode.InjectReactive);

        Assert.True(result.IsAdequate);
        Assert.Equal(400.0, result.AssignedKvar, 1);
        Assert.True(result.EstimatedVoltageChangePu > 0);
    }

    [Fact]
    public void CreateVoltVarPlan_AbsorbMode_AssignsNegativeKvar()
    {
        var devices = new[]
        {
            new VoltVarControlService.ReactiveDevice { Id = "INV1", MaxInjectKvar = 300, MaxAbsorbKvar = 300, Priority = 1 },
        };

        var result = VoltVarControlService.CreateVoltVarPlan(devices, 200, VoltVarControlService.VoltVarMode.AbsorbReactive);

        Assert.Equal(-200.0, result.AssignedKvar, 1);
        Assert.True(result.EstimatedVoltageChangePu < 0);
    }

    [Fact]
    public void CreateVoltVarPlan_HoldMode_AssignsNothing()
    {
        var result = VoltVarControlService.CreateVoltVarPlan(
            new[] { new VoltVarControlService.ReactiveDevice { Id = "INV1", MaxInjectKvar = 300, MaxAbsorbKvar = 300, Priority = 1 } },
            0,
            VoltVarControlService.VoltVarMode.Hold);

        Assert.True(result.IsAdequate);
        Assert.Equal(0, result.AssignedKvar);
    }

    [Fact]
    public void CreateVoltVarPlan_UnavailableDevice_IsSkipped()
    {
        var devices = new[]
        {
            new VoltVarControlService.ReactiveDevice { Id = "OFF", MaxInjectKvar = 300, MaxAbsorbKvar = 300, Priority = 1, IsAvailable = false },
            new VoltVarControlService.ReactiveDevice { Id = "ON", MaxInjectKvar = 300, MaxAbsorbKvar = 300, Priority = 2 },
        };

        var result = VoltVarControlService.CreateVoltVarPlan(devices, 200, VoltVarControlService.VoltVarMode.InjectReactive);

        Assert.Single(result.Allocations);
        Assert.Equal("ON", result.Allocations.Single().Id);
    }

    [Fact]
    public void CreateVoltVarPlan_InsufficientCapacity_FlagsIssue()
    {
        var devices = new[]
        {
            new VoltVarControlService.ReactiveDevice { Id = "INV1", MaxInjectKvar = 100, MaxAbsorbKvar = 100, Priority = 1 },
        };

        var result = VoltVarControlService.CreateVoltVarPlan(devices, 200, VoltVarControlService.VoltVarMode.InjectReactive);

        Assert.False(result.IsAdequate);
        Assert.Contains("below required", result.Issue, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateVoltVarPlan_PriorityOrder_DispatchesFirstDeviceFirst()
    {
        var devices = new[]
        {
            new VoltVarControlService.ReactiveDevice { Id = "FIRST", MaxInjectKvar = 150, MaxAbsorbKvar = 150, Priority = 1 },
            new VoltVarControlService.ReactiveDevice { Id = "SECOND", MaxInjectKvar = 150, MaxAbsorbKvar = 150, Priority = 2 },
        };

        var result = VoltVarControlService.CreateVoltVarPlan(devices, 200, VoltVarControlService.VoltVarMode.InjectReactive);

        Assert.Equal("FIRST", result.Allocations.First().Id);
        Assert.True(result.Allocations.First().AssignedKvar > 0);
    }
}
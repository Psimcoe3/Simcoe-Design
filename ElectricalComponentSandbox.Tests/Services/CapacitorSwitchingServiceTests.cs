using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class CapacitorSwitchingServiceTests
{
    [Fact]
    public void CalculateCapacitorCurrent_ThreePhase_ReturnsExpectedValue()
    {
        double result = CapacitorSwitchingService.CalculateCapacitorCurrent(300, 480);

        Assert.Equal(360.8, result, 1);
    }

    [Fact]
    public void CalculateCapacitorCurrent_HigherKvar_HigherCurrent()
    {
        double low = CapacitorSwitchingService.CalculateCapacitorCurrent(150, 480);
        double high = CapacitorSwitchingService.CalculateCapacitorCurrent(300, 480);

        Assert.True(high > low);
    }

    [Fact]
    public void EstimateInrushCurrent_BackToBack_IsHigher()
    {
        double single = CapacitorSwitchingService.EstimateInrushCurrent(100, backToBackSwitching: false);
        double backToBack = CapacitorSwitchingService.EstimateInrushCurrent(100, backToBackSwitching: true);

        Assert.True(backToBack > single);
    }

    [Fact]
    public void EstimateInrushCurrent_DetunedReactor_ReducesInrush()
    {
        double plain = CapacitorSwitchingService.EstimateInrushCurrent(100, detunedReactor: false);
        double limited = CapacitorSwitchingService.EstimateInrushCurrent(100, detunedReactor: true);

        Assert.True(limited < plain);
    }

    [Fact]
    public void RecommendSwitchingMethod_SensitiveLoads_UsesZeroCross()
    {
        var method = CapacitorSwitchingService.RecommendSwitchingMethod(150, 480, sensitiveToTransients: true);

        Assert.Equal(CapacitorSwitchingService.SwitchingMethod.ZeroCrossSwitch, method);
    }

    [Fact]
    public void RecommendSwitchingMethod_MediumVoltage_UsesVacuumBreaker()
    {
        var method = CapacitorSwitchingService.RecommendSwitchingMethod(600, 4160);

        Assert.Equal(CapacitorSwitchingService.SwitchingMethod.VacuumBreaker, method);
    }

    [Fact]
    public void RecommendSwitchingMethod_BackToBack_UsesVacuumContactor()
    {
        var method = CapacitorSwitchingService.RecommendSwitchingMethod(150, 480, backToBackSwitching: true);

        Assert.Equal(CapacitorSwitchingService.SwitchingMethod.VacuumContactor, method);
    }

    [Fact]
    public void CreateStepPlan_UsesRecommendedStepCountByDefault()
    {
        var result = CapacitorSwitchingService.CreateStepPlan(150, 480);

        Assert.Equal(4, result.StepCount);
    }

    [Fact]
    public void CreateStepPlan_CustomStepCount_IsRespected()
    {
        var result = CapacitorSwitchingService.CreateStepPlan(150, 480, stepCount: 3);

        Assert.Equal(3, result.StepCount);
    }

    [Fact]
    public void CreateStepPlan_StepKvarTotalsToBank()
    {
        var result = CapacitorSwitchingService.CreateStepPlan(200, 480, stepCount: 4);

        Assert.Equal(200.0, result.Steps.Sum(step => step.StepKvar), 1);
    }

    [Fact]
    public void CreateStepPlan_StepCurrentsArePositive()
    {
        var result = CapacitorSwitchingService.CreateStepPlan(200, 480, stepCount: 4);

        Assert.All(result.Steps, step => Assert.True(step.StepCurrentAmps > 0));
    }

    [Fact]
    public void CreateStepPlan_MethodMatchesInputs()
    {
        var result = CapacitorSwitchingService.CreateStepPlan(150, 480, backToBackSwitching: true);

        Assert.Equal(CapacitorSwitchingService.SwitchingMethod.VacuumContactor, result.Method);
    }

    [Fact]
    public void CreateStepPlan_EstimatedInrushIsPositive()
    {
        var result = CapacitorSwitchingService.CreateStepPlan(150, 480);

        Assert.True(result.EstimatedPeakInrushAmps > 0);
    }
}
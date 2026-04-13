using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class LoadBankTestServiceTests
{
    [Fact]
    public void GetMinimumExerciseLoadPercent_DefaultIsThirty()
    {
        Assert.Equal(30, LoadBankTestService.GetMinimumExerciseLoadPercent());
    }

    [Fact]
    public void GetMinimumExerciseLoadPercent_WetStackingConcernRaisesThreshold()
    {
        Assert.Equal(50, LoadBankTestService.GetMinimumExerciseLoadPercent(wetStackingConcern: true));
    }

    [Fact]
    public void CreateMonthlyExercisePlan_BuildingLoadAboveThreshold_NoLoadBankRequired()
    {
        var result = LoadBankTestService.CreateMonthlyExercisePlan(500, 200);

        Assert.False(result.RequiresLoadBank);
        Assert.Equal(0, result.SupplementalLoadBankKW);
    }

    [Fact]
    public void CreateMonthlyExercisePlan_BuildingLoadBelowThreshold_RequiresSupplementalLoad()
    {
        var result = LoadBankTestService.CreateMonthlyExercisePlan(500, 100);

        Assert.True(result.RequiresLoadBank);
        Assert.Equal(50.0, result.SupplementalLoadBankKW);
    }

    [Fact]
    public void CreateMonthlyExercisePlan_WetStackingConcern_IncreasesSupplementalLoad()
    {
        var normal = LoadBankTestService.CreateMonthlyExercisePlan(500, 100);
        var highTemp = LoadBankTestService.CreateMonthlyExercisePlan(500, 100, wetStackingConcern: true);

        Assert.True(highTemp.SupplementalLoadBankKW > normal.SupplementalLoadBankKW);
    }

    [Fact]
    public void CreateAcceptanceTestPlan_HasFourSteps()
    {
        var steps = LoadBankTestService.CreateAcceptanceTestPlan(1000);

        Assert.Equal(4, steps.Count);
    }

    [Fact]
    public void CreateAcceptanceTestPlan_TotalDuration_IsThreeHours()
    {
        var steps = LoadBankTestService.CreateAcceptanceTestPlan(1000);

        Assert.Equal(180, steps.Sum(step => step.DurationMinutes));
    }

    [Fact]
    public void CreateAcceptanceTestPlan_LoadKWScalesWithRating()
    {
        var steps = LoadBankTestService.CreateAcceptanceTestPlan(800);

        Assert.Equal(200.0, steps[0].LoadKW);
        Assert.Equal(800.0, steps[3].LoadKW);
    }

    [Fact]
    public void EvaluateTestResults_AcceptablePerformance_Passes()
    {
        var result = LoadBankTestService.EvaluateTestResults(500, 200, 8, 5);

        Assert.True(result.Passed);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void EvaluateTestResults_LowLoad_Fails()
    {
        var result = LoadBankTestService.EvaluateTestResults(500, 100, 8, 5);

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, issue => issue.Contains("minimum", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateTestResults_ExcessVoltageDip_Fails()
    {
        var result = LoadBankTestService.EvaluateTestResults(500, 200, 18, 5);

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, issue => issue.Contains("Voltage", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateTestResults_ExcessFrequencyDip_Fails()
    {
        var result = LoadBankTestService.EvaluateTestResults(500, 200, 8, 12);

        Assert.False(result.Passed);
        Assert.Contains(result.Issues, issue => issue.Contains("Frequency", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EstimateFuelUsed_HigherLoad_UsesMoreFuel()
    {
        double low = LoadBankTestService.EstimateFuelUsed(500, 25, 1);
        double high = LoadBankTestService.EstimateFuelUsed(500, 75, 1);

        Assert.True(high > low);
    }

    [Fact]
    public void EstimateFuelUsed_LongerDuration_UsesMoreFuel()
    {
        double shortRun = LoadBankTestService.EstimateFuelUsed(500, 50, 1);
        double longRun = LoadBankTestService.EstimateFuelUsed(500, 50, 2);

        Assert.True(longRun > shortRun);
    }
}
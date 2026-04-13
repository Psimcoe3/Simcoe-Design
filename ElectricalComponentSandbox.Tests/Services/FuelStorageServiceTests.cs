using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class FuelStorageServiceTests
{
    [Theory]
    [InlineData(FuelStorageService.EpssClass.Class2, 2)]
    [InlineData(FuelStorageService.EpssClass.Class48, 48)]
    [InlineData(FuelStorageService.EpssClass.Class96, 96)]
    public void GetMinimumRuntimeHours_ReturnsExpectedClassHours(
        FuelStorageService.EpssClass epssClass,
        double expectedHours)
    {
        Assert.Equal(expectedHours, FuelStorageService.GetMinimumRuntimeHours(epssClass));
    }

    [Fact]
    public void EstimateConsumption_HigherLoadFactor_IncreasesBurnRate()
    {
        var low = FuelStorageService.EstimateConsumption(500, 0.5);
        var high = FuelStorageService.EstimateConsumption(500, 0.75);

        Assert.True(high.GallonsPerHour > low.GallonsPerHour);
    }

    [Fact]
    public void EstimateConsumption_PropaneBurnsMoreThanDiesel()
    {
        var diesel = FuelStorageService.EstimateConsumption(300, 0.75, FuelStorageService.FuelType.Diesel);
        var propane = FuelStorageService.EstimateConsumption(300, 0.75, FuelStorageService.FuelType.Propane);

        Assert.True(propane.GallonsPerHour > diesel.GallonsPerHour);
    }

    [Fact]
    public void EstimateConsumption_DieselMatchesTypicalRate()
    {
        var result = FuelStorageService.EstimateConsumption(500, 1.0, FuelStorageService.FuelType.Diesel);

        Assert.Equal(35.5, result.GallonsPerHour, 1);
    }

    [Fact]
    public void SizeTank_Class48_IncludesMarginAndUnusableFuel()
    {
        var result = FuelStorageService.SizeTank(500, 0.5, FuelStorageService.EpssClass.Class48);

        Assert.True(result.TotalTankGallons > result.RequiredUsableGallons);
        Assert.True(result.DayTankGallons > 0);
    }

    [Fact]
    public void SizeTank_HigherRuntimeClass_RequiresLargerTank()
    {
        var shortRun = FuelStorageService.SizeTank(300, 0.75, FuelStorageService.EpssClass.Class24);
        var longRun = FuelStorageService.SizeTank(300, 0.75, FuelStorageService.EpssClass.Class72);

        Assert.True(longRun.TotalTankGallons > shortRun.TotalTankGallons);
    }

    [Fact]
    public void SizeTank_HigherLoadFactor_RequiresLargerTank()
    {
        var low = FuelStorageService.SizeTank(300, 0.5, FuelStorageService.EpssClass.Class24);
        var high = FuelStorageService.SizeTank(300, 0.9, FuelStorageService.EpssClass.Class24);

        Assert.True(high.TotalTankGallons > low.TotalTankGallons);
    }

    [Fact]
    public void SizeTank_DayTankCoversSeveralHoursOfBurn()
    {
        var result = FuelStorageService.SizeTank(400, 0.75, FuelStorageService.EpssClass.Class24);

        Assert.True(result.DayTankGallons >= result.BurnRateGallonsPerHour * 4);
    }

    [Fact]
    public void PlanRefills_NoRefillNeeded_WhenTankIsLargeEnough()
    {
        var plan = FuelStorageService.PlanRefills(1000, 20, 20);

        Assert.False(plan.NeedsMidEventRefill);
        Assert.Equal(0, plan.RequiredDeliveries);
    }

    [Fact]
    public void PlanRefills_LongEvent_RequiresRefill()
    {
        var plan = FuelStorageService.PlanRefills(500, 20, 40);

        Assert.True(plan.NeedsMidEventRefill);
        Assert.True(plan.RequiredDeliveries > 0);
    }

    [Fact]
    public void PlanRefills_DeliveryInterval_IsPositiveWhenNeeded()
    {
        var plan = FuelStorageService.PlanRefills(500, 20, 40);

        Assert.True(plan.DeliveryIntervalHours > 0);
    }

    [Fact]
    public void PlanRefills_RemainingFuel_IsNonNegative()
    {
        var plan = FuelStorageService.PlanRefills(500, 20, 40);

        Assert.True(plan.RemainingGallonsAfterEvent >= 0);
    }
}
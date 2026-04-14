using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class RecloserCoordinationServiceTests
{
    [Fact]
    public void GetDefaultSettings_FuseSaving_HasFastAndSlowShots()
    {
        var result = RecloserCoordinationService.GetDefaultSettings(RecloserCoordinationService.RecloserMode.FuseSaving);

        Assert.Equal(2, result.FastShots);
        Assert.Equal(2, result.SlowShots);
    }

    [Fact]
    public void GetDefaultSettings_FuseBlowing_HasOnlySlowShots()
    {
        var result = RecloserCoordinationService.GetDefaultSettings(RecloserCoordinationService.RecloserMode.FuseBlowing);

        Assert.Equal(0, result.FastShots);
        Assert.True(result.SlowShots > 0);
    }

    [Fact]
    public void EstimateTripTimeMs_FastOperation_IsFasterThanSlow()
    {
        var settings = RecloserCoordinationService.GetDefaultSettings(RecloserCoordinationService.RecloserMode.FuseSaving);
        double fast = RecloserCoordinationService.EstimateTripTimeMs(settings, 2000, true);
        double slow = RecloserCoordinationService.EstimateTripTimeMs(settings, 2000, false);

        Assert.True(fast < slow);
    }

    [Fact]
    public void EstimateTripTimeMs_HigherFault_FasterTrip()
    {
        var settings = RecloserCoordinationService.GetDefaultSettings(RecloserCoordinationService.RecloserMode.FuseSaving);
        double low = RecloserCoordinationService.EstimateTripTimeMs(settings, 800, true);
        double high = RecloserCoordinationService.EstimateTripTimeMs(settings, 2000, true);

        Assert.True(high < low);
    }

    [Fact]
    public void EstimateFuseMeltTimeMs_HigherFault_FasterMelt()
    {
        double low = RecloserCoordinationService.EstimateFuseMeltTimeMs(100, 300);
        double high = RecloserCoordinationService.EstimateFuseMeltTimeMs(100, 800);

        Assert.True(high < low);
    }

    [Fact]
    public void CreateOperationSequence_FuseSaving_HasExpectedShotCount()
    {
        var settings = RecloserCoordinationService.GetDefaultSettings(RecloserCoordinationService.RecloserMode.FuseSaving);
        var sequence = RecloserCoordinationService.CreateOperationSequence(settings, 2000);

        Assert.Equal(4, sequence.Count);
    }

    [Fact]
    public void CreateOperationSequence_OrdersFastShotsFirst()
    {
        var settings = RecloserCoordinationService.GetDefaultSettings(RecloserCoordinationService.RecloserMode.FuseSaving);
        var sequence = RecloserCoordinationService.CreateOperationSequence(settings, 2000);

        Assert.True(sequence[0].IsFastShot);
        Assert.True(sequence[1].IsFastShot);
        Assert.False(sequence[2].IsFastShot);
    }

    [Fact]
    public void CreateOperationSequence_IncludesDeadTimes()
    {
        var settings = RecloserCoordinationService.GetDefaultSettings(RecloserCoordinationService.RecloserMode.FuseSaving);
        var sequence = RecloserCoordinationService.CreateOperationSequence(settings, 2000);

        Assert.True(sequence[0].DeadTimeSec > 0);
    }

    [Fact]
    public void EvaluateFuseCoordination_FuseSaving_CanCoordinate()
    {
        var settings = RecloserCoordinationService.GetDefaultSettings(RecloserCoordinationService.RecloserMode.FuseSaving, pickupAmps: 200);
        var result = RecloserCoordinationService.EvaluateFuseCoordination(settings, 100, 2000);

        Assert.True(result.IsCoordinated);
    }

    [Fact]
    public void EvaluateFuseCoordination_FuseSaving_CanFailWhenRecloserTooSlow()
    {
        var settings = new RecloserCoordinationService.RecloserSettings
        {
            PickupAmps = 200,
            Mode = RecloserCoordinationService.RecloserMode.FuseSaving,
            FastShots = 1,
            SlowShots = 2,
            FastCurveMultiplier = 1.2,
            SlowCurveMultiplier = 1.5,
        };
        var result = RecloserCoordinationService.EvaluateFuseCoordination(settings, 100, 2000);

        Assert.False(result.IsCoordinated);
    }

    [Fact]
    public void EvaluateFuseCoordination_FuseBlowing_CanCoordinate()
    {
        var settings = new RecloserCoordinationService.RecloserSettings
        {
            PickupAmps = 200,
            Mode = RecloserCoordinationService.RecloserMode.FuseBlowing,
            FastShots = 0,
            SlowShots = 3,
            FastCurveMultiplier = 0.2,
            SlowCurveMultiplier = 2.0,
        };
        var result = RecloserCoordinationService.EvaluateFuseCoordination(settings, 100, 2000);

        Assert.True(result.IsCoordinated);
    }

    [Fact]
    public void EvaluateFuseCoordination_FuseBlowing_CanFailWhenRecloserTooFast()
    {
        var settings = new RecloserCoordinationService.RecloserSettings
        {
            PickupAmps = 200,
            Mode = RecloserCoordinationService.RecloserMode.FuseBlowing,
            FastShots = 0,
            SlowShots = 3,
            FastCurveMultiplier = 0.2,
            SlowCurveMultiplier = 0.1,
        };
        var result = RecloserCoordinationService.EvaluateFuseCoordination(settings, 100, 2000);

        Assert.False(result.IsCoordinated);
        Assert.Contains("fuse", result.Issue, System.StringComparison.OrdinalIgnoreCase);
    }
}
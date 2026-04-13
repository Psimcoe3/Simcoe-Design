using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ProtectiveRelayServiceTests
{
    private static ProtectiveRelayService.RelaySettings Downstream() => new()
    {
        Id = "R-DS",
        Function = ProtectiveRelayService.RelayFunction.Function51,
        Curve = ProtectiveRelayService.CurveType.VeryInverse,
        CtRatio = 200,
        PickupAmps = 300,
        TimeDial = 1.0,
        InstantaneousAmps = 3000,
    };

    private static ProtectiveRelayService.RelaySettings Upstream() => new()
    {
        Id = "R-US",
        Function = ProtectiveRelayService.RelayFunction.Function51,
        Curve = ProtectiveRelayService.CurveType.VeryInverse,
        CtRatio = 800,
        PickupAmps = 600,
        TimeDial = 3.0,
        InstantaneousAmps = 8000,
    };

    // ── Trip Time Calculation ────────────────────────────────────────────────

    [Fact]
    public void CalculateTripTime_BelowPickup_WillNotTrip()
    {
        var settings = Downstream();
        var result = ProtectiveRelayService.CalculateTripTime(settings, 200); // Below 300A pickup

        Assert.False(result.WillTrip);
        Assert.Equal(double.PositiveInfinity, result.TripTimeSec);
    }

    [Fact]
    public void CalculateTripTime_AbovePickup_WillTrip()
    {
        var settings = Downstream();
        var result = ProtectiveRelayService.CalculateTripTime(settings, 1200); // 4× pickup

        Assert.True(result.WillTrip);
        Assert.True(result.TripTimeSec > 0 && result.TripTimeSec < 10);
        Assert.Equal(4.0, result.Multiple);
    }

    [Fact]
    public void CalculateTripTime_HigherFault_FasterTrip()
    {
        var settings = Downstream();
        var r1 = ProtectiveRelayService.CalculateTripTime(settings, 600);  // 2× pickup
        var r2 = ProtectiveRelayService.CalculateTripTime(settings, 1500); // 5× pickup

        Assert.True(r2.TripTimeSec < r1.TripTimeSec);
    }

    [Fact]
    public void CalculateTripTime_Instantaneous_FastTrip()
    {
        var settings = Downstream();
        var result = ProtectiveRelayService.CalculateTripTime(settings, 5000); // Above 3000A instantaneous

        Assert.True(result.WillTrip);
        Assert.Equal(0.05, result.TripTimeSec); // ~3 cycles
    }

    [Fact]
    public void CalculateTripTime_DefiniteTime_UsesTimeDial()
    {
        var settings = new ProtectiveRelayService.RelaySettings
        {
            Curve = ProtectiveRelayService.CurveType.DefiniteTime,
            PickupAmps = 100,
            TimeDial = 0.5,
        };
        var result = ProtectiveRelayService.CalculateTripTime(settings, 200);

        Assert.Equal(0.5, result.TripTimeSec);
    }

    [Theory]
    [InlineData(ProtectiveRelayService.CurveType.ModeratelyInverse)]
    [InlineData(ProtectiveRelayService.CurveType.VeryInverse)]
    [InlineData(ProtectiveRelayService.CurveType.ExtremelyInverse)]
    public void CalculateTripTime_AllCurves_ReturnPositive(ProtectiveRelayService.CurveType curve)
    {
        var settings = new ProtectiveRelayService.RelaySettings
        {
            Curve = curve,
            PickupAmps = 200,
            TimeDial = 1.0,
        };
        var result = ProtectiveRelayService.CalculateTripTime(settings, 600);

        Assert.True(result.WillTrip);
        Assert.True(result.TripTimeSec > 0);
    }

    [Fact]
    public void CalculateTripTime_HigherTimeDial_SlowerTrip()
    {
        var fast = Downstream() with { TimeDial = 0.5 };
        var slow = Downstream() with { TimeDial = 3.0 };

        var r1 = ProtectiveRelayService.CalculateTripTime(fast, 900);
        var r2 = ProtectiveRelayService.CalculateTripTime(slow, 900);

        Assert.True(r2.TripTimeSec > r1.TripTimeSec);
    }

    // ── Coordination ─────────────────────────────────────────────────────────

    [Fact]
    public void CheckCoordination_ProperSettings_IsCoordinated()
    {
        var result = ProtectiveRelayService.CheckCoordination(
            Upstream(), Downstream(), 2500);

        Assert.True(result.UpstreamTripSec > result.DownstreamTripSec);
        Assert.True(result.CoordinationMarginSec >= 0.3);
        Assert.True(result.IsCoordinated);
    }

    [Fact]
    public void CheckCoordination_SameSettings_NotCoordinated()
    {
        var relay = Downstream();
        var result = ProtectiveRelayService.CheckCoordination(relay, relay, 1200);

        Assert.Equal(0, result.CoordinationMarginSec, 2);
        Assert.False(result.IsCoordinated);
    }

    [Fact]
    public void CheckCoordination_CustomCTI()
    {
        var result = ProtectiveRelayService.CheckCoordination(
            Upstream(), Downstream(), 2500, minimumCtiSec: 0.5);

        // May or may not coordinate with tighter CTI
        Assert.True(result.CoordinationMarginSec > 0);
    }

    // ── Pickup Recommendation ────────────────────────────────────────────────

    [Fact]
    public void RecommendPickup_Phase_150PercentLoad()
    {
        var result = ProtectiveRelayService.RecommendPickup(
            400, 200, ProtectiveRelayService.RelayFunction.Function51);

        Assert.Equal(600, result.RecommendedPickupAmps);
        Assert.Equal(1.5, result.PickupMultiple);
    }

    [Fact]
    public void RecommendPickup_Instantaneous_6xLoad()
    {
        var result = ProtectiveRelayService.RecommendPickup(
            400, 200, ProtectiveRelayService.RelayFunction.Function50);

        Assert.Equal(2400, result.RecommendedPickupAmps);
    }

    [Fact]
    public void RecommendPickup_GroundFault_30PercentLoad()
    {
        var result = ProtectiveRelayService.RecommendPickup(
            400, 200, ProtectiveRelayService.RelayFunction.Function51N);

        Assert.Equal(120, result.RecommendedPickupAmps);
    }

    [Fact]
    public void RecommendPickup_CalculatesSecondaryPickup()
    {
        var result = ProtectiveRelayService.RecommendPickup(
            400, 200, ProtectiveRelayService.RelayFunction.Function51);

        // 600A / 200 CT × 5A secondary = 15A
        Assert.Equal(15, result.PickupInCTSecondary, 2);
    }

    // ── CT Ratio ─────────────────────────────────────────────────────────────

    [Fact]
    public void SelectCtRatio_ReturnsNextStandard()
    {
        // 400A × 1.25 = 500A → 500 CT
        double ratio = ProtectiveRelayService.SelectCtRatio(400);
        Assert.Equal(500, ratio);
    }

    [Fact]
    public void SelectCtRatio_SmallLoad_SmallCT()
    {
        double ratio = ProtectiveRelayService.SelectCtRatio(30);
        // 30 × 1.25 = 37.5 → 50 CT
        Assert.Equal(50, ratio);
    }
}

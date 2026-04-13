using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// IEEE C37 protective relay coordination settings.
/// Covers overcurrent relay (50/51) pickup, time dial, and coordination.
/// </summary>
public static class ProtectiveRelayService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum RelayFunction
    {
        Function50,  // Instantaneous overcurrent
        Function51,  // Time overcurrent
        Function50N, // Instantaneous ground fault
        Function51N, // Time overcurrent ground fault
    }

    public enum CurveType
    {
        ModeratelyInverse,   // IEEE C1
        VeryInverse,         // IEEE C2
        ExtremelyInverse,    // IEEE C3
        DefiniteTime,
    }

    public record RelaySettings
    {
        public string Id { get; init; } = "";
        public RelayFunction Function { get; init; } = RelayFunction.Function51;
        public CurveType Curve { get; init; } = CurveType.VeryInverse;
        public double CtRatio { get; init; } = 200;
        public double PickupAmps { get; init; }
        public double TimeDial { get; init; } = 1.0;
        public double InstantaneousAmps { get; init; }
    }

    public record TripTimeResult
    {
        public double FaultCurrentAmps { get; init; }
        public double Multiple { get; init; }
        public double TripTimeSec { get; init; }
        public bool WillTrip { get; init; }
    }

    public record CoordinationResult
    {
        public string UpstreamId { get; init; } = "";
        public string DownstreamId { get; init; } = "";
        public double FaultCurrentAmps { get; init; }
        public double DownstreamTripSec { get; init; }
        public double UpstreamTripSec { get; init; }
        public double CoordinationMarginSec { get; init; }
        public bool IsCoordinated { get; init; }
    }

    public record PickupRecommendation
    {
        public double MaxLoadAmps { get; init; }
        public double RecommendedPickupAmps { get; init; }
        public double PickupMultiple { get; init; }
        public double CtRatio { get; init; }
        public double PickupInCTSecondary { get; init; }
    }

    // ── IEEE C37.112 Time-Current Curves ─────────────────────────────────────

    // IEEE C37.112 standard curve constants: t = TDS × (A / (M^p - 1) + B)
    private static (double A, double B, double P) GetCurveConstants(CurveType curve) => curve switch
    {
        CurveType.ModeratelyInverse => (0.0515, 0.1140, 0.02),
        CurveType.VeryInverse => (19.61, 0.491, 2.0),
        CurveType.ExtremelyInverse => (28.2, 0.1217, 2.0),
        CurveType.DefiniteTime => (0, 0, 0), // Special case
        _ => (19.61, 0.491, 2.0),
    };

    /// <summary>
    /// Calculates trip time for a relay given fault current.
    /// Uses IEEE C37.112 standard inverse-time curves.
    /// </summary>
    public static TripTimeResult CalculateTripTime(RelaySettings settings, double faultCurrentAmps)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (faultCurrentAmps <= 0) throw new ArgumentException("Fault current must be positive.");

        double multiple = faultCurrentAmps / settings.PickupAmps;

        // Check instantaneous element (Function 50)
        if (settings.InstantaneousAmps > 0 && faultCurrentAmps >= settings.InstantaneousAmps)
        {
            return new TripTimeResult
            {
                FaultCurrentAmps = faultCurrentAmps,
                Multiple = Math.Round(multiple, 2),
                TripTimeSec = 0.05, // Instantaneous ≈ 3 cycles at 60Hz
                WillTrip = true,
            };
        }

        if (multiple <= 1.0)
        {
            return new TripTimeResult
            {
                FaultCurrentAmps = faultCurrentAmps,
                Multiple = Math.Round(multiple, 2),
                TripTimeSec = double.PositiveInfinity,
                WillTrip = false,
            };
        }

        double tripTime;
        if (settings.Curve == CurveType.DefiniteTime)
        {
            tripTime = settings.TimeDial; // TDS directly = trip time in seconds
        }
        else
        {
            var (a, b, p) = GetCurveConstants(settings.Curve);
            tripTime = settings.TimeDial * (a / (Math.Pow(multiple, p) - 1.0) + b);
        }

        return new TripTimeResult
        {
            FaultCurrentAmps = faultCurrentAmps,
            Multiple = Math.Round(multiple, 2),
            TripTimeSec = Math.Round(Math.Max(tripTime, 0.01), 4),
            WillTrip = true,
        };
    }

    // ── Coordination Check ───────────────────────────────────────────────────

    /// <summary>
    /// Checks coordination between upstream and downstream relays.
    /// IEEE C37.112 recommends minimum 0.3s CTI (coordination time interval).
    /// </summary>
    public static CoordinationResult CheckCoordination(
        RelaySettings upstream,
        RelaySettings downstream,
        double faultCurrentAmps,
        double minimumCtiSec = 0.3)
    {
        ArgumentNullException.ThrowIfNull(upstream);
        ArgumentNullException.ThrowIfNull(downstream);

        var dsTripResult = CalculateTripTime(downstream, faultCurrentAmps);
        var usTripResult = CalculateTripTime(upstream, faultCurrentAmps);

        double margin = usTripResult.TripTimeSec - dsTripResult.TripTimeSec;

        return new CoordinationResult
        {
            UpstreamId = upstream.Id,
            DownstreamId = downstream.Id,
            FaultCurrentAmps = faultCurrentAmps,
            DownstreamTripSec = dsTripResult.TripTimeSec,
            UpstreamTripSec = usTripResult.TripTimeSec,
            CoordinationMarginSec = Math.Round(margin, 4),
            IsCoordinated = margin >= minimumCtiSec,
        };
    }

    // ── Pickup Recommendation ─────────────────────────────────────────────────

    /// <summary>
    /// Recommends relay pickup setting based on maximum expected load.
    /// Typical pickup: 1.25–1.5× max load for phase; 0.2–0.4× for ground.
    /// </summary>
    public static PickupRecommendation RecommendPickup(
        double maxLoadAmps,
        double ctRatio,
        RelayFunction function)
    {
        if (maxLoadAmps <= 0) throw new ArgumentException("Max load must be positive.");
        if (ctRatio <= 0) throw new ArgumentException("CT ratio must be positive.");

        double pickupMultiple = function switch
        {
            RelayFunction.Function51 => 1.5,  // 150% of max load
            RelayFunction.Function50 => 6.0,  // 6× max load for instantaneous
            RelayFunction.Function51N => 0.3,  // 30% of max load for ground
            RelayFunction.Function50N => 1.5,  // 1.5× max load for ground instant
            _ => 1.5,
        };

        double pickupAmps = maxLoadAmps * pickupMultiple;
        double pickupSecondary = pickupAmps / ctRatio * 5.0; // 5A secondary CT

        return new PickupRecommendation
        {
            MaxLoadAmps = Math.Round(maxLoadAmps, 2),
            RecommendedPickupAmps = Math.Round(pickupAmps, 2),
            PickupMultiple = pickupMultiple,
            CtRatio = ctRatio,
            PickupInCTSecondary = Math.Round(pickupSecondary, 3),
        };
    }

    // ── Standard CT Ratios ───────────────────────────────────────────────────

    private static readonly double[] StandardCtRatios =
    {
        50, 75, 100, 150, 200, 250, 300, 400, 500, 600, 800, 1000,
        1200, 1500, 2000, 2500, 3000, 4000, 5000,
    };

    /// <summary>
    /// Selects the next standard CT ratio that fully covers the maximum load.
    /// CT primary should be ≥ max expected load × 1.25 for headroom.
    /// </summary>
    public static double SelectCtRatio(double maxLoadAmps)
    {
        if (maxLoadAmps <= 0) throw new ArgumentException("Max load must be positive.");

        double target = maxLoadAmps * 1.25;
        return StandardCtRatios.FirstOrDefault(r => r >= target);
    }
}

using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// NEC 240.12 / 700.32 — Selective coordination analysis for overcurrent protective devices.
/// Ensures upstream devices trip before downstream devices for the same fault,
/// providing selectivity (only the nearest upstream device operates).
///
/// Required by NEC for:
/// - Emergency systems (NEC 700.32)
/// - Legally required standby (NEC 701.27)
/// - Critical operations power systems (NEC 708.54)
/// - Elevator circuits (NEC 620)
///
/// Analysis uses time-current characteristic (TCC) comparison:
/// - Each OCPD has a trip time at a given current
/// - Upstream device must have longer trip time than downstream at all fault currents
/// - Minimum ratio for selective coordination varies by device type
/// </summary>
public static class SelectiveCoordinationService
{
    /// <summary>
    /// Minimum trip-time ratio (upstream/downstream) for selective coordination.
    /// IEEE/NEC guidance: upstream device should be at least this many times slower.
    /// </summary>
    public const double MinCoordinationRatio = 2.0;

    /// <summary>
    /// Represents an overcurrent protective device in the coordination study.
    /// </summary>
    public record ProtectiveDevice
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public int TripRatingAmps { get; init; }

        /// <summary>Interrupting capacity in kA.</summary>
        public double AICRatingKA { get; init; } = 10.0;

        /// <summary>Device type affects coordination characteristics.</summary>
        public OCPDType DeviceType { get; init; } = OCPDType.MoldedCaseBreaker;

        /// <summary>Optional short-time delay setting in cycles (0 = instantaneous).</summary>
        public int ShortTimeDelayCycles { get; init; }

        /// <summary>Optional adjustable instantaneous trip multiplier (times rating).</summary>
        public double InstantaneousTripMultiplier { get; init; } = 10.0;
    }

    public enum OCPDType
    {
        Fuse,
        MoldedCaseBreaker,
        InsulatedCaseBreaker,
        PowerCircuitBreaker,
    }

    /// <summary>
    /// A pair of devices in series evaluated for coordination.
    /// </summary>
    public record CoordinationPair
    {
        public ProtectiveDevice Upstream { get; init; } = null!;
        public ProtectiveDevice Downstream { get; init; } = null!;
        public double FaultCurrentKA { get; init; }
        public bool IsCoordinated { get; init; }
        public double UpstreamTripTimeMs { get; init; }
        public double DownstreamTripTimeMs { get; init; }
        public double Ratio { get; init; }
        public string? Issue { get; init; }
    }

    /// <summary>
    /// Estimates trip time in milliseconds for a given device at a given fault current.
    /// Uses simplified inverse-time characteristic: t = k / (I/Ir)^2
    /// where Ir is the device rating and k is a constant based on device type.
    /// </summary>
    public static double EstimateTripTimeMs(ProtectiveDevice device, double faultCurrentAmps)
    {
        if (faultCurrentAmps <= 0 || device.TripRatingAmps <= 0) return double.MaxValue;

        double ratio = faultCurrentAmps / device.TripRatingAmps;
        if (ratio <= 1.0) return double.MaxValue; // below trip threshold

        // Instantaneous region
        if (ratio >= device.InstantaneousTripMultiplier)
        {
            return device.DeviceType switch
            {
                OCPDType.Fuse => 4.0, // half-cycle (8.3ms) or less
                OCPDType.MoldedCaseBreaker => 16.7 + (device.ShortTimeDelayCycles * 16.7),
                OCPDType.InsulatedCaseBreaker => 33.3 + (device.ShortTimeDelayCycles * 16.7),
                OCPDType.PowerCircuitBreaker => 50.0 + (device.ShortTimeDelayCycles * 16.7),
                _ => 16.7,
            };
        }

        // Inverse-time region: t = k / (I/Ir - 1)^2
        double k = device.DeviceType switch
        {
            OCPDType.Fuse => 50.0,
            OCPDType.MoldedCaseBreaker => 100.0,
            OCPDType.InsulatedCaseBreaker => 120.0,
            OCPDType.PowerCircuitBreaker => 150.0,
            _ => 100.0,
        };

        double tripTime = k / ((ratio - 1.0) * (ratio - 1.0)) * 1000.0; // seconds → ms
        return Math.Min(tripTime, 100_000); // cap at 100 seconds
    }

    /// <summary>
    /// Evaluates selective coordination between an upstream and downstream device
    /// at a specific fault current level.
    /// </summary>
    public static CoordinationPair EvaluatePair(
        ProtectiveDevice upstream,
        ProtectiveDevice downstream,
        double faultCurrentKA)
    {
        double faultAmps = faultCurrentKA * 1000.0;
        double upstreamTime = EstimateTripTimeMs(upstream, faultAmps);
        double downstreamTime = EstimateTripTimeMs(downstream, faultAmps);

        double ratio = downstreamTime > 0 ? upstreamTime / downstreamTime : 0;
        bool coordinated = ratio >= MinCoordinationRatio;
        string? issue = null;

        if (!coordinated)
        {
            if (upstreamTime <= downstreamTime)
                issue = $"Upstream {upstream.Name} ({upstream.TripRatingAmps}A) trips at same time or faster than downstream {downstream.Name} ({downstream.TripRatingAmps}A) at {faultCurrentKA:F1} kA";
            else
                issue = $"Insufficient time margin between {upstream.Name} and {downstream.Name} at {faultCurrentKA:F1} kA (ratio {ratio:F2}, need ≥ {MinCoordinationRatio:F1})";
        }

        return new CoordinationPair
        {
            Upstream = upstream,
            Downstream = downstream,
            FaultCurrentKA = faultCurrentKA,
            IsCoordinated = coordinated,
            UpstreamTripTimeMs = Math.Round(upstreamTime, 1),
            DownstreamTripTimeMs = Math.Round(downstreamTime, 1),
            Ratio = Math.Round(ratio, 2),
            Issue = issue,
        };
    }

    /// <summary>
    /// Evaluates coordination across multiple fault current levels.
    /// Tests at increments from minimum to available fault current.
    /// Returns the worst-case (minimum ratio) result.
    /// </summary>
    public static CoordinationPair EvaluateAcrossRange(
        ProtectiveDevice upstream,
        ProtectiveDevice downstream,
        double maxFaultCurrentKA,
        int testPoints = 10)
    {
        if (testPoints < 2) testPoints = 2;

        double minFault = Math.Max(downstream.TripRatingAmps * 1.5 / 1000.0, 0.5);
        double step = (maxFaultCurrentKA - minFault) / (testPoints - 1);
        if (step <= 0) step = maxFaultCurrentKA / testPoints;

        CoordinationPair? worst = null;

        for (int i = 0; i < testPoints; i++)
        {
            double faultKA = minFault + (step * i);
            if (faultKA > maxFaultCurrentKA) faultKA = maxFaultCurrentKA;
            if (faultKA <= 0) continue;

            var result = EvaluatePair(upstream, downstream, faultKA);
            if (worst == null || result.Ratio < worst.Ratio)
                worst = result;
        }

        return worst ?? EvaluatePair(upstream, downstream, maxFaultCurrentKA);
    }

    /// <summary>
    /// Walks a distribution tree and evaluates coordination between each parent-child
    /// device pair. Returns all violations found.
    /// </summary>
    public static List<CoordinationPair> AnalyzeTree(
        IEnumerable<DistributionNode> roots,
        Func<DistributionNode, ProtectiveDevice?> getDevice)
    {
        var violations = new List<CoordinationPair>();
        foreach (var root in roots)
        {
            WalkTree(root, null, getDevice, violations);
        }
        return violations;
    }

    private static void WalkTree(
        DistributionNode node,
        ProtectiveDevice? parentDevice,
        Func<DistributionNode, ProtectiveDevice?> getDevice,
        List<CoordinationPair> violations)
    {
        var device = getDevice(node);

        if (parentDevice != null && device != null && node.FaultCurrentKA > 0)
        {
            var result = EvaluateAcrossRange(parentDevice, device, node.FaultCurrentKA);
            if (!result.IsCoordinated)
                violations.Add(result);
        }

        foreach (var child in node.Children)
        {
            WalkTree(child, device ?? parentDevice, getDevice, violations);
        }
    }
}

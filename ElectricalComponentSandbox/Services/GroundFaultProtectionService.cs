using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Ground fault protection of equipment (GFPE) per NEC 230.95 and 215.10.
/// Covers sizing, two-level (main/feeder) coordination, and zone-selective interlocking.
/// </summary>
public static class GroundFaultProtectionService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum GfpeLevel { Main, Feeder }

    public enum SystemVoltageClass
    {
        /// <summary>480Y/277V wye-connected (GFPE required per 230.95).</summary>
        V480Y277,
        /// <summary>208Y/120V wye-connected (GFPE not required by NEC but often specified).</summary>
        V208Y120,
        /// <summary>Other — evaluate per AHJ.</summary>
        Other,
    }

    /// <summary>A ground fault protection device.</summary>
    public record GfpeDevice
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public GfpeLevel Level { get; init; }
        public double PickupAmps { get; init; }
        public double TripDelaySeconds { get; init; }
        public double EquipmentAmps { get; init; }
    }

    /// <summary>Result of NEC 230.95 applicability check.</summary>
    public record GfpeRequirementResult
    {
        public bool Required { get; init; }
        public string Reason { get; init; } = "";
        public double ServiceAmps { get; init; }
        public SystemVoltageClass VoltageClass { get; init; }
    }

    /// <summary>Result of sizing a GFPE device.</summary>
    public record GfpeSizingResult
    {
        public GfpeLevel Level { get; init; }
        public double MaxPickupAmps { get; init; }
        public double MaxDelaySeconds { get; init; }
        public double RecommendedPickupAmps { get; init; }
        public double RecommendedDelaySeconds { get; init; }
    }

    /// <summary>Two-level coordination analysis result.</summary>
    public record CoordinationResult
    {
        public bool IsCoordinated { get; init; }
        public double MainPickupAmps { get; init; }
        public double MainDelaySeconds { get; init; }
        public double FeederPickupAmps { get; init; }
        public double FeederDelaySeconds { get; init; }
        public double PickupRatio { get; init; }
        public double DelayMarginSeconds { get; init; }
        public List<string> Violations { get; init; } = new();
    }

    /// <summary>Zone-selective interlocking (ZSI) analysis result.</summary>
    public record ZsiResult
    {
        public bool ZsiRecommended { get; init; }
        public double UnrestrainedTripTimeSeconds { get; init; }
        public double RestrainedTripTimeSeconds { get; init; }
        public double ArcEnergyReductionPercent { get; init; }
    }

    // ── NEC 230.95 Applicability ─────────────────────────────────────────────

    /// <summary>
    /// Determines if GFPE is required per NEC 230.95:
    /// Solidly-grounded wye services > 150V to ground, ≥ 1000A disconnect.
    /// </summary>
    public static GfpeRequirementResult CheckRequirement(
        double serviceDisconnectAmps, SystemVoltageClass voltageClass)
    {
        bool required = voltageClass == SystemVoltageClass.V480Y277 &&
                        serviceDisconnectAmps >= 1000;

        string reason = required
            ? "NEC 230.95: GFPE required for solidly-grounded wye service >150V to ground with disconnect ≥1000A."
            : serviceDisconnectAmps < 1000
                ? $"Service disconnect ({serviceDisconnectAmps}A) is below 1000A threshold."
                : $"Voltage class {voltageClass} is not solidly-grounded wye >150V to ground.";

        return new GfpeRequirementResult
        {
            Required = required,
            Reason = reason,
            ServiceAmps = serviceDisconnectAmps,
            VoltageClass = voltageClass,
        };
    }

    // ── GFPE Sizing ──────────────────────────────────────────────────────────

    /// <summary>
    /// Sizes GFPE per NEC 230.95(A):
    /// Main: max 1200A pickup, max 1.0 second delay (including clearing time).
    /// Feeder (NEC 215.10): max 1200A pickup for ≥1000A feeders; lower pickup typical.
    /// </summary>
    public static GfpeSizingResult SizeDevice(
        GfpeLevel level, double equipmentAmps)
    {
        // NEC 230.95(A): Max setting 1200A, max 1 second at ≥3000A
        double maxPickup = 1200;
        double maxDelay = 1.0;

        double recommendedPickup;
        double recommendedDelay;

        if (level == GfpeLevel.Main)
        {
            // Main: typically set at 1200A with 0.5–1.0 sec delay
            recommendedPickup = Math.Min(1200, Math.Max(equipmentAmps * 0.2, 100));
            recommendedDelay = 0.5;
        }
        else
        {
            // Feeder: lower pickup, shorter delay for coordination
            recommendedPickup = Math.Min(1200, Math.Max(equipmentAmps * 0.15, 50));
            recommendedDelay = 0.1;
        }

        return new GfpeSizingResult
        {
            Level = level,
            MaxPickupAmps = maxPickup,
            MaxDelaySeconds = maxDelay,
            RecommendedPickupAmps = Math.Round(recommendedPickup, 0),
            RecommendedDelaySeconds = recommendedDelay,
        };
    }

    // ── Two-Level Coordination ───────────────────────────────────────────────

    /// <summary>
    /// Evaluates two-level GFPE coordination per NEC 230.95(C) / 517.17.
    /// For coordination: main pickup > feeder pickup, main delay > feeder delay + margin.
    /// Minimum 6-cycle (0.1s) separation per industry practice.
    /// </summary>
    public static CoordinationResult EvaluateCoordination(
        GfpeDevice main, GfpeDevice feeder,
        double minimumDelayMarginSeconds = 0.1)
    {
        var violations = new List<string>();

        if (main.Level != GfpeLevel.Main)
            violations.Add("Upstream device must be Main level.");
        if (feeder.Level != GfpeLevel.Feeder)
            violations.Add("Downstream device must be Feeder level.");

        double pickupRatio = feeder.PickupAmps > 0
            ? main.PickupAmps / feeder.PickupAmps
            : 0;

        double delayMargin = main.TripDelaySeconds - feeder.TripDelaySeconds;

        if (main.PickupAmps <= feeder.PickupAmps)
            violations.Add($"Main pickup ({main.PickupAmps}A) must exceed feeder pickup ({feeder.PickupAmps}A).");

        if (delayMargin < minimumDelayMarginSeconds)
            violations.Add($"Delay margin ({delayMargin:F2}s) is less than required minimum ({minimumDelayMarginSeconds}s).");

        if (main.TripDelaySeconds > 1.0)
            violations.Add($"Main delay ({main.TripDelaySeconds}s) exceeds NEC 230.95(A) max of 1.0s.");

        return new CoordinationResult
        {
            IsCoordinated = violations.Count == 0,
            MainPickupAmps = main.PickupAmps,
            MainDelaySeconds = main.TripDelaySeconds,
            FeederPickupAmps = feeder.PickupAmps,
            FeederDelaySeconds = feeder.TripDelaySeconds,
            PickupRatio = Math.Round(pickupRatio, 2),
            DelayMarginSeconds = Math.Round(delayMargin, 3),
            Violations = violations,
        };
    }

    // ── Zone-Selective Interlocking ──────────────────────────────────────────

    /// <summary>
    /// Analyzes benefit of ZSI: allows upstream device to trip at unrestrained
    /// (fast) time when downstream device does NOT see the fault, reducing arc energy.
    /// </summary>
    public static ZsiResult AnalyzeZsi(
        double mainDelaySeconds, double unrestrainedTripSeconds = 0.05)
    {
        // With ZSI, main trips at unrestrained time (typically 3 cycles / 50ms)
        // when feeder zone does not report the fault
        double reduction = mainDelaySeconds > 0
            ? (1.0 - unrestrainedTripSeconds / mainDelaySeconds) * 100
            : 0;

        return new ZsiResult
        {
            ZsiRecommended = mainDelaySeconds > 0.2,
            UnrestrainedTripTimeSeconds = unrestrainedTripSeconds,
            RestrainedTripTimeSeconds = mainDelaySeconds,
            ArcEnergyReductionPercent = Math.Round(Math.Max(0, reduction), 1),
        };
    }

    /// <summary>
    /// Batch: check GFPE requirement + sizing for a list of services.
    /// Returns one result per service that requires GFPE.
    /// </summary>
    public static List<GfpeSizingResult> SizeAll(
        IEnumerable<(string Id, double Amps, SystemVoltageClass Voltage)> services)
    {
        var results = new List<GfpeSizingResult>();
        foreach (var svc in services)
        {
            var req = CheckRequirement(svc.Amps, svc.Voltage);
            if (req.Required)
                results.Add(SizeDevice(GfpeLevel.Main, svc.Amps));
        }
        return results;
    }
}

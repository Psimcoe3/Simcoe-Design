using System;
using System.Collections.Generic;
using System.Linq;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Motor Control Center (MCC) bucket schedule generation per NEC 430.
/// Produces structured bucket schedules with starter types, MCP sizing,
/// overload relay settings, and total MCC load summaries.
/// </summary>
public static class MotorControlCenterScheduleService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum StarterType
    {
        FVNR,       // Full-Voltage Non-Reversing
        FVR,        // Full-Voltage Reversing
        WyeDelta,
        SoftStart,
        VFD,
    }

    public enum NemaSize
    {
        Size00 = 0,
        Size0 = 1,
        Size1 = 2,
        Size2 = 3,
        Size3 = 4,
        Size4 = 5,
        Size5 = 6,
    }

    /// <summary>Motor specification for MCC bucket.</summary>
    public record MotorSpec
    {
        public string Id { get; init; } = "";
        public string Description { get; init; } = "";
        public double HP { get; init; }
        public double Voltage { get; init; } = 460;
        public int Phases { get; init; } = 3;
        public double FLA { get; init; }
        public double ServiceFactor { get; init; } = 1.15;
        public StarterType Starter { get; init; } = StarterType.FVNR;
        public bool IsEssential { get; init; }
    }

    /// <summary>A single MCC bucket.</summary>
    public record MccBucket
    {
        public string BucketId { get; init; } = "";
        public string MotorId { get; init; } = "";
        public string Description { get; init; } = "";
        public double HP { get; init; }
        public double FLA { get; init; }
        public StarterType Starter { get; init; }
        public NemaSize StarterSize { get; init; }
        public int McpTripAmps { get; init; }
        public double OverloadTripAmps { get; init; }
        public string RecommendedWireSize { get; init; } = "";
        public double BucketWatts { get; init; }
    }

    /// <summary>Complete MCC schedule.</summary>
    public record MccSchedule
    {
        public string MccId { get; init; } = "";
        public string MccName { get; init; } = "";
        public double Voltage { get; init; } = 460;
        public int BusAmps { get; init; } = 800;
        public List<MccBucket> Buckets { get; init; } = new();
        public int TotalBuckets => Buckets.Count;
        public double TotalFLA => Buckets.Sum(b => b.FLA);
        public double TotalHP => Buckets.Sum(b => b.HP);
        public double TotalWatts => Buckets.Sum(b => b.BucketWatts);
        public double BusUtilizationPercent => BusAmps > 0
            ? Math.Round(TotalFLA / BusAmps * 100, 1) : 0;
    }

    // ── NEMA starter sizing ──────────────────────────────────────────────────
    // NEMA Size → max HP at 460V 3-phase (simplified)
    private static readonly (NemaSize Size, double MaxHP460V, double MaxFLA)[] NemaSizeTable =
    {
        (NemaSize.Size00, 1.5,   5.0),
        (NemaSize.Size0,  3.0,  10.0),
        (NemaSize.Size1,  7.5,  22.0),
        (NemaSize.Size2, 15.0,  45.0),
        (NemaSize.Size3, 30.0,  90.0),
        (NemaSize.Size4, 60.0, 180.0),
        (NemaSize.Size5, 100.0, 270.0),
    };

    // ── Standard MCP trip ratings per NEC 430.52 ─────────────────────────────
    private static readonly int[] StandardMcpTrips =
    {
        3, 7, 15, 30, 50, 70, 100, 150, 200, 250, 300, 400, 500, 600, 700, 800,
    };

    // ── Wire sizing (simplified, copper 75°C) ────────────────────────────────
    private static readonly (double MaxAmps, string Size)[] WireSizes =
    {
        (15,  "14 AWG"), (20,  "12 AWG"), (30,  "10 AWG"),
        (40,  "8 AWG"),  (55,  "6 AWG"),  (70,  "4 AWG"),
        (85,  "3 AWG"),  (95,  "2 AWG"),  (110, "1 AWG"),
        (130, "1/0 AWG"), (150, "2/0 AWG"), (175, "3/0 AWG"),
        (200, "4/0 AWG"), (230, "250 kcmil"), (255, "300 kcmil"),
        (285, "350 kcmil"), (310, "400 kcmil"), (335, "500 kcmil"),
    };

    // ── Public Methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Generates a complete MCC schedule from a list of motor specifications.
    /// </summary>
    public static MccSchedule GenerateSchedule(
        string mccId,
        string mccName,
        IEnumerable<MotorSpec> motors,
        double voltage = 460,
        int busAmps = 800)
    {
        var buckets = new List<MccBucket>();
        int bucketNum = 1;

        foreach (var motor in motors)
        {
            var bucket = SizeBucket(motor, $"B{bucketNum++}");
            buckets.Add(bucket);
        }

        return new MccSchedule
        {
            MccId = mccId,
            MccName = mccName,
            Voltage = voltage,
            BusAmps = busAmps,
            Buckets = buckets,
        };
    }

    /// <summary>
    /// Sizes a single MCC bucket for a motor.
    /// </summary>
    public static MccBucket SizeBucket(MotorSpec motor, string? bucketId = null)
    {
        var nema = SelectNemaSize(motor.FLA, motor.HP);
        int mcp = SizeMCP(motor.FLA);
        double overload = CalculateOverloadTrip(motor.FLA, motor.ServiceFactor);
        string wire = SelectWireSize(motor.FLA * 1.25);
        double watts = motor.HP * 746 / 0.90; // Approximate: HP→watts at ~90% eff

        return new MccBucket
        {
            BucketId = bucketId ?? motor.Id,
            MotorId = motor.Id,
            Description = motor.Description,
            HP = motor.HP,
            FLA = motor.FLA,
            Starter = motor.Starter,
            StarterSize = nema,
            McpTripAmps = mcp,
            OverloadTripAmps = Math.Round(overload, 1),
            RecommendedWireSize = wire,
            BucketWatts = Math.Round(watts, 0),
        };
    }

    /// <summary>
    /// Selects NEMA starter size based on FLA and HP.
    /// </summary>
    public static NemaSize SelectNemaSize(double fla, double hp)
    {
        foreach (var (size, maxHp, maxFla) in NemaSizeTable)
        {
            if (hp <= maxHp && fla <= maxFla)
                return size;
        }
        return NemaSize.Size5; // Largest
    }

    /// <summary>
    /// Sizes Motor Circuit Protector (MCP) per NEC 430.52.
    /// MCP trip = next standard size ≥ FLA × 800% (instantaneous trip breaker).
    /// </summary>
    public static int SizeMCP(double fla)
    {
        double minTrip = fla * 8.0; // 800% for instantaneous-trip MCP
        foreach (int trip in StandardMcpTrips)
        {
            if (trip >= minTrip) return trip;
        }
        return StandardMcpTrips[^1];
    }

    /// <summary>
    /// Calculates overload relay trip setting per NEC 430.32.
    /// SF ≥ 1.15: 125% of FLA. SF &lt; 1.15: 115% of FLA.
    /// </summary>
    public static double CalculateOverloadTrip(double fla, double serviceFactor = 1.15)
    {
        double multiplier = serviceFactor >= 1.15 ? 1.25 : 1.15;
        return fla * multiplier;
    }

    /// <summary>
    /// Selects wire size for the given minimum ampacity.
    /// </summary>
    public static string SelectWireSize(double minAmpacity)
    {
        foreach (var (maxAmps, size) in WireSizes)
        {
            if (maxAmps >= minAmpacity) return size;
        }
        return WireSizes[^1].Size;
    }

    /// <summary>
    /// Calculates the combined feeder ampacity for the MCC per NEC 430.24.
    /// Sum of all motor FLAs + 25% of the largest motor FLA.
    /// </summary>
    public static double CalculateFeederAmpacity(IEnumerable<MotorSpec> motors)
    {
        var list = motors.ToList();
        if (list.Count == 0) return 0;

        double totalFLA = list.Sum(m => m.FLA);
        double largestFLA = list.Max(m => m.FLA);
        return totalFLA + 0.25 * largestFLA;
    }
}

using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Motor load data for NEC 430 calculations.
/// </summary>
public record MotorLoad
{
    public string Id { get; init; } = "";
    public string Description { get; init; } = "";
    public double HP { get; init; }
    public double Voltage { get; init; } = 460;
    public int Phase { get; init; } = 3;
    public double ServiceFactor { get; init; } = 1.15;
    public double? NameplateAmps { get; init; }
}

/// <summary>
/// Result of motor branch circuit sizing per NEC 430.
/// </summary>
public record MotorBranchResult
{
    public string MotorId { get; init; } = "";
    public double FLA { get; init; }
    public double MinWireAmpacity { get; init; }
    public string RecommendedWireSize { get; init; } = "";
    public int MaxOCPDAmpsDualElement { get; init; }
    public int MaxOCPDAmpsInverse { get; init; }
    public double OverloadTripAmps { get; init; }
}

/// <summary>
/// Result of motor feeder sizing per NEC 430.24.
/// </summary>
public record MotorFeederResult
{
    public double TotalFLA { get; init; }
    public double LargestMotorFLA { get; init; }
    public double MinFeederAmpacity { get; init; }
    public string RecommendedWireSize { get; init; } = "";
    public int MotorCount { get; init; }
}

/// <summary>
/// Motor load calculations per NEC Article 430.
/// Full-load current tables, branch circuit sizing, overload protection, and feeder calculations.
/// </summary>
public static class MotorLoadService
{
    // ── NEC Table 430.248 — Single-Phase Motor FLA ──────────────────────────
    // HP → FLA at 115V, 200V, 230V
    private static readonly (double HP, double FLA115, double FLA200, double FLA230)[] Table430_248 =
    {
        (1.0/6,  4.4,  2.5, 2.2),
        (1.0/4,  5.8,  3.3, 2.9),
        (1.0/3,  7.2,  4.1, 3.6),
        (0.5,   9.8,  5.6, 4.9),
        (0.75, 13.8,  7.9, 6.9),
        (1.0,  16.0,  9.2, 8.0),
        (1.5,  20.0, 11.5,10.0),
        (2.0,  24.0, 13.8,12.0),
        (3.0,  34.0, 19.6,17.0),
        (5.0,  56.0, 32.2,28.0),
        (7.5,  80.0, 46.0,40.0),
        (10.0,100.0, 57.5,50.0),
    };

    // ── NEC Table 430.250 — Three-Phase Motor FLA ───────────────────────────
    // HP → FLA at 200V, 208V, 230V, 460V, 575V
    private static readonly (double HP, double FLA200, double FLA208, double FLA230, double FLA460, double FLA575)[] Table430_250 =
    {
        (0.5,   2.0,  1.9,  1.8,  0.9,  0.7),
        (0.75,  2.8,  2.7,  2.6,  1.3,  1.0),
        (1.0,   3.6,  3.5,  3.2,  1.6,  1.3),
        (1.5,   5.2,  5.0,  4.6,  2.3,  1.8),
        (2.0,   6.8,  6.5,  6.0,  3.0,  2.4),
        (3.0,   9.6,  9.2,  8.4,  4.2,  3.4),
        (5.0,  15.2, 14.6, 13.2,  6.6,  5.3),
        (7.5,  22.0, 21.1, 19.2,  9.6,  7.7),
        (10.0, 28.0, 26.9, 24.0, 12.0,  9.6),
        (15.0, 42.0, 40.3, 36.0, 18.0, 14.4),
        (20.0, 54.0, 51.8, 46.0, 23.0, 18.4),
        (25.0, 68.0, 65.3, 58.0, 29.0, 23.0),
        (30.0, 80.0, 76.8, 68.0, 34.0, 27.0),
        (40.0,104.0, 99.8, 88.0, 44.0, 35.0),
        (50.0,130.0,124.8,110.0, 55.0, 44.0),
        (60.0,154.0,147.8,130.0, 65.0, 52.0),
        (75.0,192.0,184.3,162.0, 81.0, 65.0),
        (100.0,248.0,238.1,210.0,105.0, 84.0),
        (125.0,312.0,299.5,264.0,132.0,106.0),
        (150.0,360.0,345.6,300.0,150.0,120.0),
        (200.0,480.0,460.8,396.0,198.0,158.0),
    };

    // ── NEC 430.52 Table — Max OCPD for motor branch circuit protection ─────
    // Type → multiplier of FLA
    // Dual-element (time-delay) fuse: 175%, Inverse-time breaker: 250%
    private const double DualElementFuseMultiplier = 1.75;
    private const double InverseTimeBreakerMultiplier = 2.50;

    // ── Standard OCPD sizes (for rounding up per NEC 430.52) ────────────────
    private static readonly int[] StandardOCPDSizes =
    {
        15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100,
        110, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450,
        500, 600, 700, 800, 1000, 1200, 1600, 2000, 2500, 3000
    };

    /// <summary>
    /// Returns the full-load amperage (FLA) for a motor per NEC Tables 430.248 / 430.250.
    /// If the motor has a nameplate amps value, that is used for overload sizing only —
    /// NEC requires using table values for branch circuit and feeder sizing.
    /// </summary>
    public static double GetFLA(double hp, double voltage, int phase)
    {
        if (phase == 1)
            return GetFLA_SinglePhase(hp, voltage);
        return GetFLA_ThreePhase(hp, voltage);
    }

    /// <summary>
    /// Sizes the motor branch circuit per NEC 430.22 (wire), 430.52 (OCPD), and 430.32 (overload).
    /// Wire ampacity ≥ 125% of FLA per NEC 430.22.
    /// OCPD per NEC Table 430.52 (dual-element fuse 175%, inverse-time breaker 250%).
    /// Overload ≤ 125% of FLA for SF ≥ 1.15, or ≤ 115% for SF < 1.15 per NEC 430.32.
    /// </summary>
    public static MotorBranchResult SizeBranchCircuit(MotorLoad motor)
    {
        double fla = GetFLA(motor.HP, motor.Voltage, motor.Phase);

        // NEC 430.22: Wire ampacity ≥ 125% of FLA
        double minWireAmpacity = fla * 1.25;
        string wireSize = NecAmpacityService.RecommendWireSize(
            minWireAmpacity, ConductorMaterial.Copper, InsulationTemperatureRating.C75) ?? "---";

        // NEC 430.52 Table: maximum OCPD size
        int maxDualElement = NextStandardOCPD((int)Math.Ceiling(fla * DualElementFuseMultiplier));
        int maxInverseTime = NextStandardOCPD((int)Math.Ceiling(fla * InverseTimeBreakerMultiplier));

        // NEC 430.32: Overload protection
        double overloadMultiplier = motor.ServiceFactor >= 1.15 ? 1.25 : 1.15;
        double flaForOverload = motor.NameplateAmps ?? fla;
        double overloadTrip = flaForOverload * overloadMultiplier;

        return new MotorBranchResult
        {
            MotorId = motor.Id,
            FLA = fla,
            MinWireAmpacity = Math.Round(minWireAmpacity, 1),
            RecommendedWireSize = wireSize,
            MaxOCPDAmpsDualElement = maxDualElement,
            MaxOCPDAmpsInverse = maxInverseTime,
            OverloadTripAmps = Math.Round(overloadTrip, 1),
        };
    }

    /// <summary>
    /// Sizes a motor feeder per NEC 430.24:
    /// Feeder ampacity ≥ 125% of largest motor FLA + 100% of all other motor FLAs.
    /// </summary>
    public static MotorFeederResult SizeMotorFeeder(IEnumerable<MotorLoad> motors)
    {
        var motorList = motors.ToList();
        if (motorList.Count == 0)
            return new MotorFeederResult();

        var flas = motorList.Select(m => GetFLA(m.HP, m.Voltage, m.Phase)).ToList();
        double largestFLA = flas.Max();
        double totalFLA = flas.Sum();

        // NEC 430.24: 125% of largest + 100% of rest = totalFLA + 25% of largest
        double minAmpacity = totalFLA + (largestFLA * 0.25);

        string wireSize = NecAmpacityService.RecommendWireSize(
            minAmpacity, ConductorMaterial.Copper, InsulationTemperatureRating.C75) ?? "---";

        return new MotorFeederResult
        {
            TotalFLA = Math.Round(totalFLA, 1),
            LargestMotorFLA = Math.Round(largestFLA, 1),
            MinFeederAmpacity = Math.Round(minAmpacity, 1),
            RecommendedWireSize = wireSize,
            MotorCount = motorList.Count,
        };
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static double GetFLA_SinglePhase(double hp, double voltage)
    {
        // Find closest HP entry
        var entry = Table430_248.MinBy(e => Math.Abs(e.HP - hp));

        // Select closest voltage column
        return voltage switch
        {
            <= 130 => entry.FLA115,
            <= 215 => entry.FLA200,
            _ => entry.FLA230,
        };
    }

    private static double GetFLA_ThreePhase(double hp, double voltage)
    {
        var entry = Table430_250.MinBy(e => Math.Abs(e.HP - hp));

        return voltage switch
        {
            <= 200 => entry.FLA200,
            <= 215 => entry.FLA208,
            <= 240 => entry.FLA230,
            <= 480 => entry.FLA460,
            _ => entry.FLA575,
        };
    }

    private static int NextStandardOCPD(int minAmps)
    {
        foreach (var size in StandardOCPDSizes)
        {
            if (size >= minAmps)
                return size;
        }
        return StandardOCPDSizes[^1];
    }
}

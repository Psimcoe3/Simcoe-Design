using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Conductor bundle/raceway fill derating per NEC 310.15(C)(1) (2020/2023 NEC).
/// When more than 3 current-carrying conductors are installed in a raceway, cable, 
/// or cable tray, their ampacity must be reduced (derated) to account for mutual heating.
/// </summary>
public static class BundleDeratingService
{
    // ── NEC Table 310.15(C)(1) ──────────────────────────────────────────────
    // Number of current-carrying conductors → adjustment factor (% of ampacity)
    private static readonly (int MinConductors, int MaxConductors, double Factor)[] Table310_15_C1 =
    {
        ( 4,  6, 0.80),
        ( 7,  9, 0.70),
        (10, 20, 0.50),
        (21, 30, 0.45),
        (31, 40, 0.40),
        (41, int.MaxValue, 0.35),
    };

    /// <summary>
    /// Returns the NEC 310.15(C)(1) adjustment factor for the number of current-carrying conductors.
    /// 1-3 conductors → 1.0 (no derating required).
    /// Returns value between 0.35 and 1.0.
    /// </summary>
    public static double GetAdjustmentFactor(int currentCarryingConductors)
    {
        if (currentCarryingConductors <= 3) return 1.0;

        foreach (var (min, max, factor) in Table310_15_C1)
        {
            if (currentCarryingConductors >= min && currentCarryingConductors <= max)
                return factor;
        }

        // Should not reach here, but return most conservative factor
        return 0.35;
    }

    /// <summary>
    /// Calculates de-rated ampacity for a wire in a bundle.
    /// Optionally combines with ambient temperature correction per NEC 310.15(B)(1).
    /// </summary>
    public static double GetDeratedAmpacity(
        string wireSize,
        ConductorMaterial material,
        InsulationTemperatureRating rating,
        int currentCarryingConductors,
        double? ambientTempC = null)
    {
        int baseAmpacity = NecAmpacityService.LookupAmpacity(wireSize, material, rating);
        double bundleFactor = GetAdjustmentFactor(currentCarryingConductors);

        double effectiveAmpacity = baseAmpacity * bundleFactor;

        if (ambientTempC.HasValue)
        {
            double tempFactor = NecAmpacityService.DefaultCorrectionFactors.GetFactor(ambientTempC.Value, rating);
            effectiveAmpacity *= tempFactor;
        }

        return Math.Round(effectiveAmpacity, 1);
    }

    /// <summary>
    /// Recommends the smallest wire size that can carry the required current
    /// after applying both bundle derating and optional ambient temperature correction.
    /// Returns null if no standard size is adequate.
    /// </summary>
    public static string? RecommendWireSizeWithBundle(
        double requiredAmps,
        ConductorMaterial material,
        InsulationTemperatureRating rating,
        int currentCarryingConductors,
        double? ambientTempC = null)
    {
        foreach (var size in NecAmpacityService.StandardSizes)
        {
            double derated = GetDeratedAmpacity(size, material, rating, currentCarryingConductors, ambientTempC);
            if (derated >= requiredAmps)
                return size;
        }
        return null;
    }

    /// <summary>
    /// Validates that a circuit's wire gauge has adequate ampacity considering bundle fill.
    /// Returns a result indicating whether derating causes the wire to be undersized.
    /// </summary>
    public static BundleDeratingResult ValidateCircuitInBundle(
        Circuit circuit,
        int currentCarryingConductors,
        double? ambientTempC = null)
    {
        var wire = circuit.Wire;
        var material = wire.Material;
        var rating = InsulationTemperatureRating.C75; // default for THW/THWN
        int baseAmpacity = NecAmpacityService.LookupAmpacity(wire.Size, material, rating);
        double factor = GetAdjustmentFactor(currentCarryingConductors);
        double deratedAmpacity = baseAmpacity * factor;

        if (ambientTempC.HasValue)
        {
            double tempFactor = NecAmpacityService.DefaultCorrectionFactors.GetFactor(ambientTempC.Value, rating);
            deratedAmpacity *= tempFactor;
        }

        deratedAmpacity = Math.Round(deratedAmpacity, 1);
        double circuitAmps = circuit.Breaker.TripAmps;
        bool isAdequate = deratedAmpacity >= circuitAmps;

        string? recommendedSize = null;
        if (!isAdequate)
        {
            recommendedSize = RecommendWireSizeWithBundle(
                circuitAmps, material, rating, currentCarryingConductors, ambientTempC);
        }

        return new BundleDeratingResult
        {
            CircuitId = circuit.Id,
            CircuitDescription = circuit.Description,
            WireGauge = wire.Size,
            BaseAmpacity = baseAmpacity,
            BundleFactor = factor,
            DeratedAmpacity = deratedAmpacity,
            CircuitLoad = circuitAmps,
            IsAdequate = isAdequate,
            RecommendedWireSize = recommendedSize,
            CurrentCarryingConductors = currentCarryingConductors,
        };
    }

    /// <summary>
    /// Counts current-carrying conductors for circuits sharing a raceway.
    /// Single-phase: 2 per circuit (hot + neutral carry current).
    /// Three-phase balanced: 3 per circuit (neutrals generally don't count per NEC 310.15(E)(1)).
    /// Three-phase with harmonics: 4 per circuit (neutral carries harmonic currents).
    /// Equipment grounds are not counted per NEC 310.15(C)(1) Exception.
    /// </summary>
    public static int CountCurrentCarrying(IEnumerable<Circuit> circuitsInRaceway, bool harmonicLoads = false)
    {
        int total = 0;
        foreach (var c in circuitsInRaceway)
        {
            // Phase is a string: "A", "B", "C" (1-pole), "AB"/"BC"/"AC" (2-pole), "ABC" (3-pole)
            if (c.Poles == 3)
                total += harmonicLoads ? 4 : 3;
            else
                total += 2;
        }
        return total;
    }
}

/// <summary>
/// Result of validating a circuit's wire size after bundle derating.
/// </summary>
public record BundleDeratingResult
{
    public string CircuitId { get; init; } = "";
    public string CircuitDescription { get; init; } = "";
    public string WireGauge { get; init; } = "";
    public int BaseAmpacity { get; init; }
    public double BundleFactor { get; init; } = 1.0;
    public double DeratedAmpacity { get; init; }
    public double CircuitLoad { get; init; }
    public bool IsAdequate { get; init; }
    public string? RecommendedWireSize { get; init; }
    public int CurrentCarryingConductors { get; init; }
    public string? Reason { get; init; }
}

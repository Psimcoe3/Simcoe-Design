namespace ElectricalComponentSandbox.Services;

/// <summary>
/// NEC Article 450 — Transformer sizing, overcurrent protection, and loss estimation.
/// Covers:
/// - Minimum kVA selection from connected load
/// - Primary/secondary OCPD sizing per NEC 450.3(B) Table
/// - Copper and core loss estimation
/// - K-factor derating for harmonic loads
/// - Full-load current calculation
/// </summary>
public static class TransformerSizingService
{
    /// <summary>Standard transformer kVA sizes per ANSI C57.</summary>
    private static readonly double[] StandardKVA =
    {
        3, 5, 7.5, 10, 15, 25, 37.5, 45, 50, 75, 100, 112.5, 150, 167, 200, 225, 250,
        300, 500, 750, 1000, 1500, 2000, 2500, 3000, 3750, 5000
    };

    /// <summary>
    /// NEC 450.3(B) — Maximum primary OCPD size as a percentage of rated primary current.
    /// If the calculated value doesn't correspond to a standard rating, next higher is allowed.
    /// Applies to transformers rated 600V and below.
    /// </summary>
    private static readonly (double MaxPrimaryPercent, double MaxSecondaryPercent)[] ProtectionTable =
    {
        // (Primary protection ≤ X% of primary FLA, Secondary protection ≤ Y% of secondary FLA)
        // Impedance ≤ 6%
        // Primary only: 125% (next size up permitted)
        // Primary + Secondary: 250% / 125%
        // Impedance 6%–10%
        // Primary only: 125%
        // Primary + Secondary: 250% / 125%
    };

    /// <summary>
    /// Calculates full-load current for a transformer at a given voltage and kVA.
    /// Single-phase: I = kVA × 1000 / V
    /// Three-phase: I = kVA × 1000 / (V × √3)
    /// </summary>
    public static double GetFullLoadAmps(double kva, double voltage, int phases = 3)
    {
        if (kva <= 0 || voltage <= 0) return 0;
        return phases == 1
            ? kva * 1000.0 / voltage
            : kva * 1000.0 / (voltage * Math.Sqrt(3));
    }

    /// <summary>
    /// Selects minimum standard kVA rating for a given load.
    /// Applies a configurable loading factor (default 80% = 125% sizing margin).
    /// </summary>
    public static double SelectTransformerKVA(double loadKVA, double maxLoadingPercent = 80)
    {
        if (loadKVA <= 0) return StandardKVA[0];
        double requiredKVA = loadKVA / (maxLoadingPercent / 100.0);
        foreach (var size in StandardKVA)
        {
            if (size >= requiredKVA) return size;
        }
        return StandardKVA[^1];
    }

    /// <summary>
    /// NEC 450.3(B) — Overcurrent protection for transformers ≤ 600V.
    /// Returns (PrimaryOCPDAmps, SecondaryOCPDAmps).
    /// Primary only: 125% of primary FLA; if not standard, next higher standard rating.
    /// With secondary protection: primary ≤ 250%, secondary ≤ 125%.
    /// </summary>
    public static TransformerProtectionResult SizeOCPD(
        double kva, double primaryVoltage, double secondaryVoltage, int phases = 3,
        bool secondaryProtection = false)
    {
        double primaryFLA = GetFullLoadAmps(kva, primaryVoltage, phases);
        double secondaryFLA = GetFullLoadAmps(kva, secondaryVoltage, phases);

        double maxPrimaryAmps;
        double maxSecondaryAmps;

        if (secondaryProtection)
        {
            // NEC 450.3(B): With secondary protection
            maxPrimaryAmps = primaryFLA * 2.50;
            maxSecondaryAmps = secondaryFLA * 1.25;
        }
        else
        {
            // NEC 450.3(B): Primary only
            maxPrimaryAmps = primaryFLA * 1.25;
            maxSecondaryAmps = 0;
        }

        // NEC 450.3(B): "next higher standard size" is permitted for the 125% calc
        int primaryOCPD = NextStandardOCPD((int)Math.Ceiling(primaryFLA * 1.25));
        // But must not exceed the maximum allowed percentage
        if (secondaryProtection && primaryOCPD > NextStandardOCPD((int)Math.Ceiling(maxPrimaryAmps)))
            primaryOCPD = FloorStandardOCPD((int)Math.Floor(maxPrimaryAmps));

        int secondaryOCPD = 0;
        if (secondaryProtection)
        {
            secondaryOCPD = NextStandardOCPD((int)Math.Ceiling(secondaryFLA * 1.25));
        }

        return new TransformerProtectionResult
        {
            PrimaryFLA = Math.Round(primaryFLA, 1),
            SecondaryFLA = Math.Round(secondaryFLA, 1),
            PrimaryOCPDAmps = primaryOCPD,
            SecondaryOCPDAmps = secondaryOCPD,
            KVA = kva,
            PrimaryVoltage = primaryVoltage,
            SecondaryVoltage = secondaryVoltage,
        };
    }

    /// <summary>
    /// Estimates transformer losses (copper + core).
    /// Copper loss varies with load²; core loss is constant.
    /// Typical dry-type: ~2% copper loss at full load, ~0.5% core loss.
    /// </summary>
    public static TransformerLossResult EstimateLosses(
        double ratedKVA, double loadKVA,
        double copperLossPercentAtFull = 2.0,
        double coreLossPercent = 0.5)
    {
        double loadingFraction = ratedKVA > 0 ? loadKVA / ratedKVA : 0;
        double copperLossKW = ratedKVA * (copperLossPercentAtFull / 100.0) * loadingFraction * loadingFraction;
        double coreLossKW = ratedKVA * (coreLossPercent / 100.0);
        double totalLossKW = copperLossKW + coreLossKW;
        double efficiency = loadKVA > 0 ? (loadKVA / (loadKVA + totalLossKW)) * 100.0 : 0;

        return new TransformerLossResult
        {
            RatedKVA = ratedKVA,
            LoadKVA = loadKVA,
            LoadingPercent = Math.Round(loadingFraction * 100, 1),
            CopperLossKW = Math.Round(copperLossKW, 3),
            CoreLossKW = Math.Round(coreLossKW, 3),
            TotalLossKW = Math.Round(totalLossKW, 3),
            EfficiencyPercent = Math.Round(efficiency, 2),
        };
    }

    /// <summary>
    /// K-factor derating for harmonic loads (NEC 450.9 / IEEE C57.110).
    /// K-rated transformers handle harmonic current without derating.
    /// Non-K-rated transformers must be derated when K-factor > 1.
    /// Returns the effective available kVA after derating.
    /// </summary>
    public static double DerateForHarmonics(double ratedKVA, double kFactor, bool isKRated = false)
    {
        if (kFactor <= 1.0 || isKRated) return ratedKVA;

        // Simplified derating: effective kVA = rated / √K
        return ratedKVA / Math.Sqrt(kFactor);
    }

    /// <summary>Standard OCPD sizes per NEC 240.6(A).</summary>
    private static readonly int[] StandardOCPDSizes =
    {
        15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90, 100,
        110, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500,
        600, 700, 800, 1000, 1200, 1600, 2000, 2500, 3000
    };

    private static int NextStandardOCPD(int minAmps)
    {
        foreach (var size in StandardOCPDSizes)
        {
            if (size >= minAmps) return size;
        }
        return StandardOCPDSizes[^1];
    }

    private static int FloorStandardOCPD(int maxAmps)
    {
        int result = StandardOCPDSizes[0];
        foreach (var size in StandardOCPDSizes)
        {
            if (size <= maxAmps) result = size;
            else break;
        }
        return result;
    }
}

public record TransformerProtectionResult
{
    public double PrimaryFLA { get; init; }
    public double SecondaryFLA { get; init; }
    public int PrimaryOCPDAmps { get; init; }
    public int SecondaryOCPDAmps { get; init; }
    public double KVA { get; init; }
    public double PrimaryVoltage { get; init; }
    public double SecondaryVoltage { get; init; }
}

public record TransformerLossResult
{
    public double RatedKVA { get; init; }
    public double LoadKVA { get; init; }
    public double LoadingPercent { get; init; }
    public double CopperLossKW { get; init; }
    public double CoreLossKW { get; init; }
    public double TotalLossKW { get; init; }
    public double EfficiencyPercent { get; init; }
}

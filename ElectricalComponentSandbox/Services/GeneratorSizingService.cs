using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// NEC Article 700/701/702 emergency load classification.
/// </summary>
public enum EmergencyLoadClass
{
    /// <summary>NEC 700 — Life safety: exit signs, egress lighting, fire alarms.</summary>
    LifeSafety,

    /// <summary>NEC 701 — Legally required standby: ventilation, communications, lighting.</summary>
    LegallyRequired,

    /// <summary>NEC 702 — Optional standby: HVAC, refrigeration, data systems.</summary>
    OptionalStandby,

    /// <summary>Normal power only — not on emergency system.</summary>
    Normal,
}

/// <summary>
/// A load entry for generator sizing calculations.
/// </summary>
public record GeneratorLoad
{
    public string Id { get; init; } = "";
    public string Description { get; init; } = "";
    public EmergencyLoadClass LoadClass { get; init; } = EmergencyLoadClass.Normal;
    public double ConnectedKVA { get; init; }
    public double DemandFactor { get; init; } = 1.0;
    public double DemandKVA => ConnectedKVA * DemandFactor;
    public double PowerFactor { get; init; } = 0.8;
    public double MotorStartingKVA { get; init; }

    /// <summary>True if this is a motor load requiring NEC 430 starting current consideration.</summary>
    public bool IsMotor { get; init; }
}

/// <summary>
/// Result of generator sizing analysis.
/// </summary>
public record GeneratorSizingResult
{
    public double LifeSafetyKVA { get; init; }
    public double LegallyRequiredKVA { get; init; }
    public double OptionalStandbyKVA { get; init; }
    public double TotalDemandKVA { get; init; }
    public double LargestMotorStartingKVA { get; init; }
    public double PeakDemandKVA { get; init; }
    public double RecommendedGeneratorKVA { get; init; }
    public int RecommendedGeneratorKW { get; init; }
    public double AverageWeightedPowerFactor { get; init; }
    public int LoadCount { get; init; }

    /// <summary>Breakdown by NEC article classification.</summary>
    public Dictionary<EmergencyLoadClass, double> BreakdownByClass { get; init; } = new();
}

/// <summary>
/// Generator sizing and emergency load analysis per NEC Articles 700, 701, 702.
/// Calculates minimum generator capacity considering:
/// - Life safety loads (NEC 700): must be served within 10 seconds
/// - Legally required standby (NEC 701): must be served within 60 seconds
/// - Optional standby (NEC 702): connected when capacity allows
/// - Motor starting inrush (largest motor starting KVA added to running load)
/// - 125% NEC sizing factor for continuous loads
/// </summary>
public static class GeneratorSizingService
{
    /// <summary>NEC continuous-load factor: generators must be sized for 125% of continuous load.</summary>
    private const double ContinuousLoadFactor = 1.25;

    /// <summary>Standard generator sizes in kW for rounding up recommendations.</summary>
    private static readonly int[] StandardGeneratorKW =
    {
        20, 25, 30, 35, 40, 45, 50, 60, 75, 80, 100, 125, 150, 175, 200,
        250, 300, 350, 400, 500, 600, 750, 800, 1000, 1250, 1500, 1750, 2000, 2500, 3000
    };

    /// <summary>
    /// Sizes a generator based on the provided emergency and standby loads.
    /// Follows NEC load hierarchy: Life Safety → Legally Required → Optional.
    /// Motor starting inrush is added on top of running loads.
    /// </summary>
    public static GeneratorSizingResult SizeGenerator(IEnumerable<GeneratorLoad> loads)
    {
        var loadList = loads.ToList();
        if (loadList.Count == 0)
            return new GeneratorSizingResult();

        // Sum demand KVA by classification
        var byClass = new Dictionary<EmergencyLoadClass, double>
        {
            [EmergencyLoadClass.LifeSafety] = 0,
            [EmergencyLoadClass.LegallyRequired] = 0,
            [EmergencyLoadClass.OptionalStandby] = 0,
            [EmergencyLoadClass.Normal] = 0,
        };

        double totalDemandKVA = 0;
        double largestMotorStartingKVA = 0;
        double weightedPFNumerator = 0;

        foreach (var load in loadList)
        {
            byClass[load.LoadClass] += load.DemandKVA;
            totalDemandKVA += load.DemandKVA;
            weightedPFNumerator += load.DemandKVA * load.PowerFactor;

            if (load.IsMotor && load.MotorStartingKVA > largestMotorStartingKVA)
                largestMotorStartingKVA = load.MotorStartingKVA;
        }

        // Peak = running + largest motor starting inrush (starting minus running of that motor)
        double peakDemandKVA = totalDemandKVA + largestMotorStartingKVA;

        // NEC sizing: 125% of continuous loads (simplified: treat all emergency as continuous)
        double requiredKVA = peakDemandKVA * ContinuousLoadFactor;

        double averagePF = totalDemandKVA > 0 ? weightedPFNumerator / totalDemandKVA : 0.8;
        int requiredKW = (int)Math.Ceiling(requiredKVA * averagePF);

        int recommendedKW = NextStandardGenerator(requiredKW);
        double recommendedKVA = averagePF > 0 ? recommendedKW / averagePF : recommendedKW;

        return new GeneratorSizingResult
        {
            LifeSafetyKVA = Math.Round(byClass[EmergencyLoadClass.LifeSafety], 1),
            LegallyRequiredKVA = Math.Round(byClass[EmergencyLoadClass.LegallyRequired], 1),
            OptionalStandbyKVA = Math.Round(byClass[EmergencyLoadClass.OptionalStandby], 1),
            TotalDemandKVA = Math.Round(totalDemandKVA, 1),
            LargestMotorStartingKVA = Math.Round(largestMotorStartingKVA, 1),
            PeakDemandKVA = Math.Round(peakDemandKVA, 1),
            RecommendedGeneratorKVA = Math.Round(recommendedKVA, 1),
            RecommendedGeneratorKW = recommendedKW,
            AverageWeightedPowerFactor = Math.Round(averagePF, 3),
            LoadCount = loadList.Count,
            BreakdownByClass = byClass.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 1)),
        };
    }

    /// <summary>
    /// Validates that emergency loads comply with NEC 700/701/702 requirements:
    /// - Life safety loads must not exceed generator capacity
    /// - Life safety + legally required must not exceed capacity
    /// - Total must not exceed capacity
    /// Returns a list of issues found.
    /// </summary>
    public static List<string> ValidateEmergencySystem(
        IEnumerable<GeneratorLoad> loads,
        double generatorKVA)
    {
        var issues = new List<string>();
        var result = SizeGenerator(loads);

        if (result.LifeSafetyKVA > generatorKVA)
            issues.Add($"NEC 700: Life safety load ({result.LifeSafetyKVA:F1} kVA) exceeds generator capacity ({generatorKVA:F1} kVA)");

        double tier2 = result.LifeSafetyKVA + result.LegallyRequiredKVA;
        if (tier2 > generatorKVA)
            issues.Add($"NEC 700+701: Life safety + legally required ({tier2:F1} kVA) exceeds generator capacity ({generatorKVA:F1} kVA)");

        if (result.PeakDemandKVA > generatorKVA)
            issues.Add($"Total peak demand ({result.PeakDemandKVA:F1} kVA) exceeds generator capacity ({generatorKVA:F1} kVA)");

        if (result.RecommendedGeneratorKVA > generatorKVA)
            issues.Add($"Generator undersized: requires {result.RecommendedGeneratorKVA:F1} kVA (with 125% factor), available {generatorKVA:F1} kVA");

        return issues;
    }

    /// <summary>
    /// Determines emergency load priority sequence for generator loading.
    /// NEC 700.4: Life safety must be available within 10 seconds.
    /// NEC 701.12: Legally required within 60 seconds.
    /// NEC 702.12: Optional standby when capacity permits.
    /// </summary>
    public static List<(EmergencyLoadClass Class, double DemandKVA, int SequencePriority)> GetLoadSequence(
        IEnumerable<GeneratorLoad> loads)
    {
        var groups = loads
            .GroupBy(l => l.LoadClass)
            .Select(g => (
                Class: g.Key,
                DemandKVA: Math.Round(g.Sum(l => l.DemandKVA), 1),
                SequencePriority: g.Key switch
                {
                    EmergencyLoadClass.LifeSafety => 1,
                    EmergencyLoadClass.LegallyRequired => 2,
                    EmergencyLoadClass.OptionalStandby => 3,
                    _ => 4,
                }))
            .OrderBy(x => x.SequencePriority)
            .ToList();
        return groups;
    }

    private static int NextStandardGenerator(int minKW)
    {
        foreach (var size in StandardGeneratorKW)
        {
            if (size >= minKW) return size;
        }
        return StandardGeneratorKW[^1];
    }
}

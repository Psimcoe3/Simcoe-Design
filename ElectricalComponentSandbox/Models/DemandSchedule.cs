namespace ElectricalComponentSandbox.Models;

/// <summary>
/// A single tier within a tiered demand factor schedule.
/// Load up to <see cref="ThresholdVA"/> uses <see cref="Factor"/>;
/// remaining load falls through to the next tier.
/// </summary>
public class DemandTier
{
    /// <summary>
    /// Maximum VA this tier applies to. Use <see cref="double.MaxValue"/>
    /// (or a very large number) for the "remainder" tier.
    /// </summary>
    public double ThresholdVA { get; set; }

    /// <summary>Demand factor (0.0–1.0) applied to load within this tier.</summary>
    public double Factor { get; set; } = 1.0;
}

/// <summary>
/// A named tiered demand factor schedule for a specific <see cref="LoadClassification"/>.
/// Mirrors Revit's per-classification demand factor settings in <c>ElectricalSetting</c>.
/// Stored in <see cref="ProjectModel.DemandSchedules"/>.
/// </summary>
public class DemandSchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>The load classification this schedule applies to.</summary>
    public LoadClassification Classification { get; set; }

    /// <summary>
    /// Ordered list of tiers. Tiers are evaluated in order; each tier consumes
    /// load up to its <see cref="DemandTier.ThresholdVA"/> before passing the
    /// remainder to the next tier.
    /// </summary>
    public List<DemandTier> Tiers { get; set; } = new();

    /// <summary>Whether this is a built-in NEC 220 schedule that ships with the app.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Applies the tiered demand factors to a raw connected load in VA,
    /// returning the resulting demand load in VA.
    /// </summary>
    public double Apply(double connectedVA)
    {
        if (connectedVA <= 0 || Tiers.Count == 0)
            return connectedVA;

        double remaining = connectedVA;
        double demandVA = 0;

        foreach (var tier in Tiers)
        {
            if (remaining <= 0) break;
            double portion = Math.Min(remaining, tier.ThresholdVA);
            demandVA += portion * tier.Factor;
            remaining -= portion;
        }

        // Any load beyond all tiers passes through at 100%
        demandVA += remaining;
        return demandVA;
    }

    // ── Built-In NEC 220 Defaults ────────────────────────────────────────────

    /// <summary>
    /// Returns the standard NEC Article 220 tiered demand schedules.
    /// <list type="bullet">
    ///   <item>Lighting: first 3,000 VA @ 100%, remainder @ 35% (NEC 220.42)</item>
    ///   <item>Receptacle (Power): first 10,000 VA @ 100%, remainder @ 50% (NEC 220.44)</item>
    ///   <item>HVAC: 100% (NEC 220.50 — largest motor at 125%)</item>
    ///   <item>Other: 100% (no NEC tier reduction)</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<DemandSchedule> GetNec220Defaults() => new[]
    {
        new DemandSchedule
        {
            Id = "nec220-lighting",
            Classification = LoadClassification.Lighting,
            IsBuiltIn = true,
            Tiers =
            {
                new DemandTier { ThresholdVA = 3_000, Factor = 1.0 },
                new DemandTier { ThresholdVA = double.MaxValue, Factor = 0.35 }
            }
        },
        new DemandSchedule
        {
            Id = "nec220-power",
            Classification = LoadClassification.Power,
            IsBuiltIn = true,
            Tiers =
            {
                new DemandTier { ThresholdVA = 10_000, Factor = 1.0 },
                new DemandTier { ThresholdVA = double.MaxValue, Factor = 0.50 }
            }
        },
        new DemandSchedule
        {
            Id = "nec220-hvac",
            Classification = LoadClassification.HVAC,
            IsBuiltIn = true,
            Tiers =
            {
                new DemandTier { ThresholdVA = double.MaxValue, Factor = 1.0 }
            }
        },
        new DemandSchedule
        {
            Id = "nec220-other",
            Classification = LoadClassification.Other,
            IsBuiltIn = true,
            Tiers =
            {
                new DemandTier { ThresholdVA = double.MaxValue, Factor = 1.0 }
            }
        }
    };
}

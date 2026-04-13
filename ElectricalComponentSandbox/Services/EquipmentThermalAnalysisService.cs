using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Equipment thermal analysis: heat gain from electrical equipment,
/// HVAC tonnage requirements, ambient temperature rise, and derating factors.
/// </summary>
public static class EquipmentThermalAnalysisService
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>BTU per watt (1 W = 3.412 BTU/h).</summary>
    public const double BtuPerWatt = 3.412;

    /// <summary>BTU/h per ton of cooling.</summary>
    public const double BtuPerTon = 12000;

    // ── Records ──────────────────────────────────────────────────────────────

    public enum EquipmentCategory
    {
        Transformer,
        Panel,
        MCC,
        VFD,
        UPS,
        Switchgear,
        Server,
        Lighting,
        Other,
    }

    /// <summary>An electrical equipment item generating heat.</summary>
    public record HeatSource
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public EquipmentCategory Category { get; init; }
        public double RatedKW { get; init; }
        public double LoadFactor { get; init; } = 1.0;
        public double EfficiencyPercent { get; init; } = 97;
        public double? HeatDissipationWatts { get; init; }
    }

    /// <summary>Result for a single heat source.</summary>
    public record HeatSourceResult
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public double HeatDissipationWatts { get; init; }
        public double HeatDissipationBtuH { get; init; }
    }

    /// <summary>Room thermal analysis result.</summary>
    public record RoomThermalResult
    {
        public List<HeatSourceResult> Sources { get; init; } = new();
        public double TotalHeatWatts { get; init; }
        public double TotalHeatBtuH { get; init; }
        public double RequiredCoolingTons { get; init; }
        public double AmbientRiseDegF { get; init; }
        public double EstimatedRoomTempF { get; init; }
        public bool RequiresDedicatedCooling { get; init; }
    }

    /// <summary>Transformer heat dissipation result.</summary>
    public record TransformerHeatResult
    {
        public double NoLoadLossWatts { get; init; }
        public double LoadLossWatts { get; init; }
        public double TotalLossWatts { get; init; }
        public double TotalLossBtuH { get; init; }
    }

    // ── Heat Dissipation Calculations ────────────────────────────────────────

    /// <summary>
    /// Calculates heat dissipation of a single equipment item.
    /// If HeatDissipationWatts is explicitly provided, uses that value.
    /// Otherwise: Heat = RatedKW × LoadFactor × (1 - Efficiency/100) × 1000.
    /// </summary>
    public static double CalculateHeatDissipation(HeatSource source)
    {
        if (source.HeatDissipationWatts.HasValue)
            return source.HeatDissipationWatts.Value * source.LoadFactor;

        double inputWatts = source.RatedKW * 1000 * source.LoadFactor;
        double lossPercent = 1.0 - (source.EfficiencyPercent / 100.0);
        return inputWatts * lossPercent;
    }

    /// <summary>
    /// Estimates dry-type transformer losses per DOE 2016 standards (simplified).
    /// No-load loss ≈ 0.5% of kVA (as watts). Load loss varies by load factor.
    /// </summary>
    public static TransformerHeatResult CalculateTransformerHeat(
        double kva, double loadFactor = 1.0, double noLoadLossPercent = 0.5, double loadLossPercent = 2.0)
    {
        double noLoad = kva * (noLoadLossPercent / 100.0) * 1000;
        double loadLoss = kva * (loadLossPercent / 100.0) * 1000 * loadFactor * loadFactor;
        double total = noLoad + loadLoss;

        return new TransformerHeatResult
        {
            NoLoadLossWatts = Math.Round(noLoad, 0),
            LoadLossWatts = Math.Round(loadLoss, 0),
            TotalLossWatts = Math.Round(total, 0),
            TotalLossBtuH = Math.Round(total * BtuPerWatt, 0),
        };
    }

    // ── Room Thermal Analysis ────────────────────────────────────────────────

    /// <summary>
    /// Analyzes total heat gain for an electrical room and determines cooling requirements.
    /// </summary>
    /// <param name="sources">Equipment in the room.</param>
    /// <param name="roomVolumeCuFt">Room volume for ambient rise calculation.</param>
    /// <param name="baseAmbientF">Ambient temperature before equipment heat (°F).</param>
    /// <param name="airChangesPerHour">Natural/forced ventilation air changes per hour.</param>
    public static RoomThermalResult AnalyzeRoom(
        IEnumerable<HeatSource> sources,
        double roomVolumeCuFt = 1000,
        double baseAmbientF = 75,
        double airChangesPerHour = 1.0)
    {
        var results = new List<HeatSourceResult>();
        double totalWatts = 0;

        foreach (var src in sources)
        {
            double watts = CalculateHeatDissipation(src);
            results.Add(new HeatSourceResult
            {
                Id = src.Id,
                Name = src.Name,
                HeatDissipationWatts = Math.Round(watts, 0),
                HeatDissipationBtuH = Math.Round(watts * BtuPerWatt, 0),
            });
            totalWatts += watts;
        }

        double totalBtu = totalWatts * BtuPerWatt;
        double coolingTons = totalBtu / BtuPerTon;

        // Simplified ambient rise: ΔT = Q / (ρ × Cp × V × ACH)
        // Air: ρ ≈ 0.075 lb/ft³, Cp ≈ 0.24 BTU/(lb·°F)
        double airFlowCfm = roomVolumeCuFt * airChangesPerHour / 60.0;
        double ambientRise = airFlowCfm > 0
            ? totalBtu / (airFlowCfm * 60 * 0.075 * 0.24)
            : 0;

        double estimatedTemp = baseAmbientF + ambientRise;

        return new RoomThermalResult
        {
            Sources = results,
            TotalHeatWatts = Math.Round(totalWatts, 0),
            TotalHeatBtuH = Math.Round(totalBtu, 0),
            RequiredCoolingTons = Math.Round(coolingTons, 2),
            AmbientRiseDegF = Math.Round(ambientRise, 1),
            EstimatedRoomTempF = Math.Round(estimatedTemp, 1),
            RequiresDedicatedCooling = coolingTons >= 1.0,
        };
    }

    // ── Derating ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the NEC 310.15(B) ambient temperature correction factor
    /// for 75°C rated conductors (simplified).
    /// </summary>
    public static double AmbientCorrectionFactor75C(double ambientTempC)
    {
        if (ambientTempC <= 30) return 1.00;
        if (ambientTempC <= 35) return 0.94;
        if (ambientTempC <= 40) return 0.88;
        if (ambientTempC <= 45) return 0.82;
        if (ambientTempC <= 50) return 0.75;
        if (ambientTempC <= 55) return 0.67;
        if (ambientTempC <= 60) return 0.58;
        return 0.00; // Above 60°C, no current allowed for 75°C insulation
    }

    /// <summary>Converts Fahrenheit to Celsius.</summary>
    public static double FahrenheitToCelsius(double f) => (f - 32) * 5.0 / 9.0;

    /// <summary>Converts watts to BTU/h.</summary>
    public static double WattsToBtu(double watts) => watts * BtuPerWatt;

    /// <summary>Converts BTU/h to tons of cooling.</summary>
    public static double BtuToTons(double btu) => btu / BtuPerTon;
}

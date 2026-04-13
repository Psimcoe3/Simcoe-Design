using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Emergency and egress lighting calculations per NEC 700, IBC 1008,
/// and NFPA 101 Life Safety Code.
/// </summary>
public static class EmergencyLightingService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum EgressPathType
    {
        Corridor,
        Stairway,
        Exit,
        OpenFloor,
        Exterior,
    }

    public enum EmergencyPowerSource
    {
        BatteryUnit,           // Self-contained battery pack
        CentralBattery,        // Central inverter system
        Generator,             // Standby generator
        UnitEquipmentCombo,    // Unit equipment per NEC 700.12(F)
    }

    /// <summary>An egress path segment requiring emergency illumination.</summary>
    public record EgressSegment
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public EgressPathType PathType { get; init; }
        public double LengthFeet { get; init; }
        public double WidthFeet { get; init; }
        public double AreaSqFt { get; init; }
    }

    /// <summary>Emergency lighting analysis result for one segment.</summary>
    public record SegmentResult
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public double RequiredFc { get; init; }
        public double MinFc { get; init; }
        public double MaxUniformityRatio { get; init; }
        public int MinimumUnits { get; init; }
        public double AreaSqFt { get; init; }
    }

    /// <summary>Battery unit sizing result.</summary>
    public record BatteryUnitResult
    {
        public int UnitCount { get; init; }
        public double WattsPerUnit { get; init; }
        public double TotalWatts { get; init; }
        public double RequiredDurationMinutes { get; init; }
        public double BatteryAH { get; init; }
    }

    /// <summary>Complete emergency lighting analysis.</summary>
    public record EmergencyLightingResult
    {
        public List<SegmentResult> Segments { get; init; } = new();
        public int TotalUnitsRequired { get; init; }
        public double TotalAreaSqFt { get; init; }
        public BatteryUnitResult? BatterySizing { get; init; }
        public double RequiredDurationMinutes { get; init; }
    }

    // ── IBC 1008 / IES Illumination Requirements ────────────────────────────

    /// <summary>
    /// Returns required average illumination (fc) per IBC 1008.3.
    /// IBC: ≥1 fc average at floor, ≥0.1 fc minimum at any point.
    /// Stairways: exit discharge ≥1 fc.
    /// </summary>
    public static double GetRequiredIllumination(EgressPathType pathType)
    {
        return pathType switch
        {
            EgressPathType.Stairway => 1.0,
            EgressPathType.Exit     => 1.0,
            EgressPathType.Corridor => 1.0,
            EgressPathType.OpenFloor => 1.0,
            EgressPathType.Exterior => 1.0,
            _ => 1.0,
        };
    }

    /// <summary>
    /// Returns minimum illumination at any point per IBC 1008.3.2.
    /// ≥0.1 fc minimum to maintain max 40:1 uniformity ratio.
    /// </summary>
    public static double GetMinimumIllumination(EgressPathType pathType)
    {
        // IBC requires that at the end of 90 minutes, illumination shall
        // not be less than an average of 1 fc and not less than 0.1 fc
        // at any point (40:1 max uniformity).
        return pathType switch
        {
            EgressPathType.Stairway => 0.1,
            _ => 0.1,
        };
    }

    /// <summary>
    /// Maximum uniformity ratio (max:min illumination) per IBC 1008.3.2.
    /// </summary>
    public static double GetMaxUniformityRatio(EgressPathType pathType)
    {
        return pathType switch
        {
            _ => 40.0,  // IBC 1008.3.2: max 40:1
        };
    }

    // ── Unit Spacing ─────────────────────────────────────────────────────────

    /// <summary>
    /// Estimates minimum emergency lighting units for a segment.
    /// Rule of thumb: one unit per 1000 sqft for open areas,
    /// one per 50ft of corridor length, one per stairway landing.
    /// </summary>
    public static int EstimateMinimumUnits(EgressSegment segment)
    {
        double area = segment.AreaSqFt > 0
            ? segment.AreaSqFt
            : segment.LengthFeet * segment.WidthFeet;

        if (area <= 0) return 1;

        return segment.PathType switch
        {
            EgressPathType.Corridor  => Math.Max(1, (int)Math.Ceiling(segment.LengthFeet / 50.0)),
            EgressPathType.Stairway  => Math.Max(1, (int)Math.Ceiling(segment.LengthFeet / 30.0)),
            EgressPathType.Exit      => 1,
            _                        => Math.Max(1, (int)Math.Ceiling(area / 1000.0)),
        };
    }

    // ── Duration Requirements ────────────────────────────────────────────────

    /// <summary>
    /// Required emergency lighting duration per code.
    /// NEC 700.12: Not less than 1.5 hours (90 minutes).
    /// NFPA 101 § 7.9.2.1: 1.5 hours minimum.
    /// </summary>
    public static double GetRequiredDurationMinutes(EmergencyPowerSource source)
    {
        return source switch
        {
            EmergencyPowerSource.BatteryUnit        => 90,
            EmergencyPowerSource.CentralBattery     => 90,
            EmergencyPowerSource.UnitEquipmentCombo => 90,
            EmergencyPowerSource.Generator          => 90, // Generator must start within 10 sec per NEC 700.12
            _ => 90,
        };
    }

    // ── Battery Unit Sizing ──────────────────────────────────────────────────

    /// <summary>
    /// Sizes battery-backed emergency lighting units.
    /// </summary>
    /// <param name="unitCount">Number of units required.</param>
    /// <param name="wattsPerUnit">Lamp wattage per unit (LED typical: 3-10W).</param>
    /// <param name="batteryVoltage">Battery voltage (typical: 6V or 12V).</param>
    /// <param name="durationMinutes">Required duration (default 90 min).</param>
    /// <param name="agingFactor">Battery aging derating (default 1.25 = 25% margin).</param>
    public static BatteryUnitResult SizeBatteryUnits(
        int unitCount, double wattsPerUnit = 7.2,
        double batteryVoltage = 6.0, double durationMinutes = 90,
        double agingFactor = 1.25)
    {
        double totalWatts = unitCount * wattsPerUnit;
        double currentAmps = wattsPerUnit / batteryVoltage;
        double durationHours = durationMinutes / 60.0;
        double batteryAH = currentAmps * durationHours * agingFactor;

        return new BatteryUnitResult
        {
            UnitCount = unitCount,
            WattsPerUnit = wattsPerUnit,
            TotalWatts = Math.Round(totalWatts, 1),
            RequiredDurationMinutes = durationMinutes,
            BatteryAH = Math.Round(batteryAH, 1),
        };
    }

    // ── Full Analysis ────────────────────────────────────────────────────────

    /// <summary>
    /// Analyzes all egress segments and produces a complete emergency lighting plan.
    /// </summary>
    public static EmergencyLightingResult Analyze(
        IEnumerable<EgressSegment> segments,
        EmergencyPowerSource source = EmergencyPowerSource.BatteryUnit,
        double wattsPerUnit = 7.2)
    {
        var results = new List<SegmentResult>();
        int totalUnits = 0;
        double totalArea = 0;

        foreach (var seg in segments)
        {
            double area = seg.AreaSqFt > 0 ? seg.AreaSqFt : seg.LengthFeet * seg.WidthFeet;
            int units = EstimateMinimumUnits(seg);
            totalUnits += units;
            totalArea += area;

            results.Add(new SegmentResult
            {
                Id = seg.Id,
                Name = seg.Name,
                RequiredFc = GetRequiredIllumination(seg.PathType),
                MinFc = GetMinimumIllumination(seg.PathType),
                MaxUniformityRatio = GetMaxUniformityRatio(seg.PathType),
                MinimumUnits = units,
                AreaSqFt = area,
            });
        }

        double duration = GetRequiredDurationMinutes(source);
        var battery = source == EmergencyPowerSource.BatteryUnit || source == EmergencyPowerSource.UnitEquipmentCombo
            ? SizeBatteryUnits(totalUnits, wattsPerUnit, durationMinutes: duration)
            : null;

        return new EmergencyLightingResult
        {
            Segments = results,
            TotalUnitsRequired = totalUnits,
            TotalAreaSqFt = totalArea,
            BatterySizing = battery,
            RequiredDurationMinutes = duration,
        };
    }
}

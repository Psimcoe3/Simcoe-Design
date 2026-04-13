using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Lightning protection risk assessment per NFPA 780 and IEC 62305-2.
/// Simplified risk analysis and protection system recommendations.
/// </summary>
public static class LightningProtectionService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum BuildingOccupancy
    {
        Commercial,
        Industrial,
        Residential,
        HighRisk,          // Hospitals, data centers, explosive storage
        Assembly,          // Schools, theaters, stadiums
        Heritage,          // Historic or irreplaceable structures
    }

    public enum LightningProtectionLevel
    {
        I,      // Highest protection (rolling sphere 20m)
        II,     // High (30m)
        III,    // Standard (45m)
        IV,     // Basic (60m)
    }

    /// <summary>Building parameters for risk assessment.</summary>
    public record BuildingParameters
    {
        public double LengthMeters { get; init; }
        public double WidthMeters { get; init; }
        public double HeightMeters { get; init; }
        public BuildingOccupancy Occupancy { get; init; }
        public double FlashDensity { get; init; } = 4.0; // Ng: flashes/km²/year
        public bool HasExplosiveContents { get; init; }
        public bool IsIsolated { get; init; }
        public bool HasElectronicSystems { get; init; } = true;
    }

    /// <summary>Risk assessment result.</summary>
    public record RiskAssessmentResult
    {
        public double CollectionArea { get; init; }
        public double AnnualStrikeFrequency { get; init; }
        public double TolerableRisk { get; init; }
        public bool ProtectionRequired { get; init; }
        public LightningProtectionLevel RecommendedLevel { get; init; }
        public string Justification { get; init; } = "";
    }

    /// <summary>Rolling sphere method parameters.</summary>
    public record RollingSphereResult
    {
        public LightningProtectionLevel Level { get; init; }
        public double SphereRadiusMeters { get; init; }
        public double MeshSizeMeters { get; init; }
        public double DownConductorSpacingMeters { get; init; }
        public int MinDownConductors { get; init; }
    }

    /// <summary>Grounding system specification.</summary>
    public record GroundingSpec
    {
        public double TargetResistanceOhms { get; init; }
        public int GroundRodCount { get; init; }
        public double GroundRodLengthFt { get; init; }
        public double RingGroundPerimeterFt { get; init; }
        public string ConductorSize { get; init; } = "";
    }

    // ── Collection Area (IEC 62305-2) ────────────────────────────────────────

    /// <summary>
    /// Calculates the equivalent collection area per IEC 62305-2 Annex A.
    /// Ad = L×W + 2×(3H)×(L+W) + π×(3H)²
    /// where H is the effective height.
    /// </summary>
    public static double CalculateCollectionArea(double lengthM, double widthM, double heightM)
    {
        double effectiveH = 3 * heightM;
        double area = lengthM * widthM
                      + 2 * effectiveH * (lengthM + widthM)
                      + Math.PI * effectiveH * effectiveH;
        return Math.Round(area, 1);
    }

    /// <summary>
    /// Annual expected number of lightning strikes to the structure.
    /// Nd = Ng × Ad × Cd × 1e-6
    /// where Cd is the environmental correction factor.
    /// </summary>
    public static double CalculateStrikeFrequency(
        double collectionAreaSqM, double flashDensity, bool isIsolated)
    {
        double cd = isIsolated ? 2.0 : 1.0; // Exposed buildings → 2× risk
        double nd = flashDensity * collectionAreaSqM * cd * 1e-6;
        return Math.Round(nd, 4);
    }

    // ── Tolerable Risk ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns tolerable risk level based on building occupancy per IEC 62305.
    /// Lower values = more protection needed.
    /// </summary>
    public static double GetTolerableRisk(BuildingOccupancy occupancy)
    {
        return occupancy switch
        {
            BuildingOccupancy.HighRisk  => 1e-5,
            BuildingOccupancy.Heritage  => 1e-4,
            BuildingOccupancy.Assembly  => 1e-4,
            BuildingOccupancy.Industrial => 1e-3,
            BuildingOccupancy.Commercial => 1e-3,
            BuildingOccupancy.Residential => 1e-3,
            _ => 1e-3,
        };
    }

    // ── Risk Assessment ──────────────────────────────────────────────────────

    /// <summary>
    /// Performs a simplified lightning risk assessment per IEC 62305-2 / NFPA 780.
    /// </summary>
    public static RiskAssessmentResult AssessRisk(BuildingParameters building)
    {
        double area = CalculateCollectionArea(
            building.LengthMeters, building.WidthMeters, building.HeightMeters);
        double nd = CalculateStrikeFrequency(area, building.FlashDensity, building.IsIsolated);
        double tolerableRisk = GetTolerableRisk(building.Occupancy);

        bool required = nd > tolerableRisk || building.HasExplosiveContents;

        // Determine protection level based on risk ratio
        LightningProtectionLevel level;
        if (building.HasExplosiveContents)
            level = LightningProtectionLevel.I;
        else if (nd > tolerableRisk * 10)
            level = LightningProtectionLevel.I;
        else if (nd > tolerableRisk * 5)
            level = LightningProtectionLevel.II;
        else if (nd > tolerableRisk)
            level = LightningProtectionLevel.III;
        else
            level = LightningProtectionLevel.IV;

        string justification = required
            ? $"Nd ({nd:F4}) exceeds tolerable risk ({tolerableRisk:E1})"
            : $"Nd ({nd:F4}) within tolerable risk ({tolerableRisk:E1})";

        return new RiskAssessmentResult
        {
            CollectionArea = area,
            AnnualStrikeFrequency = nd,
            TolerableRisk = tolerableRisk,
            ProtectionRequired = required,
            RecommendedLevel = level,
            Justification = justification,
        };
    }

    // ── Rolling Sphere Method ────────────────────────────────────────────────

    /// <summary>
    /// Returns rolling sphere method parameters per IEC 62305-3 / NFPA 780.
    /// </summary>
    public static RollingSphereResult GetRollingSphereParameters(
        LightningProtectionLevel level, double buildingPerimeterM)
    {
        (double radius, double mesh, double spacing) = level switch
        {
            LightningProtectionLevel.I   => (20.0, 5.0, 10.0),
            LightningProtectionLevel.II  => (30.0, 10.0, 15.0),
            LightningProtectionLevel.III => (45.0, 15.0, 20.0),
            LightningProtectionLevel.IV  => (60.0, 20.0, 25.0),
            _ => (45.0, 15.0, 20.0),
        };

        int minDownConductors = Math.Max(2, (int)Math.Ceiling(buildingPerimeterM / spacing));

        return new RollingSphereResult
        {
            Level = level,
            SphereRadiusMeters = radius,
            MeshSizeMeters = mesh,
            DownConductorSpacingMeters = spacing,
            MinDownConductors = minDownConductors,
        };
    }

    // ── Grounding ────────────────────────────────────────────────────────────

    /// <summary>
    /// Specifies the grounding system for a lightning protection system.
    /// NFPA 780 / NEC 250: Target ≤ 25 ohms for LPS, ≤ 10 ohms preferred.
    /// </summary>
    public static GroundingSpec SpecifyGrounding(
        double buildingPerimeterFt,
        LightningProtectionLevel level)
    {
        double targetOhms = level switch
        {
            LightningProtectionLevel.I or LightningProtectionLevel.II => 5.0,
            _ => 10.0,
        };

        // Ring ground conductor: #2 AWG copper minimum per NFPA 780
        string conductorSize = level switch
        {
            LightningProtectionLevel.I => "1/0",
            _ => "2",
        };

        // One ground rod per down conductor location, 8ft minimum
        int rods = Math.Max(2, (int)Math.Ceiling(buildingPerimeterFt / (level <= LightningProtectionLevel.II ? 30 : 60)));

        return new GroundingSpec
        {
            TargetResistanceOhms = targetOhms,
            GroundRodCount = rods,
            GroundRodLengthFt = 8,
            RingGroundPerimeterFt = buildingPerimeterFt,
            ConductorSize = conductorSize,
        };
    }
}

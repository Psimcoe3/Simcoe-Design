using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// IES-standard illuminance calculations: zonal cavity (lumen) method,
/// point-by-point method, fixture spacing criteria, and light-loss factors.
/// </summary>
public static class LightingCalculationService
{
    // ── IES recommended illuminance by occupancy (lux) ───────────────────────

    public enum OccupancyType
    {
        Office,
        Classroom,
        Corridor,
        Warehouse,
        Retail,
        Hospital,
        Laboratory,
        Assembly,
        Parking,
        Restroom,
        Kitchen,
        Industrial,
        Exterior,
    }

    private static readonly Dictionary<OccupancyType, (double MinLux, double TargetLux, double MaxLux)> IesLevels = new()
    {
        [OccupancyType.Office] = (300, 500, 750),
        [OccupancyType.Classroom] = (300, 500, 750),
        [OccupancyType.Corridor] = (50, 100, 150),
        [OccupancyType.Warehouse] = (100, 200, 300),
        [OccupancyType.Retail] = (300, 500, 1000),
        [OccupancyType.Hospital] = (300, 500, 750),
        [OccupancyType.Laboratory] = (500, 750, 1000),
        [OccupancyType.Assembly] = (150, 300, 500),
        [OccupancyType.Parking] = (50, 100, 200),
        [OccupancyType.Restroom] = (150, 200, 300),
        [OccupancyType.Kitchen] = (300, 500, 750),
        [OccupancyType.Industrial] = (200, 300, 500),
        [OccupancyType.Exterior] = (20, 50, 100),
    };

    // ── Records ──────────────────────────────────────────────────────────────

    /// <summary>Room geometry for cavity calculations.</summary>
    public record RoomGeometry
    {
        public double LengthFeet { get; init; }
        public double WidthFeet { get; init; }
        public double CeilingHeightFeet { get; init; } = 9.0;
        public double WorkPlaneHeightFeet { get; init; } = 2.5;
        public double LuminaireMountHeightFeet { get; init; } = 8.5;
    }

    /// <summary>Luminaire photometric data.</summary>
    public record LuminaireData
    {
        public string Model { get; init; } = "";
        public double InitialLumens { get; init; }
        public double Watts { get; init; }
        public double CoefficientOfUtilization { get; init; } = 0.65;
        public double SpacingToMountHeightRatio { get; init; } = 1.2;
    }

    /// <summary>Light loss factors.</summary>
    public record LightLossFactors
    {
        /// <summary>Lamp lumen depreciation (LLD). Typical LED: 0.90-0.95.</summary>
        public double LampLumenDepreciation { get; init; } = 0.90;

        /// <summary>Luminaire dirt depreciation (LDD). Typical clean: 0.90.</summary>
        public double LuminaireDirtDepreciation { get; init; } = 0.90;

        /// <summary>Room surface depreciation. Typical: 0.95.</summary>
        public double RoomSurfaceDepreciation { get; init; } = 0.95;

        /// <summary>Ballast factor (for fluorescent) or driver factor (LED). Typical: 1.0.</summary>
        public double BallastFactor { get; init; } = 1.0;

        /// <summary>Combined total LLF.</summary>
        public double TotalLLF => LampLumenDepreciation * LuminaireDirtDepreciation
                                  * RoomSurfaceDepreciation * BallastFactor;
    }

    /// <summary>Cavity ratio results.</summary>
    public record CavityRatios
    {
        public double RoomCavityRatio { get; init; }
        public double CeilingCavityRatio { get; init; }
        public double FloorCavityRatio { get; init; }
    }

    /// <summary>Result of zonal cavity calculation.</summary>
    public record ZonalCavityResult
    {
        public CavityRatios CavityRatios { get; init; } = new();
        public double MaintainedIlluminanceLux { get; init; }
        public double MaintainedIlluminanceFC { get; init; }
        public int RecommendedFixtureCount { get; init; }
        public double ActualSpacingFeet { get; init; }
        public double MaxSpacingFeet { get; init; }
        public bool SpacingCompliant { get; init; }
        public double WattsPerSquareFoot { get; init; }
        public double TotalWatts { get; init; }
        public string? Note { get; init; }
    }

    /// <summary>Point illuminance result.</summary>
    public record PointIlluminanceResult
    {
        public double X { get; init; }
        public double Y { get; init; }
        public double IlluminanceLux { get; init; }
        public double IlluminanceFC { get; init; }
    }

    /// <summary>Illuminance compliance result.</summary>
    public record IlluminanceComplianceResult
    {
        public OccupancyType Occupancy { get; init; }
        public double TargetLux { get; init; }
        public double CalculatedLux { get; init; }
        public bool MeetsMinimum { get; init; }
        public bool ExceedsMaximum { get; init; }
        public double UniformityRatio { get; init; }
    }

    // ── Zonal Cavity (Lumen) Method ──────────────────────────────────────────

    /// <summary>
    /// Calculates cavity ratios per IES zonal cavity method.
    /// CR = 5h × (L + W) / (L × W)
    /// </summary>
    public static CavityRatios CalculateCavityRatios(RoomGeometry room)
    {
        double area = room.LengthFeet * room.WidthFeet;
        if (area <= 0) return new CavityRatios();

        double perimeter = 2.0 * (room.LengthFeet + room.WidthFeet);
        double hrc = room.CeilingHeightFeet - room.LuminaireMountHeightFeet;
        double hrr = room.LuminaireMountHeightFeet - room.WorkPlaneHeightFeet;
        double hrf = room.WorkPlaneHeightFeet;

        return new CavityRatios
        {
            RoomCavityRatio = 5.0 * hrr * perimeter / area,
            CeilingCavityRatio = 5.0 * hrc * perimeter / area,
            FloorCavityRatio = 5.0 * hrf * perimeter / area,
        };
    }

    /// <summary>
    /// Determines the number of luminaires needed to achieve a target illuminance
    /// using the zonal cavity (lumen) method.
    /// N = (E × A) / (F × CU × LLF)
    /// </summary>
    public static ZonalCavityResult CalculateZonalCavity(
        RoomGeometry room,
        LuminaireData luminaire,
        double targetLux,
        LightLossFactors? llf = null)
    {
        llf ??= new LightLossFactors();
        double area = room.LengthFeet * room.WidthFeet;
        if (area <= 0 || luminaire.InitialLumens <= 0)
        {
            return new ZonalCavityResult { Note = "Invalid room or luminaire data" };
        }

        var cavities = CalculateCavityRatios(room);

        // Convert target lux to foot-candles for calculation
        double targetFC = targetLux / 10.764;

        // Number of luminaires: N = (FC × Area) / (Lumens × CU × LLF)
        double denominator = luminaire.InitialLumens * luminaire.CoefficientOfUtilization * llf.TotalLLF;
        double nExact = (targetFC * area) / denominator;
        int fixtureCount = Math.Max(1, (int)Math.Ceiling(nExact));

        // Actual maintained illuminance with integer fixture count
        double actualFC = (fixtureCount * denominator) / area;
        double actualLux = actualFC * 10.764;

        // Spacing check
        double mountHeight = room.LuminaireMountHeightFeet - room.WorkPlaneHeightFeet;
        double maxSpacing = luminaire.SpacingToMountHeightRatio * mountHeight;

        // Simple grid spacing estimate
        double aspectRatio = room.LengthFeet / room.WidthFeet;
        int cols = Math.Max(1, (int)Math.Round(Math.Sqrt(fixtureCount / aspectRatio)));
        int rows = Math.Max(1, (int)Math.Ceiling((double)fixtureCount / cols));
        double spacingX = room.LengthFeet / Math.Max(1, cols);
        double spacingY = room.WidthFeet / Math.Max(1, rows);
        double actualSpacing = Math.Max(spacingX, spacingY);

        return new ZonalCavityResult
        {
            CavityRatios = cavities,
            MaintainedIlluminanceLux = Math.Round(actualLux, 1),
            MaintainedIlluminanceFC = Math.Round(actualFC, 1),
            RecommendedFixtureCount = fixtureCount,
            ActualSpacingFeet = Math.Round(actualSpacing, 2),
            MaxSpacingFeet = Math.Round(maxSpacing, 2),
            SpacingCompliant = actualSpacing <= maxSpacing,
            WattsPerSquareFoot = Math.Round(fixtureCount * luminaire.Watts / area, 2),
            TotalWatts = fixtureCount * luminaire.Watts,
        };
    }

    /// <summary>
    /// Determines the minimum number of fixtures to achieve target illuminance.
    /// </summary>
    public static int CalculateMinimumFixtures(
        RoomGeometry room,
        LuminaireData luminaire,
        double targetLux,
        LightLossFactors? llf = null)
    {
        var result = CalculateZonalCavity(room, luminaire, targetLux, llf);
        return result.RecommendedFixtureCount;
    }

    // ── Point-by-Point Method ────────────────────────────────────────────────

    /// <summary>
    /// Calculates illuminance at a point on the work plane from a single luminaire
    /// using the inverse square cosine law: E = I × cos(θ) / d²
    /// where I is candela, θ is angle from nadir, d is distance.
    /// </summary>
    public static double CalculatePointIlluminance(
        double fixtureX, double fixtureY, double fixtureHeightAboveWorkPlane,
        double pointX, double pointY,
        double candelaAtAngle)
    {
        double dx = pointX - fixtureX;
        double dy = pointY - fixtureY;
        double horizontalDist = Math.Sqrt(dx * dx + dy * dy);
        double distance = Math.Sqrt(horizontalDist * horizontalDist
                                     + fixtureHeightAboveWorkPlane * fixtureHeightAboveWorkPlane);

        if (distance <= 0) return 0;

        double cosTheta = fixtureHeightAboveWorkPlane / distance;
        double illuminanceFC = candelaAtAngle * cosTheta / (distance * distance);
        return illuminanceFC * 10.764; // Convert FC to lux
    }

    /// <summary>
    /// Calculates illuminance grid across room from an array of fixtures,
    /// assuming each fixture emits uniformly. Returns a list of point results.
    /// </summary>
    public static List<PointIlluminanceResult> CalculateIlluminanceGrid(
        RoomGeometry room,
        List<(double X, double Y)> fixturePositions,
        double candelaPerFixture,
        int gridRows = 5,
        int gridCols = 5)
    {
        var results = new List<PointIlluminanceResult>();
        double mountAboveWP = room.LuminaireMountHeightFeet - room.WorkPlaneHeightFeet;
        double stepX = room.LengthFeet / (gridCols + 1);
        double stepY = room.WidthFeet / (gridRows + 1);

        for (int r = 1; r <= gridRows; r++)
        {
            for (int c = 1; c <= gridCols; c++)
            {
                double px = c * stepX;
                double py = r * stepY;
                double totalLux = 0;

                foreach (var (fx, fy) in fixturePositions)
                {
                    totalLux += CalculatePointIlluminance(fx, fy, mountAboveWP, px, py, candelaPerFixture);
                }

                results.Add(new PointIlluminanceResult
                {
                    X = Math.Round(px, 2),
                    Y = Math.Round(py, 2),
                    IlluminanceLux = Math.Round(totalLux, 1),
                    IlluminanceFC = Math.Round(totalLux / 10.764, 1),
                });
            }
        }

        return results;
    }

    // ── Compliance Check ─────────────────────────────────────────────────────

    /// <summary>
    /// Checks calculated illuminance against IES recommended levels for the occupancy.
    /// </summary>
    public static IlluminanceComplianceResult CheckCompliance(
        OccupancyType occupancy,
        double calculatedLux,
        double uniformityRatio = 1.0)
    {
        var levels = IesLevels[occupancy];
        return new IlluminanceComplianceResult
        {
            Occupancy = occupancy,
            TargetLux = levels.TargetLux,
            CalculatedLux = calculatedLux,
            MeetsMinimum = calculatedLux >= levels.MinLux,
            ExceedsMaximum = calculatedLux > levels.MaxLux,
            UniformityRatio = uniformityRatio,
        };
    }

    /// <summary>
    /// Gets the IES recommended illuminance range for an occupancy type.
    /// Returns (MinLux, TargetLux, MaxLux).
    /// </summary>
    public static (double Min, double Target, double Max) GetRecommendedIlluminance(OccupancyType occupancy)
    {
        var levels = IesLevels[occupancy];
        return (levels.MinLux, levels.TargetLux, levels.MaxLux);
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    /// <summary>Converts foot-candles to lux.</summary>
    public static double FootCandlesToLux(double fc) => fc * 10.764;

    /// <summary>Converts lux to foot-candles.</summary>
    public static double LuxToFootCandles(double lux) => lux / 10.764;

    /// <summary>
    /// Calculates lighting power density (W/ft²) from total lighting watts and room area.
    /// Used for ASHRAE 90.1 / IECC compliance checks.
    /// </summary>
    public static double CalculateLPD(double totalWatts, double areaSqFt)
    {
        return areaSqFt > 0 ? totalWatts / areaSqFt : 0;
    }
}

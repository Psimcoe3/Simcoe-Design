using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Centralized NEC ampacity database service. Provides default NEC Table 310.16
/// ampacity tables, NEC Table 310.15(B)(1) ambient temperature correction factors,
/// and wire-size recommendation. Project-specific custom tables can override defaults.
/// </summary>
public class NecAmpacityService
{
    // ── Standard wire size ordering (smallest → largest) ─────────────────────

    public static readonly string[] StandardSizes =
    {
        "14", "12", "10", "8", "6", "4", "3", "2", "1",
        "1/0", "2/0", "3/0", "4/0", "250", "300", "350", "400", "500"
    };

    // ── NEC Table 310.16 — Copper ────────────────────────────────────────────

    public static readonly AmpacityTable DefaultCopper60C = new()
    {
        Id = "NEC-310.16-Cu-60C",
        Name = "NEC Table 310.16 – Copper 60°C (TW, UF)",
        Material = ConductorMaterial.Copper,
        TemperatureRating = InsulationTemperatureRating.C60,
        Entries = new Dictionary<string, int>
        {
            ["14"] = 15, ["12"] = 20, ["10"] = 30, ["8"]  = 40,
            ["6"]  = 55, ["4"]  = 70, ["3"]  = 85, ["2"]  = 95,
            ["1"]  = 110, ["1/0"] = 125, ["2/0"] = 145, ["3/0"] = 165,
            ["4/0"] = 195, ["250"] = 215, ["300"] = 240, ["350"] = 260,
            ["400"] = 280, ["500"] = 320,
        }
    };

    public static readonly AmpacityTable DefaultCopper75C = new()
    {
        Id = "NEC-310.16-Cu-75C",
        Name = "NEC Table 310.16 – Copper 75°C (THW, THWN, XHHW)",
        Material = ConductorMaterial.Copper,
        TemperatureRating = InsulationTemperatureRating.C75,
        Entries = new Dictionary<string, int>
        {
            ["14"] = 20, ["12"] = 25, ["10"] = 35, ["8"]  = 50,
            ["6"]  = 65, ["4"]  = 85, ["3"]  = 100, ["2"]  = 115,
            ["1"]  = 130, ["1/0"] = 150, ["2/0"] = 175, ["3/0"] = 200,
            ["4/0"] = 230, ["250"] = 255, ["300"] = 285, ["350"] = 310,
            ["400"] = 335, ["500"] = 380,
        }
    };

    public static readonly AmpacityTable DefaultCopper90C = new()
    {
        Id = "NEC-310.16-Cu-90C",
        Name = "NEC Table 310.16 – Copper 90°C (THHN, THWN-2, XHHW-2)",
        Material = ConductorMaterial.Copper,
        TemperatureRating = InsulationTemperatureRating.C90,
        Entries = new Dictionary<string, int>
        {
            ["14"] = 25, ["12"] = 30, ["10"] = 40, ["8"]  = 55,
            ["6"]  = 75, ["4"]  = 95, ["3"]  = 115, ["2"]  = 130,
            ["1"]  = 145, ["1/0"] = 170, ["2/0"] = 195, ["3/0"] = 225,
            ["4/0"] = 260, ["250"] = 290, ["300"] = 320, ["350"] = 350,
            ["400"] = 380, ["500"] = 430,
        }
    };

    // ── NEC Table 310.16 — Aluminum ──────────────────────────────────────────

    public static readonly AmpacityTable DefaultAluminum60C = new()
    {
        Id = "NEC-310.16-Al-60C",
        Name = "NEC Table 310.16 – Aluminum 60°C (TW, UF)",
        Material = ConductorMaterial.Aluminum,
        TemperatureRating = InsulationTemperatureRating.C60,
        Entries = new Dictionary<string, int>
        {
            ["12"] = 15, ["10"] = 25, ["8"]  = 30,
            ["6"]  = 40, ["4"]  = 55, ["3"]  = 65, ["2"]  = 75,
            ["1"]  = 85, ["1/0"] = 100, ["2/0"] = 115, ["3/0"] = 130,
            ["4/0"] = 150, ["250"] = 170, ["300"] = 190, ["350"] = 210,
            ["400"] = 225, ["500"] = 260,
        }
    };

    public static readonly AmpacityTable DefaultAluminum75C = new()
    {
        Id = "NEC-310.16-Al-75C",
        Name = "NEC Table 310.16 – Aluminum 75°C (THW, THWN, XHHW)",
        Material = ConductorMaterial.Aluminum,
        TemperatureRating = InsulationTemperatureRating.C75,
        Entries = new Dictionary<string, int>
        {
            ["14"] = 15, ["12"] = 20, ["10"] = 30, ["8"]  = 40,
            ["6"]  = 50, ["4"]  = 65, ["3"]  = 75, ["2"]  = 90,
            ["1"]  = 100, ["1/0"] = 120, ["2/0"] = 135, ["3/0"] = 155,
            ["4/0"] = 180, ["250"] = 205, ["300"] = 230, ["350"] = 250,
            ["400"] = 270, ["500"] = 310,
        }
    };

    public static readonly AmpacityTable DefaultAluminum90C = new()
    {
        Id = "NEC-310.16-Al-90C",
        Name = "NEC Table 310.16 – Aluminum 90°C (THHN, THWN-2, XHHW-2)",
        Material = ConductorMaterial.Aluminum,
        TemperatureRating = InsulationTemperatureRating.C90,
        Entries = new Dictionary<string, int>
        {
            ["12"] = 25, ["10"] = 35, ["8"]  = 45,
            ["6"]  = 60, ["4"]  = 75, ["3"]  = 85, ["2"]  = 100,
            ["1"]  = 115, ["1/0"] = 135, ["2/0"] = 150, ["3/0"] = 175,
            ["4/0"] = 205, ["250"] = 230, ["300"] = 255, ["350"] = 280,
            ["400"] = 305, ["500"] = 350,
        }
    };

    // ── Default wire materials ───────────────────────────────────────────────

    public static readonly WireMaterial DefaultCopperThhn = new()
    {
        Id = "Cu-THHN",
        Name = "Copper THHN/THWN-2",
        Material = ConductorMaterial.Copper,
        BaseResistivityOhmsPerKFt = 1.98, // #12 AWG reference
        TemperatureRating = InsulationTemperatureRating.C90,
    };

    public static readonly WireMaterial DefaultAluminumXhhw = new()
    {
        Id = "Al-XHHW",
        Name = "Aluminum XHHW-2",
        Material = ConductorMaterial.Aluminum,
        BaseResistivityOhmsPerKFt = 3.25, // #12 AWG reference
        TemperatureRating = InsulationTemperatureRating.C90,
    };

    // ── NEC Table 310.15(B)(1) Correction Factors ────────────────────────────

    public static readonly CorrectionFactorTable DefaultCorrectionFactors = new()
    {
        Id = "NEC-310.15(B)(1)",
        Name = "NEC Table 310.15(B)(1) Ambient Temperature Correction",
        Entries = new List<CorrectionFactorEntry>
        {
            new() { AmbientTempMinC = 10, AmbientTempMaxC = 15, Factor60C = 1.29, Factor75C = 1.20, Factor90C = 1.15 },
            new() { AmbientTempMinC = 16, AmbientTempMaxC = 20, Factor60C = 1.22, Factor75C = 1.15, Factor90C = 1.11 },
            new() { AmbientTempMinC = 21, AmbientTempMaxC = 25, Factor60C = 1.15, Factor75C = 1.11, Factor90C = 1.08 },
            new() { AmbientTempMinC = 26, AmbientTempMaxC = 30, Factor60C = 1.00, Factor75C = 1.00, Factor90C = 1.00 },
            new() { AmbientTempMinC = 31, AmbientTempMaxC = 35, Factor60C = 0.91, Factor75C = 0.94, Factor90C = 0.96 },
            new() { AmbientTempMinC = 36, AmbientTempMaxC = 40, Factor60C = 0.82, Factor75C = 0.88, Factor90C = 0.91 },
            new() { AmbientTempMinC = 41, AmbientTempMaxC = 45, Factor60C = 0.71, Factor75C = 0.82, Factor90C = 0.87 },
            new() { AmbientTempMinC = 46, AmbientTempMaxC = 50, Factor60C = 0.58, Factor75C = 0.75, Factor90C = 0.82 },
            new() { AmbientTempMinC = 51, AmbientTempMaxC = 55, Factor60C = 0.41, Factor75C = 0.67, Factor90C = 0.76 },
            new() { AmbientTempMinC = 56, AmbientTempMaxC = 60, Factor60C = 0.00, Factor75C = 0.58, Factor90C = 0.71 },
            new() { AmbientTempMinC = 61, AmbientTempMaxC = 65, Factor60C = 0.00, Factor75C = 0.33, Factor90C = 0.58 },
            new() { AmbientTempMinC = 66, AmbientTempMaxC = 70, Factor60C = 0.00, Factor75C = 0.00, Factor90C = 0.41 },
        }
    };

    // ── Lookup Methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the default NEC Table 310.16 ampacity table for the given material and temperature rating.
    /// </summary>
    public static AmpacityTable GetDefaultTable(ConductorMaterial material, InsulationTemperatureRating rating)
    {
        return (material, rating) switch
        {
            (ConductorMaterial.Copper, InsulationTemperatureRating.C60) => DefaultCopper60C,
            (ConductorMaterial.Copper, InsulationTemperatureRating.C75) => DefaultCopper75C,
            (ConductorMaterial.Copper, InsulationTemperatureRating.C90) => DefaultCopper90C,
            (ConductorMaterial.Aluminum, InsulationTemperatureRating.C60) => DefaultAluminum60C,
            (ConductorMaterial.Aluminum, InsulationTemperatureRating.C75) => DefaultAluminum75C,
            (ConductorMaterial.Aluminum, InsulationTemperatureRating.C90) => DefaultAluminum90C,
            _ => DefaultCopper75C,
        };
    }

    /// <summary>
    /// Looks up ampacity for a wire size, using a custom table if provided, otherwise the NEC default.
    /// </summary>
    public static int LookupAmpacity(
        string wireSize,
        ConductorMaterial material,
        InsulationTemperatureRating rating = InsulationTemperatureRating.C75,
        AmpacityTable? customTable = null)
    {
        var table = customTable ?? GetDefaultTable(material, rating);
        return table.Lookup(wireSize);
    }

    /// <summary>
    /// Gets corrected ampacity after applying NEC 310.15(B)(1) ambient temperature correction.
    /// </summary>
    public static double GetCorrectedAmpacity(
        string wireSize,
        ConductorMaterial material,
        InsulationTemperatureRating rating,
        double ambientTempC,
        CorrectionFactorTable? customFactors = null,
        AmpacityTable? customTable = null)
    {
        int baseAmpacity = LookupAmpacity(wireSize, material, rating, customTable);
        var factors = customFactors ?? DefaultCorrectionFactors;
        double factor = factors.GetFactor(ambientTempC, rating);
        return baseAmpacity * factor;
    }

    /// <summary>
    /// Recommends the smallest standard wire size whose ampacity meets or exceeds the required current.
    /// Optionally applies ambient temperature correction. Returns null if no size is adequate.
    /// </summary>
    public static string? RecommendWireSize(
        double requiredAmps,
        ConductorMaterial material,
        InsulationTemperatureRating rating = InsulationTemperatureRating.C75,
        double? ambientTempC = null,
        AmpacityTable? customTable = null,
        CorrectionFactorTable? customFactors = null)
    {
        var table = customTable ?? GetDefaultTable(material, rating);
        var factors = customFactors ?? DefaultCorrectionFactors;

        foreach (var size in StandardSizes)
        {
            int baseAmpacity = table.Lookup(size);
            if (baseAmpacity <= 0) continue;

            double effectiveAmpacity = baseAmpacity;
            if (ambientTempC.HasValue)
            {
                double factor = factors.GetFactor(ambientTempC.Value, rating);
                effectiveAmpacity = baseAmpacity * factor;
            }

            if (effectiveAmpacity >= requiredAmps)
                return size;
        }

        return null;
    }

    /// <summary>
    /// Returns all six default NEC Table 310.16 ampacity tables.
    /// </summary>
    public static List<AmpacityTable> GetAllDefaultTables() => new()
    {
        DefaultCopper60C, DefaultCopper75C, DefaultCopper90C,
        DefaultAluminum60C, DefaultAluminum75C, DefaultAluminum90C,
    };

    /// <summary>
    /// Returns the default wire materials (Copper THHN, Aluminum XHHW).
    /// </summary>
    public static List<WireMaterial> GetDefaultWireMaterials() => new()
    {
        DefaultCopperThhn, DefaultAluminumXhhw,
    };
}

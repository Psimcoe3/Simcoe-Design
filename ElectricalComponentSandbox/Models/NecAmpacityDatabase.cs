namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Insulation temperature rating per NEC Table 310.16 column headers.
/// </summary>
public enum InsulationTemperatureRating
{
    C60,
    C75,
    C90
}

/// <summary>
/// A wire material definition with base resistivity and rated temperature.
/// </summary>
public record WireMaterial
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public ConductorMaterial Material { get; init; }
    public double BaseResistivityOhmsPerKFt { get; init; }
    public InsulationTemperatureRating TemperatureRating { get; init; }
}

/// <summary>
/// A named ampacity lookup table (AWG/kcmil → amps) for a specific material and temperature rating.
/// NEC Table 310.16 defaults are provided by <see cref="Services.NecAmpacityService"/>.
/// Users may override these with project-specific tables.
/// </summary>
public class AmpacityTable
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public ConductorMaterial Material { get; init; }
    public InsulationTemperatureRating TemperatureRating { get; init; }
    public Dictionary<string, int> Entries { get; init; } = new();

    /// <summary>
    /// Looks up ampacity for the given wire size. Returns 0 if not found.
    /// </summary>
    public int Lookup(string wireSize) =>
        Entries.TryGetValue(wireSize, out int amps) ? amps : 0;
}

/// <summary>
/// NEC 310.15(B)(1) ambient temperature correction factor entry.
/// Each row covers a temperature range and provides correction factors per insulation rating.
/// </summary>
public record CorrectionFactorEntry
{
    public int AmbientTempMinC { get; init; }
    public int AmbientTempMaxC { get; init; }
    public double Factor60C { get; init; }
    public double Factor75C { get; init; }
    public double Factor90C { get; init; }
}

/// <summary>
/// NEC 310.15(B)(1) ambient temperature correction factor table.
/// </summary>
public class CorrectionFactorTable
{
    public string Id { get; init; } = "NEC-310.15(B)(1)";
    public string Name { get; init; } = "NEC Table 310.15(B)(1) Ambient Temperature Correction";
    public List<CorrectionFactorEntry> Entries { get; init; } = new();

    /// <summary>
    /// Returns the correction factor for the given ambient temperature and insulation rating.
    /// Returns 1.0 if no matching entry is found (assumes 30°C base).
    /// </summary>
    public double GetFactor(double ambientTempC, InsulationTemperatureRating rating)
    {
        foreach (var entry in Entries)
        {
            if (ambientTempC >= entry.AmbientTempMinC && ambientTempC <= entry.AmbientTempMaxC)
            {
                return rating switch
                {
                    InsulationTemperatureRating.C60 => entry.Factor60C,
                    InsulationTemperatureRating.C75 => entry.Factor75C,
                    InsulationTemperatureRating.C90 => entry.Factor90C,
                    _ => 1.0
                };
            }
        }
        return 1.0;
    }
}

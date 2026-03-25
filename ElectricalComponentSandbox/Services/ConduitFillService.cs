using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Calculates conduit fill percentage per NEC 2023 Chapter 9, Table 1
/// and validates against code limits.
/// </summary>
public class ConduitFillService
{
    // ── Trade sizes in ascending order ───────────────────────────────────────

    private static readonly string[] TradeSizes =
    [
        "1/2", "3/4", "1", "1-1/4", "1-1/2", "2",
        "2-1/2", "3", "3-1/2", "4"
    ];

    // ── Conduit inner areas (sq in) per NEC Chapter 9, Table 4 ───────────────

    private static readonly Dictionary<string, double> EmtAreas = new()
    {
        ["1/2"]   = 0.304,
        ["3/4"]   = 0.533,
        ["1"]     = 0.864,
        ["1-1/4"] = 1.496,
        ["1-1/2"] = 2.036,
        ["2"]     = 3.356,
        ["2-1/2"] = 5.858,
        ["3"]     = 8.846,
        ["3-1/2"] = 11.545,
        ["4"]     = 14.753,
    };

    private static readonly Dictionary<string, double> RmcAreas = new()
    {
        ["1/2"]   = 0.314,
        ["3/4"]   = 0.533,
        ["1"]     = 0.887,
        ["1-1/4"] = 1.526,
        ["1-1/2"] = 2.071,
        ["2"]     = 3.408,
        ["2-1/2"] = 5.940,
        ["3"]     = 9.000,
        ["3-1/2"] = 11.726,
        ["4"]     = 14.963,
    };

    private static readonly Dictionary<string, double> PvcAreas = new()
    {
        ["1/2"]   = 0.285,
        ["3/4"]   = 0.508,
        ["1"]     = 0.832,
        ["1-1/4"] = 1.453,
        ["1-1/2"] = 1.986,
        ["2"]     = 3.291,
        ["2-1/2"] = 5.281,
        ["3"]     = 8.091,
        ["3-1/2"] = 10.649,
        ["4"]     = 13.631,
    };

    // ── Wire cross-sectional areas including insulation (THHN/THWN, sq in) ──
    //    NEC Chapter 9, Table 5

    private static readonly Dictionary<string, double> WireAreas = new()
    {
        ["14"]  = 0.0097,
        ["12"]  = 0.0133,
        ["10"]  = 0.0211,
        ["8"]   = 0.0366,
        ["6"]   = 0.0507,
        ["4"]   = 0.0824,
        ["3"]   = 0.0973,
        ["2"]   = 0.1158,
        ["1"]   = 0.1562,
        ["1/0"] = 0.1855,
        ["2/0"] = 0.2223,
        ["3/0"] = 0.2679,
        ["4/0"] = 0.3237,
        ["250"] = 0.3970,
        ["300"] = 0.4608,
        ["350"] = 0.5242,
        ["400"] = 0.5863,
        ["500"] = 0.7073,
    };

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the maximum allowable fill percentage based on the number of
    /// conductors per NEC 2023 Chapter 9, Table 1.
    /// </summary>
    /// <param name="conductorCount">Number of conductors in the conduit.</param>
    /// <returns>Maximum fill percentage (e.g. 40.0 for 40%).</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="conductorCount"/> is less than 1.
    /// </exception>
    public double GetMaxFillPercent(int conductorCount)
    {
        return conductorCount switch
        {
            < 1  => throw new ArgumentOutOfRangeException(
                         nameof(conductorCount), "Conductor count must be at least 1."),
            1    => 53.0,
            2    => 31.0,
            _    => 40.0,
        };
    }

    /// <summary>
    /// Returns the inner cross-sectional area of a conduit in square inches.
    /// Materials without dedicated tables (IMC, FMC, LFMC, LFNC, ENT) fall
    /// back to EMT values.
    /// </summary>
    /// <param name="tradeSize">Trade size designation (e.g. "1/2", "3/4", "1").</param>
    /// <param name="material">Conduit material type.</param>
    /// <returns>Inner area in square inches.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the trade size is not found in the lookup table.
    /// </exception>
    public double GetConduitArea(string tradeSize, ConduitMaterialType material)
    {
        var table = GetAreaTable(material);

        if (table.TryGetValue(tradeSize, out var area))
            return area;

        throw new ArgumentException(
            $"Unknown trade size '{tradeSize}'. Valid sizes: {string.Join(", ", TradeSizes)}",
            nameof(tradeSize));
    }

    /// <summary>
    /// Returns the cross-sectional area of a THHN/THWN insulated wire in
    /// square inches.
    /// </summary>
    /// <param name="wireSize">Wire gauge (e.g. "12", "1/0", "250").</param>
    /// <returns>Wire area in square inches, including insulation.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the wire size is not found in the lookup table.
    /// </exception>
    public double GetWireArea(string wireSize)
    {
        if (WireAreas.TryGetValue(wireSize, out var area))
            return area;

        throw new ArgumentException(
            $"Unknown wire size '{wireSize}'. Valid sizes: {string.Join(", ", WireAreas.Keys)}",
            nameof(wireSize));
    }

    /// <summary>
    /// Calculates conduit fill for a set of wires in a given conduit and
    /// checks compliance with NEC 2023 Chapter 9, Table 1.
    /// </summary>
    /// <param name="conduitTradeSize">Conduit trade size (e.g. "3/4").</param>
    /// <param name="material">Conduit material type.</param>
    /// <param name="wireSizes">List of wire sizes to place in the conduit.</param>
    /// <returns>A <see cref="ConduitFillResult"/> with fill details.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when a conduit or wire size is invalid.
    /// </exception>
    public ConduitFillResult CalculateFill(
        string conduitTradeSize,
        ConduitMaterialType material,
        IReadOnlyList<string> wireSizes)
    {
        ArgumentNullException.ThrowIfNull(wireSizes);

        var conduitArea = GetConduitArea(conduitTradeSize, material);
        var conductorCount = wireSizes.Count;
        var totalWireArea = 0.0;

        foreach (var size in wireSizes)
            totalWireArea += GetWireArea(size);

        var fillPercent = conduitArea > 0
            ? totalWireArea / conduitArea * 100.0
            : 0.0;

        var maxAllowed = conductorCount > 0
            ? GetMaxFillPercent(conductorCount)
            : 0.0;

        return new ConduitFillResult
        {
            ConduitAreaSqIn = conduitArea,
            TotalWireAreaSqIn = totalWireArea,
            FillPercent = Math.Round(fillPercent, 2),
            MaxAllowedFillPercent = maxAllowed,
            ConductorCount = conductorCount,
            ExceedsCode = fillPercent > maxAllowed,
        };
    }

    /// <summary>
    /// Recommends the minimum conduit trade size for a given material that can
    /// accommodate the supplied wires without exceeding NEC fill limits.
    /// </summary>
    /// <param name="material">Conduit material type.</param>
    /// <param name="wireSizes">List of wire sizes to place in the conduit.</param>
    /// <returns>
    /// The smallest trade size that passes NEC fill requirements, or
    /// <c>null</c> if no standard size is large enough.
    /// </returns>
    public string? RecommendConduitSize(ConduitMaterialType material, IReadOnlyList<string> wireSizes)
    {
        ArgumentNullException.ThrowIfNull(wireSizes);

        if (wireSizes.Count == 0)
            return TradeSizes[0]; // smallest available

        var totalWireArea = 0.0;
        foreach (var size in wireSizes)
            totalWireArea += GetWireArea(size);

        var maxFill = GetMaxFillPercent(wireSizes.Count) / 100.0;
        var requiredArea = totalWireArea / maxFill;

        var table = GetAreaTable(material);

        foreach (var tradeSize in TradeSizes)
        {
            if (table.TryGetValue(tradeSize, out var conduitArea) && conduitArea >= requiredArea)
                return tradeSize;
        }

        return null; // no standard size is large enough
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static Dictionary<string, double> GetAreaTable(ConduitMaterialType material)
    {
        return material switch
        {
            ConduitMaterialType.RMC => RmcAreas,
            ConduitMaterialType.PVC => PvcAreas,
            _                      => EmtAreas, // EMT, IMC, FMC, LFMC, LFNC, ENT
        };
    }
}

/// <summary>
/// Result of a conduit fill calculation per NEC 2023 Chapter 9, Table 1.
/// </summary>
public class ConduitFillResult
{
    /// <summary>Inner cross-sectional area of the conduit (sq in).</summary>
    public double ConduitAreaSqIn { get; init; }

    /// <summary>Total cross-sectional area of all conductors (sq in).</summary>
    public double TotalWireAreaSqIn { get; init; }

    /// <summary>Actual fill percentage (wire area / conduit area * 100).</summary>
    public double FillPercent { get; init; }

    /// <summary>Maximum fill percentage allowed by NEC for this conductor count.</summary>
    public double MaxAllowedFillPercent { get; init; }

    /// <summary>Number of conductors in the conduit.</summary>
    public int ConductorCount { get; init; }

    /// <summary>True when the fill percentage exceeds the NEC code limit.</summary>
    public bool ExceedsCode { get; init; }

    /// <summary>NEC code reference for the fill calculation.</summary>
    public string NecReference { get; init; } = "NEC Chapter 9, Table 1";
}

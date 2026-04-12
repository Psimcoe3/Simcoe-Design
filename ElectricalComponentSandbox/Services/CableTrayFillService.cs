using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Result of a cable tray fill calculation per NEC 392.
/// </summary>
public class CableTrayFillResult
{
    /// <summary>Usable cross-section area of the tray in sq inches.</summary>
    public double TrayAreaSqIn { get; init; }

    /// <summary>Sum of all cable cross-section areas in sq inches.</summary>
    public double TotalCableAreaSqIn { get; init; }

    /// <summary>Fill percentage (cable area / tray area × 100).</summary>
    public double FillPercent { get; init; }

    /// <summary>Maximum allowed fill percent per NEC 392.22.</summary>
    public double MaxAllowedFillPercent { get; init; }

    /// <summary>Number of cables in the tray.</summary>
    public int CableCount { get; init; }

    /// <summary>Whether the fill exceeds NEC 392 limits.</summary>
    public bool ExceedsCode { get; init; }

    /// <summary>Applicable NEC reference.</summary>
    public string NecReference { get; init; } = "NEC 392.22";
}

/// <summary>
/// Cable tray fill calculations per NEC Article 392.
/// </summary>
public static class CableTrayFillService
{
    /// <summary>
    /// NEC 392.22 maximum fill percentages by tray type.
    /// Ladder and ventilated trough allow single-layer for large cables,
    /// but general mixed-cable fill is limited to the percentages below.
    /// </summary>
    public static double GetMaxFillPercent(CableTrayType trayType) => trayType switch
    {
        CableTrayType.Ladder => 50.0,             // NEC 392.22(A) — multiconductor cables
        CableTrayType.VentilatedTrough => 50.0,    // NEC 392.22(A)
        CableTrayType.SolidBottom => 40.0,         // NEC 392.22(B) — reduced ventilation
        CableTrayType.Channel => 50.0,             // NEC 392.22(C)
        CableTrayType.Wire => 50.0,
        CableTrayType.SingleRail => 50.0,
        _ => 40.0
    };

    /// <summary>
    /// Computes the usable cross-section area of a cable tray.
    /// </summary>
    /// <param name="widthInches">Tray width in inches.</param>
    /// <param name="depthInches">Tray loading depth in inches.</param>
    /// <returns>Usable area in square inches.</returns>
    public static double GetTrayArea(double widthInches, double depthInches) =>
        widthInches * depthInches;

    /// <summary>
    /// Calculates fill for a cable tray given its dimensions and the cables it contains.
    /// </summary>
    public static CableTrayFillResult CalculateFill(
        double widthInches,
        double depthInches,
        CableTrayType trayType,
        IEnumerable<CableSpec> cables)
    {
        double trayArea = GetTrayArea(widthInches, depthInches);
        double totalCableArea = 0;
        int cableCount = 0;

        foreach (var cable in cables)
        {
            totalCableArea += cable.TotalAreaSqIn;
            cableCount += cable.Quantity;
        }

        double fillPercent = trayArea > 0
            ? Math.Round(totalCableArea / trayArea * 100, 2)
            : 0;

        double maxFill = GetMaxFillPercent(trayType);

        return new CableTrayFillResult
        {
            TrayAreaSqIn = trayArea,
            TotalCableAreaSqIn = totalCableArea,
            FillPercent = fillPercent,
            MaxAllowedFillPercent = maxFill,
            CableCount = cableCount,
            ExceedsCode = fillPercent > maxFill,
            NecReference = "NEC 392.22"
        };
    }

    /// <summary>
    /// Recommends the minimum tray width (in standard increments) that satisfies
    /// NEC 392 fill limits for a given depth and set of cables.
    /// Standard widths: 6, 12, 18, 24, 30, 36 inches.
    /// </summary>
    public static double? RecommendTrayWidth(
        double depthInches,
        CableTrayType trayType,
        IEnumerable<CableSpec> cables)
    {
        double[] standardWidths = { 6, 12, 18, 24, 30, 36 };
        var cableList = cables.ToList();

        foreach (double width in standardWidths)
        {
            var result = CalculateFill(width, depthInches, trayType, cableList);
            if (!result.ExceedsCode)
                return width;
        }

        return null; // No standard width satisfies NEC 392
    }
}

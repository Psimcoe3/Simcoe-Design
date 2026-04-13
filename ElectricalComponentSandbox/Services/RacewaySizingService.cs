using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Raceway sizing per NEC 376 (wireways) and pull/junction box sizing
/// per NEC 312/314. Includes conduit body minimum dimensions.
/// </summary>
public static class RacewaySizingService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum RacewayType
    {
        MetalWireway,        // NEC 376
        NonmetallicWireway,  // NEC 378
        Gutter,              // Auxiliary gutters NEC 366
    }

    public enum BoxType
    {
        Pull,                // NEC 314.28(A)(1) — straight pull
        AnglePull,           // NEC 314.28(A)(2) — angle/U pull
        Junction,            // NEC 314.28 general
    }

    /// <summary>Standard wireway cross-section sizes (inches).</summary>
    public static readonly (double Width, double Height)[] StandardWirewaySizes =
    {
        (2.5, 2.5),
        (4.0, 4.0),
        (6.0, 6.0),
        (8.0, 8.0),
        (10.0, 10.0),
        (12.0, 12.0),
    };

    /// <summary>Wire area in sq-in by AWG/kcmil for THHN/THWN-2 (typical).</summary>
    public static double GetWireArea(string wireSize)
    {
        // Per NEC Chapter 9 Table 5 — THHN/THWN-2 conductor areas
        return wireSize.ToUpperInvariant() switch
        {
            "14" => 0.0097,
            "12" => 0.0133,
            "10" => 0.0211,
            "8"  => 0.0366,
            "6"  => 0.0507,
            "4"  => 0.0824,
            "3"  => 0.0973,
            "2"  => 0.1158,
            "1"  => 0.1562,
            "1/0" => 0.1855,
            "2/0" => 0.2223,
            "3/0" => 0.2679,
            "4/0" => 0.3237,
            "250" => 0.3970,
            "300" => 0.4608,
            "350" => 0.5242,
            "400" => 0.5863,
            "500" => 0.7073,
            "600" => 0.8676,
            "750" => 1.0496,
            _ => throw new ArgumentException($"Unknown wire size: {wireSize}"),
        };
    }

    // ── Wireway Fill Calculation ─────────────────────────────────────────────

    /// <summary>Conductor fill result for a wireway.</summary>
    public record WirewayFillResult
    {
        public double TotalConductorAreaSqIn { get; init; }
        public double WirewayAreaSqIn { get; init; }
        public double FillPercent { get; init; }
        public double MaxFillPercent { get; init; }
        public bool IsCompliant { get; init; }
        public (double Width, double Height) RecommendedSize { get; init; }
    }

    /// <summary>
    /// Calculates wireway fill per NEC 376.22 (metal) / 378.22 (nonmetallic).
    /// Maximum 20% fill for conductors at any cross section.
    /// </summary>
    /// <param name="conductors">List of (wireSize, count) tuples.</param>
    /// <param name="wirewayWidth">Proposed wireway width in inches.</param>
    /// <param name="wirewayHeight">Proposed wireway height in inches.</param>
    /// <param name="racewayType">Type of raceway.</param>
    public static WirewayFillResult CalculateWirewayFill(
        IEnumerable<(string WireSize, int Count)> conductors,
        double wirewayWidth, double wirewayHeight,
        RacewayType racewayType = RacewayType.MetalWireway)
    {
        double totalArea = 0;
        foreach (var (wireSize, count) in conductors)
        {
            totalArea += GetWireArea(wireSize) * count;
        }

        double wirewayArea = wirewayWidth * wirewayHeight;
        double maxFill = 20.0; // NEC 376.22(A): 20% max
        double fillPercent = wirewayArea > 0 ? (totalArea / wirewayArea) * 100.0 : 100.0;
        bool compliant = fillPercent <= maxFill;

        // Find minimum standard size that meets 20% fill
        var recommended = StandardWirewaySizes
            .Where(s => totalArea / (s.Width * s.Height) * 100.0 <= maxFill)
            .FirstOrDefault();
        if (recommended == default)
            recommended = StandardWirewaySizes[^1];

        return new WirewayFillResult
        {
            TotalConductorAreaSqIn = Math.Round(totalArea, 4),
            WirewayAreaSqIn = wirewayArea,
            FillPercent = Math.Round(fillPercent, 1),
            MaxFillPercent = maxFill,
            IsCompliant = compliant,
            RecommendedSize = recommended,
        };
    }

    // ── Pull Box Sizing ──────────────────────────────────────────────────────

    /// <summary>Pull/junction box sizing result.</summary>
    public record BoxSizeResult
    {
        public BoxType Type { get; init; }
        public double MinLengthInches { get; init; }
        public double MinWidthInches { get; init; }
        public double MinDepthInches { get; init; }
        public string NecReference { get; init; } = "";
    }

    /// <summary>
    /// Sizes a pull or junction box per NEC 314.28.
    /// <para>Straight pull (314.28(A)(1)): L ≥ 8 × largest trade size.</para>
    /// <para>Angle/U pull (314.28(A)(2)): L ≥ 6 × largest + sum of other entries on same wall.</para>
    /// </summary>
    /// <param name="boxType">Pull or angle pull.</param>
    /// <param name="largestConduitTradeSize">Largest conduit trade size in inches.</param>
    /// <param name="otherConduitTradeSizes">Trade sizes of other conduits entering on same wall.</param>
    public static BoxSizeResult SizePullBox(
        BoxType boxType,
        double largestConduitTradeSize,
        IEnumerable<double>? otherConduitTradeSizes = null)
    {
        double otherSum = otherConduitTradeSizes?.Sum() ?? 0;

        double minLength;
        string necRef;

        if (boxType == BoxType.Pull)
        {
            // NEC 314.28(A)(1): 8 × largest trade size
            minLength = 8 * largestConduitTradeSize;
            necRef = "NEC 314.28(A)(1)";
        }
        else
        {
            // NEC 314.28(A)(2): 6 × largest + sum of others on same wall
            minLength = 6 * largestConduitTradeSize + otherSum;
            necRef = "NEC 314.28(A)(2)";
        }

        // Width ≥ distance per 314.28(A)(2) for angle pulls
        double minWidth = boxType == BoxType.Pull
            ? largestConduitTradeSize * 2
            : 6 * largestConduitTradeSize + otherSum;

        // Depth: at minimum, accommodate the largest conduit
        double minDepth = Math.Max(largestConduitTradeSize * 2, 4);

        return new BoxSizeResult
        {
            Type = boxType,
            MinLengthInches = Math.Round(minLength, 1),
            MinWidthInches = Math.Round(minWidth, 1),
            MinDepthInches = Math.Round(minDepth, 1),
            NecReference = necRef,
        };
    }

    // ── Gutter Space ─────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates minimum gutter space (bending space) in a panel or enclosure
    /// per NEC 312.6. Returns minimum bending-space depth in inches.
    /// </summary>
    /// <param name="largestWireSize">Largest conductor size (AWG/kcmil string).</param>
    /// <param name="wiresPerTerminal">Number of wires per terminal (1 or 2).</param>
    public static double GetMinGutterDepth(string largestWireSize, int wiresPerTerminal = 1)
    {
        // Simplified NEC Table 312.6(A) for one wire per terminal
        double depth = largestWireSize.ToUpperInvariant() switch
        {
            "14" or "12" or "10" => 1.5,
            "8" or "6" => 2.0,
            "4" or "3" => 2.5,
            "2" or "1" => 3.0,
            "1/0" or "2/0" => 3.5,
            "3/0" or "4/0" => 4.0,
            "250" or "300" => 5.0,
            "350" or "400" => 6.0,
            "500" => 7.0,
            "600" => 8.0,
            "750" => 8.0,
            _ => 4.0,
        };

        // Two wires per terminal requires more depth per NEC 312.6(B)
        if (wiresPerTerminal >= 2)
            depth *= 1.5;

        return Math.Round(depth, 1);
    }

    // ── Minimum Number of Conductors in Wireway ──────────────────────────────

    /// <summary>
    /// NEC 376.22(B) / 378.22(B): Max 30 current-carrying conductors
    /// at any cross section (derating applies beyond 20).
    /// Returns whether the count is within limit and if derating applies.
    /// </summary>
    public static (bool IsWithinLimit, bool DeratingRequired) CheckConductorCount(int currentCarryingConductors)
    {
        bool withinLimit = currentCarryingConductors <= 30;
        bool deratingRequired = currentCarryingConductors > 20;
        return (withinLimit, deratingRequired);
    }
}

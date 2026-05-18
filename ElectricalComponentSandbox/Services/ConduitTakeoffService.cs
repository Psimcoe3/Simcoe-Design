using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Computes NEC bending deducts and conduit run takeoff lengths for field
/// prefabrication.  Deduct values are based on published bender-wheel tables
/// (Klein/Greenlee) and the geometric offset formula.
/// </summary>
public static class ConduitTakeoffService
{
    // ── 90-degree stub deducts by trade size (inches) ────────────────────
    // Source: Klein/Greenlee bender-wheel reference and NEC 358.24 / 344.24

    private static readonly Dictionary<string, double> Deduct90Emt = new()
    {
        ["1/2"]   =  5.0,
        ["3/4"]   =  6.0,
        ["1"]     =  8.0,
        ["1-1/4"] = 11.0,
        ["1-1/2"] = 13.0,
        ["2"]     = 16.0,
        ["2-1/2"] = 22.0,
        ["3"]     = 26.0,
        ["3-1/2"] = 31.0,
        ["4"]     = 36.0,
    };

    private static readonly Dictionary<string, double> Deduct90Rmc = new()
    {
        ["1/2"]   =  6.0,
        ["3/4"]   =  8.0,
        ["1"]     = 11.0,
        ["1-1/4"] = 14.0,
        ["1-1/2"] = 17.0,
        ["2"]     = 21.0,
        ["2-1/2"] = 27.0,
        ["3"]     = 33.0,
        ["3-1/2"] = 38.0,
        ["4"]     = 43.0,
    };

    // ── 45-degree deducts by trade size (inches) ─────────────────────────

    private static readonly Dictionary<string, double> Deduct45Emt = new()
    {
        ["1/2"]   = 2.5,
        ["3/4"]   = 3.0,
        ["1"]     = 4.0,
        ["1-1/4"] = 5.0,
        ["1-1/2"] = 6.0,
        ["2"]     = 8.0,
        ["2-1/2"] = 11.0,
        ["3"]     = 13.0,
        ["3-1/2"] = 16.0,
        ["4"]     = 18.0,
    };

    private static readonly Dictionary<string, double> Deduct45Rmc = new()
    {
        ["1/2"]   = 3.0,
        ["3/4"]   = 4.0,
        ["1"]     = 5.5,
        ["1-1/4"] = 7.0,
        ["1-1/2"] = 8.5,
        ["2"]     = 10.5,
        ["2-1/2"] = 13.5,
        ["3"]     = 16.5,
        ["3-1/2"] = 19.0,
        ["4"]     = 21.0,
    };

    // ── NEC support spacing (feet) by material ────────────────────────────
    // EMT NEC 358.30: ≤10 ft.  RMC NEC 344.30: ≤10 ft.  PVC NEC 352.30.

    private static readonly Dictionary<string, double> PvcSupportSpacing = new()
    {
        ["1/2"]   = 3.0,
        ["3/4"]   = 3.0,
        ["1"]     = 3.0,
        ["1-1/4"] = 5.0,
        ["1-1/2"] = 5.0,
        ["2"]     = 5.0,
        ["2-1/2"] = 6.0,
        ["3"]     = 6.0,
        ["3-1/2"] = 8.0,
        ["4"]     = 8.0,
    };

    // ── Public API ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the NEC bending deduct in inches for a 90° stub bend.
    /// PVC and other materials fall back to EMT values.
    /// </summary>
    public static double GetDeduct90(string tradeSize, ConduitMaterialType material)
    {
        var table = material == ConduitMaterialType.RMC ? Deduct90Rmc : Deduct90Emt;
        return table.TryGetValue(tradeSize, out var d) ? d : 0;
    }

    /// <summary>
    /// Returns the NEC bending deduct in inches for a 45° bend.
    /// PVC and other materials fall back to EMT values.
    /// </summary>
    public static double GetDeduct45(string tradeSize, ConduitMaterialType material)
    {
        var table = material == ConduitMaterialType.RMC ? Deduct45Rmc : Deduct45Emt;
        return table.TryGetValue(tradeSize, out var d) ? d : 0;
    }

    /// <summary>
    /// Computes the shrinkage deduct (inches) for a two-bend parallel offset.
    /// Formula: deduct = offsetInches × (1 − cos θ) / sin θ
    /// </summary>
    /// <param name="offsetInches">Perpendicular offset distance in inches.</param>
    /// <param name="bendAngleDegrees">Bend angle of each bend in degrees (typically 22.5, 30, 45, 60).</param>
    public static double ComputeOffsetDeduct(double offsetInches, double bendAngleDegrees)
    {
        if (offsetInches <= 0) return 0;
        var radians = bendAngleDegrees * Math.PI / 180.0;
        var sinA = Math.Sin(radians);
        if (Math.Abs(sinA) < 1e-9) return 0;
        return offsetInches * (1.0 - Math.Cos(radians)) / sinA;
    }

    /// <summary>
    /// Returns the maximum support spacing in feet per NEC for the given
    /// trade size and material.
    /// </summary>
    public static double GetSupportSpacing(string tradeSize, ConduitMaterialType material)
    {
        if (material == ConduitMaterialType.PVC)
        {
            return PvcSupportSpacing.TryGetValue(tradeSize, out var pvcSpacing)
                ? pvcSpacing
                : 5.0;
        }

        return 10.0; // EMT 358.30, RMC 344.30, IMC 342.30 all ≤ 10 ft
    }

    /// <summary>
    /// Computes a full takeoff for a conduit run: adjusted footage (gross
    /// minus fitting deducts) and the recommended support count.
    /// </summary>
    public static ConduitRunTakeoff ComputeRunTakeoff(
        ConduitModelStore store,
        string runId)
    {
        ArgumentNullException.ThrowIfNull(store);

        var run = store.GetRun(runId)
            ?? throw new ArgumentException($"Run '{runId}' not found.", nameof(runId));

        var grossFeet = run.ComputeTotalLength(store);
        var fittingTakeoffs = BuildFittingTakeoffs(store, run);
        var totalDeductIn = fittingTakeoffs.Sum(f => f.DeductInches);
        var adjustedFeet = Math.Max(0, grossFeet - totalDeductIn / 12.0);
        var spacingFeet = GetSupportSpacing(run.TradeSize, run.Material);
        var supportCount = spacingFeet > 0
            ? (int)Math.Ceiling(adjustedFeet / spacingFeet) + 1
            : 0;

        return new ConduitRunTakeoff(
            RunId: runId,
            GrossLengthFeet: grossFeet,
            AdjustedLengthFeet: adjustedFeet,
            TotalDeductInches: totalDeductIn,
            Fittings: fittingTakeoffs,
            RecommendedSupportCount: supportCount,
            SupportSpacingFeet: spacingFeet,
            AuditTrace: BuildRunAuditTrace(run, grossFeet, totalDeductIn, adjustedFeet, spacingFeet, supportCount));
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private static IReadOnlyList<ConduitFittingTakeoff> BuildFittingTakeoffs(
        ConduitModelStore store, ConduitRun run)
    {
        var list = new List<ConduitFittingTakeoff>();
        foreach (var fid in run.FittingIds)
        {
            var fitting = store.GetFitting(fid);
            if (fitting == null) continue;
            var (category, deduct, basis) = ClassifyFitting(fitting, run.Material);
            list.Add(new ConduitFittingTakeoff(
                FittingId: fid,
                Category: category,
                TradeSize: fitting.TradeSize,
                AngleDegrees: fitting.AngleDegrees,
                DeductInches: deduct,
                CalculationBasis: basis,
                AuditTrace: BuildFittingAuditTrace(fitting, category, deduct, basis, run.Material)));
        }

        return list;
    }

    private static (TakeoffFittingCategory category, double deductInches, string basis) ClassifyFitting(
        ConduitFitting fitting, ConduitMaterialType material)
    {
        // Use stored DeductLength when it has been explicitly set
        if (fitting.DeductLength > 0)
            return (MapCategory(fitting.Type), fitting.DeductLength, "stored-deduct");

        return fitting.Type switch
        {
            FittingType.Elbow90 =>
                (TakeoffFittingCategory.Elbow90, GetDeduct90(fitting.TradeSize, material), "deduct90-table"),
            FittingType.Elbow45 =>
                (TakeoffFittingCategory.Elbow45, GetDeduct45(fitting.TradeSize, material), "deduct45-table"),
            FittingType.Offset =>
                (TakeoffFittingCategory.Offset,
                    ComputeOffsetDeduct(fitting.AngleDegrees, 45),
                    "offset-formula-default45"),   // default 45° offset bend
            _ =>
                (MapCategory(fitting.Type), 0, "no-deduct"),
        };
    }

    private static string BuildFittingAuditTrace(
        ConduitFitting fitting,
        TakeoffFittingCategory category,
        double deductInches,
        string basis,
        ConduitMaterialType material)
    {
        return $"fitting={fitting.Id}; type={fitting.Type}; category={category}; tradeSize={fitting.TradeSize}; angleDeg={fitting.AngleDegrees:F2}; material={material}; basis={basis}; deductIn={deductInches:F3}";
    }

    private static string BuildRunAuditTrace(
        ConduitRun run,
        double grossFeet,
        double totalDeductInches,
        double adjustedFeet,
        double spacingFeet,
        int supportCount)
    {
        return $"run={run.RunId}; material={run.Material}; tradeSize={run.TradeSize}; grossFt={grossFeet:F3}; totalDeductIn={totalDeductInches:F3}; adjustedFt={adjustedFeet:F3}; supportSpacingFt={spacingFeet:F3}; supportCount={supportCount}; formula=adjusted=max(0,gross-deduct/12)";
    }

    private static TakeoffFittingCategory MapCategory(FittingType t) =>
        t switch
        {
            FittingType.Elbow90   => TakeoffFittingCategory.Elbow90,
            FittingType.Elbow45   => TakeoffFittingCategory.Elbow45,
            FittingType.Offset    => TakeoffFittingCategory.Offset,
            FittingType.Coupling  => TakeoffFittingCategory.Coupling,
            _                     => TakeoffFittingCategory.Unknown,
        };
}

using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Bend pattern classifications that determine how the per-bend marks
/// (<c>Mark1–Mark4</c>) and dimensions (<c>DimA–DimF</c>) are interpreted on
/// a fabrication drawing. Names parallel the eVolve spool template families.
/// </summary>
public enum BendSchedulePattern
{
    /// <summary>Run has no bends — straight stick.</summary>
    Straight,

    /// <summary>Single 90° stub up or stub down.</summary>
    Stub90,

    /// <summary>Single 90° kicked in plan (not vertical).</summary>
    Kick90,

    /// <summary>Two equal-angle offset bends (one offset).</summary>
    Offset,

    /// <summary>Two pairs of offset bends — common for routing past two obstacles.</summary>
    OffsetTwoPiece,

    /// <summary>3-bend saddle (22.5/45/22.5 or 30/60/30 — bend-rise-bend).</summary>
    Saddle3Point,

    /// <summary>4-bend saddle (offset-rise-offset).</summary>
    Saddle4Point,

    /// <summary>Bend pattern that does not match any standard template.</summary>
    Custom,
}

/// <summary>
/// One row of the per-bend schedule. The schema mirrors the eVolve
/// <c>eE_ConduitBend_*</c> shared parameters: each bend is described by up to
/// two angles, a deduct, up to four cumulative marks measured from the start
/// of the stick, six geometric dimensions (DimA–DimF), and a bender wheel
/// reference. Unused values are zero so the report renders cleanly.
/// </summary>
public sealed record BendScheduleRow(
    int BendNumber,
    string FittingId,
    BendSchedulePattern Pattern,
    double Angle1Degrees,
    double Angle2Degrees,
    string TradeSize,
    ConduitMaterialType Material,
    double DeductInches,
    double Mark1Inches,
    double Mark2Inches,
    double Mark3Inches,
    double Mark4Inches,
    double DimAInches,
    double DimBInches,
    double DimCInches,
    double DimDInches,
    double DimEInches,
    double DimFInches,
    string BenderType,
    string Notes);

/// <summary>
/// Per-run bend schedule built from a <see cref="ConduitRun"/>. Drives the
/// "Bend Marks" table on a spool sheet — exactly the data a bender at the
/// shop needs to lay marks on a stick of conduit.
/// </summary>
public sealed record BendSchedule(
    string RunId,
    string TradeSize,
    ConduitMaterialType Material,
    BendSchedulePattern OverallPattern,
    IReadOnlyList<BendScheduleRow> Bends,
    string AuditTrace);

/// <summary>
/// Builds per-bend mark schedules for a conduit run. Sits one level above
/// <see cref="ConduitTakeoffService"/>: takeoff computes the run-level
/// adjusted footage and per-fitting deducts, while this service emits the
/// row-per-bend layout the fabricator needs.
/// </summary>
public static class BendScheduleService
{
    /// <summary>Default bender wheel reference used when none is stored on the fitting.</summary>
    public const string DefaultBenderType = "Greenlee 555/881";

    /// <summary>
    /// Computes a bend schedule for a single run.
    /// </summary>
    public static BendSchedule ComputeForRun(ConduitModelStore store, string runId)
    {
        ArgumentNullException.ThrowIfNull(store);

        var run = store.GetRun(runId)
            ?? throw new ArgumentException($"Run '{runId}' not found.", nameof(runId));

        var segments = run.GetSegments(store).ToList();
        var fittings = run.GetFittings(store).ToList();
        var bendFittings = OrderBendsAlongRun(run, segments, fittings);
        var rows = new List<BendScheduleRow>(bendFittings.Count);

        // Cumulative position (inches) along the run, measured from the start
        // of the stick. We accumulate raw segment length so the marks reflect
        // the layout a bender draws on uncut tubing.
        double cumulativeInches = 0;
        for (int i = 0; i < bendFittings.Count; i++)
        {
            var (fitting, leadingSegment) = bendFittings[i];
            if (leadingSegment != null)
            {
                cumulativeInches += leadingSegment.Length * 12.0;
            }

            var pattern = ClassifyBendPattern(fitting);
            var deduct = ResolveDeduct(fitting, run.Material);

            // Mark1 is the centerline of this bend along the stick.
            double mark1 = cumulativeInches;
            // Mark2 onward only apply to multi-bend patterns; the spool builder
            // upgrades them when it recognizes an offset / saddle group.
            double mark2 = 0, mark3 = 0, mark4 = 0;
            double dimA = 0, dimB = 0, dimC = 0, dimD = 0, dimE = 0, dimF = 0;

            switch (pattern)
            {
                case BendSchedulePattern.Stub90:
                    // DimA = stub leg; Mark1 = stub mark = stub - takeup; takeup ≈ deduct.
                    dimA = leadingSegment != null ? leadingSegment.Length * 12.0 : 0;
                    mark1 = Math.Max(0, mark1 - deduct);
                    break;

                case BendSchedulePattern.Kick90:
                    // DimA = kick depth (perpendicular offset); DimB = distance to obstacle.
                    dimA = fitting.AngleDegrees;
                    break;

                case BendSchedulePattern.Offset:
                    // DimA = offset depth (stored on the fitting angle field for offsets).
                    dimA = fitting.AngleDegrees;
                    break;
            }

            string bender = ResolveBenderType(fitting);
            string notes = BuildNotes(pattern, fitting);

            rows.Add(new BendScheduleRow(
                BendNumber: i + 1,
                FittingId: fitting.Id,
                Pattern: pattern,
                Angle1Degrees: ResolveAngle1(fitting, pattern),
                Angle2Degrees: ResolveAngle2(fitting, pattern),
                TradeSize: fitting.TradeSize,
                Material: run.Material,
                DeductInches: deduct,
                Mark1Inches: Math.Round(mark1, 3),
                Mark2Inches: Math.Round(mark2, 3),
                Mark3Inches: Math.Round(mark3, 3),
                Mark4Inches: Math.Round(mark4, 3),
                DimAInches: Math.Round(dimA, 3),
                DimBInches: Math.Round(dimB, 3),
                DimCInches: Math.Round(dimC, 3),
                DimDInches: Math.Round(dimD, 3),
                DimEInches: Math.Round(dimE, 3),
                DimFInches: Math.Round(dimF, 3),
                BenderType: bender,
                Notes: notes));
        }

        // Group rows into offset and saddle patterns where the geometry warrants.
        var groupedRows = GroupOffsetsAndSaddles(rows);
        var overall = ClassifyRunPattern(groupedRows);

        string audit = $"run={runId}; bends={groupedRows.Count}; pattern={overall}; material={run.Material}; tradeSize={run.TradeSize}";
        return new BendSchedule(runId, run.TradeSize, run.Material, overall, groupedRows, audit);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static List<(ConduitFitting Fitting, ConduitSegment? LeadingSegment)> OrderBendsAlongRun(
        ConduitRun run, List<ConduitSegment> segments, List<ConduitFitting> fittings)
    {
        // Run.FittingIds is the authoritative order — segments are interleaved.
        // For each fitting we associate the segment that *precedes* it on the run
        // so cumulative-mark accumulation walks the stick in order.
        var result = new List<(ConduitFitting, ConduitSegment?)>(fittings.Count);
        var fittingById = fittings.ToDictionary(f => f.Id, f => f);

        int segIdx = 0;
        foreach (var fid in run.FittingIds)
        {
            if (!fittingById.TryGetValue(fid, out var fit)) continue;
            ConduitSegment? leading = segIdx < segments.Count ? segments[segIdx] : null;
            result.Add((fit, leading));
            segIdx++;
        }

        // Fittings with no recorded order are appended in their natural list order
        foreach (var fit in fittings.Where(f => !run.FittingIds.Contains(f.Id)))
        {
            result.Add((fit, null));
        }

        return result;
    }

    private static BendSchedulePattern ClassifyBendPattern(ConduitFitting fitting)
    {
        return fitting.Type switch
        {
            FittingType.Elbow90 => BendSchedulePattern.Stub90,
            FittingType.Elbow45 => BendSchedulePattern.Offset,
            FittingType.Offset => BendSchedulePattern.Offset,
            _ => BendSchedulePattern.Custom,
        };
    }

    private static List<BendScheduleRow> GroupOffsetsAndSaddles(List<BendScheduleRow> rows)
    {
        // Consecutive offset-class bends with matching angles fold into a single
        // schedule row covering both bend centers (Mark1 + Mark2). Saddles fold
        // three or four consecutive offset bends into a single saddle row.
        var output = new List<BendScheduleRow>();
        int i = 0;
        while (i < rows.Count)
        {
            var current = rows[i];
            int matchCount = 1;
            while (i + matchCount < rows.Count
                && rows[i + matchCount].Pattern == BendSchedulePattern.Offset
                && current.Pattern == BendSchedulePattern.Offset
                && Math.Abs(rows[i + matchCount].Angle1Degrees - current.Angle1Degrees) < 1.0
                && matchCount < 4)
            {
                matchCount++;
            }

            if (matchCount >= 4)
            {
                output.Add(MakeMultiMarkRow(rows, i, 4, BendSchedulePattern.Saddle4Point));
                i += 4;
            }
            else if (matchCount == 3)
            {
                output.Add(MakeMultiMarkRow(rows, i, 3, BendSchedulePattern.Saddle3Point));
                i += 3;
            }
            else if (matchCount == 2)
            {
                output.Add(MakeMultiMarkRow(rows, i, 2, BendSchedulePattern.Offset));
                i += 2;
            }
            else
            {
                output.Add(current);
                i++;
            }
        }
        return output;
    }

    private static BendScheduleRow MakeMultiMarkRow(List<BendScheduleRow> rows, int start, int count, BendSchedulePattern pattern)
    {
        var first = rows[start];
        double m1 = first.Mark1Inches;
        double m2 = count >= 2 ? rows[start + 1].Mark1Inches : 0;
        double m3 = count >= 3 ? rows[start + 2].Mark1Inches : 0;
        double m4 = count >= 4 ? rows[start + 3].Mark1Inches : 0;

        // DimB on a grouped row is the spacing between Mark1 and Mark2 — the
        // dimension the bender uses to space bends on the stick.
        double dimB = count >= 2 ? Math.Round(m2 - m1, 3) : 0;
        double dimC = count >= 3 ? Math.Round(m3 - m2, 3) : 0;
        double dimD = count >= 4 ? Math.Round(m4 - m3, 3) : 0;

        return first with
        {
            Pattern = pattern,
            Mark1Inches = m1,
            Mark2Inches = m2,
            Mark3Inches = m3,
            Mark4Inches = m4,
            DimBInches = dimB,
            DimCInches = dimC,
            DimDInches = dimD,
            Notes = pattern switch
            {
                BendSchedulePattern.Offset => "offset bend pair",
                BendSchedulePattern.Saddle3Point => "3-point saddle",
                BendSchedulePattern.Saddle4Point => "4-point saddle",
                _ => first.Notes,
            }
        };
    }

    private static BendSchedulePattern ClassifyRunPattern(IReadOnlyList<BendScheduleRow> rows)
    {
        if (rows.Count == 0) return BendSchedulePattern.Straight;
        if (rows.Count == 1) return rows[0].Pattern;

        // If every row is the same kicker pattern, surface that as the overall.
        var distinct = rows.Select(r => r.Pattern).Distinct().ToList();
        if (distinct.Count == 1) return distinct[0];

        return BendSchedulePattern.Custom;
    }

    private static double ResolveDeduct(ConduitFitting fitting, ConduitMaterialType material)
    {
        if (fitting.DeductLength > 0) return fitting.DeductLength;
        return fitting.Type switch
        {
            FittingType.Elbow90 => ConduitTakeoffService.GetDeduct90(fitting.TradeSize, material),
            FittingType.Elbow45 => ConduitTakeoffService.GetDeduct45(fitting.TradeSize, material),
            FittingType.Offset => ConduitTakeoffService.ComputeOffsetDeduct(fitting.AngleDegrees, 45),
            _ => 0,
        };
    }

    private static double ResolveAngle1(ConduitFitting fitting, BendSchedulePattern pattern)
    {
        return pattern switch
        {
            BendSchedulePattern.Offset => 45.0,
            BendSchedulePattern.Stub90 or BendSchedulePattern.Kick90 => 90.0,
            BendSchedulePattern.Saddle3Point => 22.5,
            BendSchedulePattern.Saddle4Point => 22.5,
            _ => fitting.AngleDegrees,
        };
    }

    private static double ResolveAngle2(ConduitFitting fitting, BendSchedulePattern pattern)
    {
        return pattern switch
        {
            BendSchedulePattern.Saddle3Point => 45.0, // center bend
            _ => 0,
        };
    }

    private static string ResolveBenderType(ConduitFitting fitting)
    {
        return fitting.TradeSize switch
        {
            "1/2" or "3/4" => "Klein/Greenlee hand bender",
            "1" or "1-1/4" => DefaultBenderType,
            "1-1/2" or "2" => "Greenlee 555/881 hydraulic",
            _ => "Hydraulic 1801",
        };
    }

    private static string BuildNotes(BendSchedulePattern pattern, ConduitFitting fitting)
    {
        return pattern switch
        {
            BendSchedulePattern.Stub90 => $"{fitting.TradeSize}\" 90° stub",
            BendSchedulePattern.Kick90 => $"{fitting.TradeSize}\" 90° kick",
            BendSchedulePattern.Offset => $"{fitting.TradeSize}\" offset bend",
            _ => string.Empty,
        };
    }
}

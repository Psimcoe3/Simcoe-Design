using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Conduit.Core.Routing;

/// <summary>
/// Smart bend type, inspired by EVOLVE MEP families.
/// </summary>
public enum SmartBendType
{
    Stub90,   // 90-degree stub-up or stub-down
    Kick90,   // 90-degree kick (offset with 90-degree bends)
    Offset,   // Offset bend (two equal bends)
    Saddle    // 3-bend or 4-bend saddle
}

/// <summary>
/// A bend table entry mapping trade size + angle to deduct/gain values.
/// </summary>
public class BendTableEntry
{
    public string TradeSize { get; set; } = string.Empty;
    public double AngleDegrees { get; set; }
    public double BendRadius { get; set; }
    public double DeductInches { get; set; }
    public double TangentLengthInches { get; set; }
    public double GainInches { get; set; }
}

/// <summary>
/// Manages bend deduct tables and performs smart bend calculations.
/// </summary>
public class SmartBendService
{
    private readonly List<BendTableEntry> _bendTable = new();
    private const double AngleLookupTolerance = 0.5;

    private static readonly IReadOnlyDictionary<double, double> OffsetMultipliers =
        new Dictionary<double, double>
        {
            [10] = 6.0,
            [15] = 3.86,
            [22.5] = 2.6,
            [30] = 2.0,
            [45] = 1.4,
            [60] = 1.2
        };

    private static readonly IReadOnlyDictionary<double, double> OffsetShrinkPerInch =
        new Dictionary<double, double>
        {
            [10] = 1.0 / 16.0,
            [15] = 1.0 / 8.0,
            [22.5] = 3.0 / 16.0,
            [30] = 1.0 / 4.0,
            [45] = 3.0 / 8.0,
            [60] = 1.0 / 2.0
        };

    public IReadOnlyList<BendTableEntry> BendTable => _bendTable;

    public SmartBendService()
    {
        LoadDefaultBendTable();
    }

    /// <summary>
    /// Loads default EMT bend table data.
    /// </summary>
    private void LoadDefaultBendTable()
    {
        // Standard EMT bend data: trade size, angle, bend radius, deduct, tangent, gain
        AddEntry("1/2", 90, 4.0, 5.0, 4.0, 3.0);
        AddEntry("1/2", 45, 4.0, 2.5, 4.0, 1.5);
        AddEntry("1/2", 30, 4.0, 1.5, 4.0, 0.75);
        AddEntry("1/2", 22.5, 4.0, 1.0, 4.0, 0.5);
        AddEntry("1/2", 10, 4.0, 0.5, 4.0, 0.15);

        AddEntry("3/4", 90, 4.5, 6.0, 4.5, 3.375);
        AddEntry("3/4", 45, 4.5, 3.0, 4.5, 1.688);
        AddEntry("3/4", 30, 4.5, 1.75, 4.5, 0.844);

        AddEntry("1", 90, 5.75, 8.0, 5.75, 3.5);
        AddEntry("1", 45, 5.75, 3.625, 5.75, 1.75);
        AddEntry("1", 30, 5.75, 2.125, 5.75, 0.875);

        AddEntry("1-1/4", 90, 7.25, 10.0, 7.25, 4.5);
        AddEntry("1-1/4", 45, 7.25, 4.625, 7.25, 2.25);

        AddEntry("1-1/2", 90, 8.25, 11.0, 8.25, 5.75);
        AddEntry("1-1/2", 45, 8.25, 5.25, 8.25, 2.875);

        AddEntry("2", 90, 9.5, 13.0, 9.5, 6.0);
        AddEntry("2", 45, 9.5, 6.0, 9.5, 3.0);
    }

    private void AddEntry(string tradeSize, double angle, double radius,
        double deduct, double tangent, double gain)
    {
        _bendTable.Add(new BendTableEntry
        {
            TradeSize = tradeSize,
            AngleDegrees = angle,
            BendRadius = radius,
            DeductInches = deduct,
            TangentLengthInches = tangent,
            GainInches = gain
        });
    }

    /// <summary>
    /// Loads bend table entries from CSV lines (header + data).
    /// Format: TradeSize,AngleDegrees,BendRadius,DeductInches,TangentLengthInches,GainInches
    /// </summary>
    public void LoadFromCsv(IEnumerable<string> csvLines)
    {
        _bendTable.Clear();
        bool header = true;
        foreach (var line in csvLines)
        {
            if (header) { header = false; continue; }
            var parts = line.Split(',');
            if (parts.Length < 6) continue;

            _bendTable.Add(new BendTableEntry
            {
                TradeSize = parts[0].Trim(),
                AngleDegrees = double.Parse(parts[1].Trim()),
                BendRadius = double.Parse(parts[2].Trim()),
                DeductInches = double.Parse(parts[3].Trim()),
                TangentLengthInches = double.Parse(parts[4].Trim()),
                GainInches = double.Parse(parts[5].Trim())
            });
        }
    }

    /// <summary>
    /// Looks up deduct for a given trade size and bend angle.
    /// Interpolates if exact angle not found.
    /// </summary>
    public BendTableEntry? LookupDeduct(string tradeSize, double angleDegrees)
    {
        var entries = _bendTable
            .Where(e => e.TradeSize == tradeSize)
            .OrderBy(e => e.AngleDegrees)
            .ToList();

        if (entries.Count == 0) return null;

        // Exact match
        var exact = entries.FirstOrDefault(e => Math.Abs(e.AngleDegrees - angleDegrees) < 0.5);
        if (exact != null) return exact;

        // Interpolate between nearest entries
        var lower = entries.LastOrDefault(e => e.AngleDegrees <= angleDegrees);
        var upper = entries.FirstOrDefault(e => e.AngleDegrees >= angleDegrees);

        if (lower == null) return upper;
        if (upper == null) return lower;
        if (Math.Abs(upper.AngleDegrees - lower.AngleDegrees) < 0.01) return lower;

        double t = (angleDegrees - lower.AngleDegrees) / (upper.AngleDegrees - lower.AngleDegrees);
        return new BendTableEntry
        {
            TradeSize = tradeSize,
            AngleDegrees = angleDegrees,
            BendRadius = Lerp(lower.BendRadius, upper.BendRadius, t),
            DeductInches = Lerp(lower.DeductInches, upper.DeductInches, t),
            TangentLengthInches = Lerp(lower.TangentLengthInches, upper.TangentLengthInches, t),
            GainInches = Lerp(lower.GainInches, upper.GainInches, t)
        };
    }

    /// <summary>
    /// Classifies the smart bend type based on segment geometry.
    /// </summary>
    public SmartBendType ClassifyBend(XYZ dir1, XYZ dir2, XYZ up)
    {
        double angleDeg = XYZ.AngleBetween(dir1, dir2) * 180.0 / Math.PI;
        bool isVertical1 = Math.Abs(dir1.DotProduct(up)) > 0.7;
        bool isVertical2 = Math.Abs(dir2.DotProduct(up)) > 0.7;

        if (angleDeg > 80 && angleDeg < 100)
        {
            if (isVertical1 || isVertical2)
                return SmartBendType.Stub90;
            return SmartBendType.Kick90;
        }

        return SmartBendType.Offset;
    }

    /// <summary>
    /// Computes cut length for a segment considering bends at both ends.
    /// </summary>
    public double ComputeCutLength(double rawLengthInches, string tradeSize,
        double? startAngleDeg, double? endAngleDeg)
    {
        double cut = rawLengthInches;

        if (startAngleDeg.HasValue)
        {
            var entry = LookupDeduct(tradeSize, startAngleDeg.Value);
            if (entry != null) cut -= entry.DeductInches;
        }

        if (endAngleDeg.HasValue)
        {
            var entry = LookupDeduct(tradeSize, endAngleDeg.Value);
            if (entry != null) cut -= entry.DeductInches;
        }

        return Math.Max(0, cut);
    }

    /// <summary>
    /// Optimizes a sequence of colinear segments into a single stick,
    /// computing the total cut length minus deducts.
    /// </summary>
    public OptimizedStick MergeColinearSegments(
        List<ConduitSegment> segments, List<double> bendAngles)
    {
        double totalLength = segments.Sum(s => s.Length) * 12.0; // ft to inches
        double totalDeduct = 0;

        foreach (var angle in bendAngles)
        {
            var entry = LookupDeduct(segments[0].TradeSize, angle);
            if (entry != null)
                totalDeduct += entry.DeductInches;
        }

        return new OptimizedStick
        {
            RawLengthInches = totalLength,
            CutLengthInches = Math.Max(0, totalLength - totalDeduct),
            TotalDeductInches = totalDeduct,
            SegmentCount = segments.Count,
            BendCount = bendAngles.Count
        };
    }

    /// <summary>
    /// Stub-up mark location measured from conduit end.
    /// </summary>
    public double CalculateStubMark(double desiredStubHeightInches, double takeUpInches)
    {
        if (desiredStubHeightInches < 0)
            throw new ArgumentOutOfRangeException(nameof(desiredStubHeightInches));
        if (takeUpInches < 0)
            throw new ArgumentOutOfRangeException(nameof(takeUpInches));

        return desiredStubHeightInches - takeUpInches;
    }

    /// <summary>
    /// Returns the field multiplier for common offset bend angles.
    /// </summary>
    public bool TryGetOffsetMultiplier(double angleDegrees, out double multiplier) =>
        TryLookupAngleValue(OffsetMultipliers, angleDegrees, out multiplier);

    /// <summary>
    /// Returns shrink-per-inch factor for common offset bend angles.
    /// </summary>
    public bool TryGetOffsetShrinkPerInch(double angleDegrees, out double shrinkPerInch) =>
        TryLookupAngleValue(OffsetShrinkPerInch, angleDegrees, out shrinkPerInch);

    /// <summary>
    /// Distance between the two offset bends.
    /// </summary>
    public double CalculateOffsetSpacing(double offsetDepthInches, double angleDegrees)
    {
        if (offsetDepthInches < 0)
            throw new ArgumentOutOfRangeException(nameof(offsetDepthInches));

        if (TryGetOffsetMultiplier(angleDegrees, out var multiplier))
            return offsetDepthInches * multiplier;

        // Fallback to trig form: spacing = offset * csc(theta)
        var thetaRadians = angleDegrees * Math.PI / 180.0;
        if (thetaRadians <= 0 || thetaRadians >= Math.PI / 2)
            throw new ArgumentOutOfRangeException(nameof(angleDegrees), "Angle must be between 0 and 90 degrees.");

        return offsetDepthInches / Math.Sin(thetaRadians);
    }

    /// <summary>
    /// Offset shrink compensation along the run.
    /// </summary>
    public double CalculateOffsetShrink(double offsetDepthInches, double angleDegrees)
    {
        if (offsetDepthInches < 0)
            throw new ArgumentOutOfRangeException(nameof(offsetDepthInches));

        if (TryGetOffsetShrinkPerInch(angleDegrees, out var shrinkPerInch))
            return offsetDepthInches * shrinkPerInch;

        var sorted = OffsetShrinkPerInch.OrderBy(p => p.Key).ToList();
        if (sorted.Count == 0)
            return 0;

        if (angleDegrees <= sorted[0].Key)
            return offsetDepthInches * sorted[0].Value;

        if (angleDegrees >= sorted[^1].Key)
            return offsetDepthInches * sorted[^1].Value;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            var left = sorted[i];
            var right = sorted[i + 1];
            if (angleDegrees < left.Key || angleDegrees > right.Key)
                continue;

            var t = (angleDegrees - left.Key) / (right.Key - left.Key);
            var interpolated = Lerp(left.Value, right.Value, t);
            return offsetDepthInches * interpolated;
        }

        return offsetDepthInches * sorted[^1].Value;
    }

    /// <summary>
    /// Marks for an offset laid out toward an obstruction.
    /// </summary>
    public (double FirstMarkInches, double SecondMarkInches, double SpacingInches, double ShrinkInches)
        CalculateOffsetMarksTowardObstruction(double distanceToObstructionInches, double offsetDepthInches, double angleDegrees)
    {
        if (distanceToObstructionInches < 0)
            throw new ArgumentOutOfRangeException(nameof(distanceToObstructionInches));

        var spacing = CalculateOffsetSpacing(offsetDepthInches, angleDegrees);
        var shrink = CalculateOffsetShrink(offsetDepthInches, angleDegrees);
        var firstMark = distanceToObstructionInches + shrink;
        var secondMark = firstMark - spacing;
        return (firstMark, secondMark, spacing, shrink);
    }

    /// <summary>
    /// Center angle for a symmetric 3-point saddle.
    /// </summary>
    public static double CalculateThreePointSaddleCenterAngle(double outerAngleDegrees) =>
        outerAngleDegrees * 2.0;

    /// <summary>
    /// True when center angle equals twice outer angle within tolerance.
    /// </summary>
    public static bool IsSymmetricThreePointSaddle(double centerAngleDegrees, double outerAngleDegrees, double toleranceDegrees = 0.5) =>
        Math.Abs(centerAngleDegrees - (2.0 * outerAngleDegrees)) <= Math.Abs(toleranceDegrees);

    /// <summary>
    /// Rule-of-thumb 45-degree center saddle layout (22.5/45/22.5).
    /// </summary>
    public (double OuterMarkOffsetInches, double TotalOutsideSpacingInches)
        CalculateThreePointSaddleMarks45(double obstacleWidthInches)
    {
        if (obstacleWidthInches < 0)
            throw new ArgumentOutOfRangeException(nameof(obstacleWidthInches));

        var outerOffset = obstacleWidthInches * 2.0;
        return (outerOffset, outerOffset * 2.0);
    }

    private static bool TryLookupAngleValue(IReadOnlyDictionary<double, double> table, double angleDegrees, out double value)
    {
        value = 0;
        if (table.Count == 0)
            return false;

        var nearest = table.Keys
            .OrderBy(k => Math.Abs(k - angleDegrees))
            .First();

        if (Math.Abs(nearest - angleDegrees) > AngleLookupTolerance)
            return false;

        value = table[nearest];
        return true;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}

/// <summary>
/// Result of merging segments into a single manufacturable stick.
/// </summary>
public class OptimizedStick
{
    public double RawLengthInches { get; set; }
    public double CutLengthInches { get; set; }
    public double TotalDeductInches { get; set; }
    public int SegmentCount { get; set; }
    public int BendCount { get; set; }
}

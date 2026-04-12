using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Service for creating and managing cable tray runs with automatic
/// fitting insertion at direction changes (parallels <c>ConduitRunService</c>).
/// </summary>
public static class CableTrayRunService
{
    /// <summary>
    /// Central store of cable tray segments, fittings, and runs.
    /// </summary>
    public class CableTrayStore
    {
        public Dictionary<string, CableTraySegment> Segments { get; } = new();
        public Dictionary<string, CableTrayFitting> Fittings { get; } = new();
        public Dictionary<string, CableTrayRun> Runs { get; } = new();

        public void AddSegment(CableTraySegment segment) => Segments[segment.Id] = segment;
        public void AddFitting(CableTrayFitting fitting) => Fittings[fitting.Id] = fitting;
        public void AddRun(CableTrayRun run) => Runs[run.Id] = run;
    }

    /// <summary>
    /// Creates a cable tray run from ordered segments, automatically inserting
    /// fittings at direction changes.
    /// </summary>
    public static CableTrayRun CreateRunFromSegments(
        CableTrayStore store,
        IReadOnlyList<CableTraySegment> segments,
        double width = 12.0,
        double depth = 4.0,
        CableTrayType trayType = CableTrayType.Ladder)
    {
        var run = new CableTrayRun
        {
            Width = width,
            Depth = depth,
            TrayType = trayType
        };

        foreach (var seg in segments)
        {
            store.AddSegment(seg);
            run.SegmentIds.Add(seg.Id);
        }

        // Insert fittings at direction changes between consecutive segments
        for (int i = 0; i < segments.Count - 1; i++)
        {
            var fitting = CreateFittingBetween(segments[i], segments[i + 1], width, depth);
            if (fitting != null)
            {
                store.AddFitting(fitting);
                run.FittingIds.Add(fitting.Id);
            }
        }

        store.AddRun(run);
        return run;
    }

    /// <summary>
    /// Computes total run length in feet by summing all segment lengths.
    /// </summary>
    public static double GetTotalLength(CableTrayStore store, CableTrayRun run)
    {
        double total = 0;
        foreach (var id in run.SegmentIds)
        {
            if (store.Segments.TryGetValue(id, out var seg))
                total += seg.Length;
        }
        return total;
    }

    /// <summary>
    /// Returns the segments of a run in order.
    /// </summary>
    public static IReadOnlyList<CableTraySegment> GetSegments(CableTrayStore store, CableTrayRun run) =>
        run.SegmentIds
            .Where(id => store.Segments.ContainsKey(id))
            .Select(id => store.Segments[id])
            .ToList();

    /// <summary>
    /// Returns the fittings of a run in order.
    /// </summary>
    public static IReadOnlyList<CableTrayFitting> GetFittings(CableTrayStore store, CableTrayRun run) =>
        run.FittingIds
            .Where(id => store.Fittings.ContainsKey(id))
            .Select(id => store.Fittings[id])
            .ToList();

    // ── private helpers ─────────────────────────────────────────

    private static CableTrayFitting? CreateFittingBetween(
        CableTraySegment seg1,
        CableTraySegment seg2,
        double width,
        double depth)
    {
        var dir1 = seg1.Direction;
        var dir2 = seg2.Direction;

        double angleRad = XYZ.AngleBetween(dir1, dir2);
        double angleDeg = angleRad * 180.0 / Math.PI;

        // Skip near-collinear segments (< 5°) — splice, not fitting
        if (angleDeg < 5 || angleDeg > 175)
            return null;

        var fittingType = angleDeg > 60
            ? CableTrayFittingType.Elbow90
            : CableTrayFittingType.Elbow45;

        return new CableTrayFitting
        {
            Type = fittingType,
            Location = seg1.EndPoint,
            AngleDegrees = angleDeg,
            Width = width,
            Depth = depth,
            ConnectedSegmentIds = { seg1.Id, seg2.Id }
        };
    }
}

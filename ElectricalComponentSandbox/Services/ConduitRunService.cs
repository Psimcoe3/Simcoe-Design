using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// High-level service for conduit run operations layered above <see cref="ConduitModelStore"/>.
/// Provides CreateRun, AppendSegment, SplitAt, MergeRuns, and GetTotalLength.
/// </summary>
public static class ConduitRunService
{
    /// <summary>
    /// Creates a new conduit run from a list of segments, registering them in the store.
    /// Fittings are auto-inserted between consecutive segments when the store settings allow.
    /// </summary>
    public static ConduitRun CreateRun(
        ConduitModelStore store,
        List<ConduitSegment> segments,
        string? conduitTypeId = null,
        string? tradeSize = null)
    {
        if (segments.Count == 0)
            throw new ArgumentException("At least one segment is required.", nameof(segments));

        // Apply overrides before delegating to store
        if (conduitTypeId != null || tradeSize != null)
        {
            var defaults = store.ResolveRoutingDefaults(conduitTypeId, tradeSize);
            foreach (var seg in segments)
            {
                if (conduitTypeId != null) seg.ConduitTypeId = defaults.ConduitTypeId;
                if (tradeSize != null) seg.TradeSize = defaults.TradeSize;
            }
        }

        return store.CreateRunFromSegments(segments);
    }

    /// <summary>
    /// Appends a segment to an existing run, optionally inserting a fitting at the junction.
    /// </summary>
    public static void AppendSegment(
        ConduitModelStore store,
        string runId,
        ConduitSegment segment)
    {
        var run = store.GetRun(runId)
            ?? throw new ArgumentException($"Run '{runId}' not found.", nameof(runId));

        // Resolve routing defaults from the run
        segment.ConduitTypeId = run.ConduitTypeId;
        segment.TradeSize = run.TradeSize;
        segment.Material = run.Material;
        segment.LevelId = run.LevelId;

        store.AddSegment(segment);

        // Insert a fitting between the last existing segment and the new one
        if (run.SegmentIds.Count > 0)
        {
            var lastSeg = store.GetSegment(run.SegmentIds[^1]);
            if (lastSeg != null)
            {
                var fitting = CreateFittingIfNeeded(store, run, lastSeg, segment);
                if (fitting != null)
                {
                    store.AddFitting(fitting);
                    run.FittingIds.Add(fitting.Id);
                }
            }
        }

        run.SegmentIds.Add(segment.Id);
    }

    /// <summary>
    /// Splits a run at the specified segment index, producing two runs.
    /// The original run keeps segments [0..splitIndex) and the new run gets [splitIndex..end).
    /// </summary>
    /// <returns>The newly created second run.</returns>
    public static ConduitRun SplitAt(
        ConduitModelStore store,
        string runId,
        int splitIndex)
    {
        var run = store.GetRun(runId)
            ?? throw new ArgumentException($"Run '{runId}' not found.", nameof(runId));

        if (splitIndex <= 0 || splitIndex >= run.SegmentIds.Count)
            throw new ArgumentOutOfRangeException(nameof(splitIndex),
                $"Split index must be between 1 and {run.SegmentIds.Count - 1}.");

        var newRun = new ConduitRun
        {
            RunId = store.GenerateRunId(),
            ConduitTypeId = run.ConduitTypeId,
            TradeSize = run.TradeSize,
            Material = run.Material,
            LevelId = run.LevelId,
            EndEquipment = run.EndEquipment
        };

        // Move segments from splitIndex onward to the new run
        newRun.SegmentIds = run.SegmentIds.GetRange(splitIndex, run.SegmentIds.Count - splitIndex);
        run.SegmentIds = run.SegmentIds.GetRange(0, splitIndex);

        // Re-partition fittings: fitting at index i connects segments i and i+1
        // Fittings at indices [0..splitIndex-2] stay, [splitIndex-1] is the split point (removed),
        // [splitIndex..end] move to new run
        var oldFittings = new List<string>(run.FittingIds);
        run.FittingIds = new List<string>();
        newRun.FittingIds = new List<string>();

        for (int i = 0; i < oldFittings.Count; i++)
        {
            if (i < splitIndex - 1)
                run.FittingIds.Add(oldFittings[i]);
            else if (i == splitIndex - 1)
                store.RemoveFitting(oldFittings[i]); // fitting at the split point
            else
                newRun.FittingIds.Add(oldFittings[i]);
        }

        run.EndEquipment = string.Empty;
        store.AddRun(newRun);
        return newRun;
    }

    /// <summary>
    /// Merges a second run onto the end of the first run. The second run is removed from the store.
    /// A fitting is inserted at the junction if appropriate.
    /// </summary>
    public static void MergeRuns(
        ConduitModelStore store,
        string primaryRunId,
        string secondaryRunId)
    {
        var primary = store.GetRun(primaryRunId)
            ?? throw new ArgumentException($"Run '{primaryRunId}' not found.", nameof(primaryRunId));
        var secondary = store.GetRun(secondaryRunId)
            ?? throw new ArgumentException($"Run '{secondaryRunId}' not found.", nameof(secondaryRunId));

        // Insert fitting between last segment of primary and first segment of secondary
        if (primary.SegmentIds.Count > 0 && secondary.SegmentIds.Count > 0)
        {
            var lastSeg = store.GetSegment(primary.SegmentIds[^1]);
            var firstSeg = store.GetSegment(secondary.SegmentIds[0]);
            if (lastSeg != null && firstSeg != null)
            {
                var fitting = CreateFittingIfNeeded(store, primary, lastSeg, firstSeg);
                if (fitting != null)
                {
                    store.AddFitting(fitting);
                    primary.FittingIds.Add(fitting.Id);
                }
            }
        }

        primary.SegmentIds.AddRange(secondary.SegmentIds);
        primary.FittingIds.AddRange(secondary.FittingIds);
        primary.EndEquipment = secondary.EndEquipment;

        store.RemoveRun(secondary.Id);
    }

    /// <summary>
    /// Computes total length of all segments in a run (convenience wrapper).
    /// </summary>
    public static double GetTotalLength(ConduitModelStore store, string runId)
    {
        var run = store.GetRun(runId)
            ?? throw new ArgumentException($"Run '{runId}' not found.", nameof(runId));

        return run.ComputeTotalLength(store);
    }

    /// <summary>
    /// Creates a <see cref="ConduitTagInfo"/> for a conduit run, capturing its label text.
    /// </summary>
    public static ConduitTagInfo CreateTag(ConduitModelStore store, string runId)
    {
        var run = store.GetRun(runId)
            ?? throw new ArgumentException($"Run '{runId}' not found.", nameof(runId));

        var conduitType = store.GetType(run.ConduitTypeId);
        string typeName = conduitType?.Name ?? run.Material.ToString();
        string label = $"{run.TradeSize}\" {typeName}";

        return new ConduitTagInfo
        {
            RunId = run.Id,
            Label = label,
            TradeSize = run.TradeSize,
            TypeName = typeName
        };
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private static ConduitFitting? CreateFittingIfNeeded(
        ConduitModelStore store,
        ConduitRun run,
        ConduitSegment seg1,
        ConduitSegment seg2)
    {
        var conduitType = store.GetType(run.ConduitTypeId);
        bool insertFittings = store.Settings.AutoInsertFittings && (conduitType?.IsWithFitting ?? true);
        if (!insertFittings) return null;

        var dir1 = seg1.Direction;
        var dir2 = seg2.Direction;
        double angleRad = XYZ.AngleBetween(dir1, dir2);
        double angleDeg = angleRad * 180.0 / Math.PI;

        var fittingType = conduitType?.SelectFitting(angleDeg);
        if (fittingType == null && angleDeg > 5 && angleDeg < 170)
            fittingType = angleDeg > 60 ? FittingType.Elbow90 : FittingType.Elbow45;

        if (fittingType == null) return null;

        return new ConduitFitting
        {
            Type = fittingType.Value,
            Location = seg1.EndPoint,
            AngleDegrees = angleDeg,
            TradeSize = run.TradeSize,
            ConnectedSegmentIds = new List<string> { seg1.Id, seg2.Id }
        };
    }
}

/// <summary>
/// Info for a conduit tag annotation. Used to create a <c>MarkupType.ConduitTag</c> markup.
/// </summary>
public class ConduitTagInfo
{
    /// <summary>ID of the tagged conduit run.</summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>Display label (e.g. "3/4\" EMT Conduit").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Trade size designation.</summary>
    public string TradeSize { get; set; } = string.Empty;

    /// <summary>Conduit type name.</summary>
    public string TypeName { get; set; } = string.Empty;
}

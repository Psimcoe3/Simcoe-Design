namespace ElectricalComponentSandbox.Conduit.Core.Model;

/// <summary>
/// Type-driven routing defaults used when creating conduit runs.
/// </summary>
public class RoutingDefaults
{
    public string ConduitTypeId { get; set; } = string.Empty;
    public string TradeSize { get; set; } = "1/2";
    public ConduitMaterialType Material { get; set; } = ConduitMaterialType.EMT;
}

/// <summary>
/// Central store for all conduit model objects - the "database" layer.
/// </summary>
public class ConduitModelStore
{
    private readonly Dictionary<string, ConduitSegment> _segments = new();
    private readonly Dictionary<string, ConduitFitting> _fittings = new();
    private readonly Dictionary<string, ConduitRun> _runs = new();
    private readonly Dictionary<string, ConduitType> _types = new();

    public ConduitSettings Settings { get; set; } = new();
    public ConnectivityGraph Connectivity { get; } = new();

    private int _nextRunNumber = 1;

    // Types

    public void AddType(ConduitType type) => _types[type.Id] = type;
    public ConduitType? GetType(string id) => _types.GetValueOrDefault(id);
    public IEnumerable<ConduitType> GetAllTypes() => _types.Values;

    // Segments

    public void AddSegment(ConduitSegment segment)
    {
        segment.InitializeConnectors();
        _segments[segment.Id] = segment;
        foreach (var c in segment.Connectors.Connectors)
            Connectivity.Register(c);
    }

    public ConduitSegment? GetSegment(string id) => _segments.GetValueOrDefault(id);
    public IEnumerable<ConduitSegment> GetAllSegments() => _segments.Values;

    public void RemoveSegment(string id)
    {
        if (_segments.TryGetValue(id, out var seg))
        {
            foreach (var c in seg.Connectors.Connectors)
            {
                Connectivity.Disconnect(c.Id);
                Connectivity.Unregister(c.Id);
            }
            _segments.Remove(id);
        }
    }

    // Fittings

    public void AddFitting(ConduitFitting fitting)
    {
        _fittings[fitting.Id] = fitting;
        foreach (var c in fitting.Connectors.Connectors)
            Connectivity.Register(c);
    }

    public ConduitFitting? GetFitting(string id) => _fittings.GetValueOrDefault(id);
    public IEnumerable<ConduitFitting> GetAllFittings() => _fittings.Values;

    public void RemoveFitting(string id)
    {
        if (_fittings.TryGetValue(id, out var fit))
        {
            foreach (var c in fit.Connectors.Connectors)
            {
                Connectivity.Disconnect(c.Id);
                Connectivity.Unregister(c.Id);
            }
            _fittings.Remove(id);
        }
    }

    // Runs

    public void AddRun(ConduitRun run) => _runs[run.Id] = run;
    public ConduitRun? GetRun(string id) => _runs.GetValueOrDefault(id);
    public IEnumerable<ConduitRun> GetAllRuns() => _runs.Values;

    public void RemoveRun(string id) => _runs.Remove(id);

    /// <summary>
    /// Resolves a valid conduit type, trade size, and material combination
    /// using current model settings and available conduit types.
    /// </summary>
    public RoutingDefaults ResolveRoutingDefaults(
        string? conduitTypeId = null,
        string? tradeSize = null,
        ConduitMaterialType? material = null)
    {
        var selectedType = !string.IsNullOrWhiteSpace(conduitTypeId)
            ? GetType(conduitTypeId!)
            : null;

        selectedType ??= GetType(Settings.DefaultConduitTypeId) ?? GetAllTypes().FirstOrDefault() ?? new ConduitType();

        var resolvedTradeSize = string.IsNullOrWhiteSpace(tradeSize)
            ? Settings.DefaultTradeSize
            : tradeSize!;

        if (!selectedType.SizeSettings.IsValidTradeSize(resolvedTradeSize))
        {
            resolvedTradeSize = selectedType.SizeSettings.StandardTradeSizes.FirstOrDefault() ?? Settings.DefaultTradeSize;
        }

        return new RoutingDefaults
        {
            ConduitTypeId = selectedType.Id,
            TradeSize = resolvedTradeSize,
            Material = material ?? selectedType.Standard
        };
    }

    /// <summary>
    /// Generates the next sequential run ID.
    /// </summary>
    public string GenerateRunId()
    {
        return $"CR-{_nextRunNumber++:D3}";
    }

    /// <summary>
    /// Creates a run from a list of segments, inserting fittings at junctions.
    /// </summary>
    public ConduitRun CreateRunFromSegments(List<ConduitSegment> segments, string? runId = null)
    {
        if (segments.Count == 0)
            throw new ArgumentException("At least one segment is required to create a conduit run.", nameof(segments));

        var defaults = ResolveRoutingDefaults(
            segments[0].ConduitTypeId,
            segments[0].TradeSize,
            segments[0].Material);

        foreach (var seg in segments)
        {
            seg.ConduitTypeId = defaults.ConduitTypeId;
            seg.TradeSize = defaults.TradeSize;
            seg.Material = defaults.Material;
        }

        var run = new ConduitRun
        {
            RunId = runId ?? GenerateRunId(),
            ConduitTypeId = defaults.ConduitTypeId,
            TradeSize = defaults.TradeSize,
            Material = defaults.Material,
            LevelId = segments[0].LevelId
        };

        foreach (var seg in segments)
        {
            AddSegment(seg);
            run.SegmentIds.Add(seg.Id);
        }

        // Insert fittings between consecutive segments
        var conduitType = GetType(run.ConduitTypeId);
        bool insertFittings = Settings.AutoInsertFittings && (conduitType?.IsWithFitting ?? true);
        if (insertFittings)
        {
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var fitting = CreateFittingBetween(segments[i], segments[i + 1]);
                if (fitting != null)
                {
                    AddFitting(fitting);
                    run.FittingIds.Add(fitting.Id);
                }
            }
        }

        // Auto-connect
        Connectivity.AutoConnect(Settings.ConnectionTolerance);

        AddRun(run);
        return run;
    }

    /// <summary>
    /// Creates a fitting between two consecutive segments based on angle.
    /// </summary>
    private ConduitFitting? CreateFittingBetween(ConduitSegment seg1, ConduitSegment seg2)
    {
        var dir1 = seg1.Direction;
        var dir2 = seg2.Direction;
        double angleRad = XYZ.AngleBetween(dir1, dir2);
        double angleDeg = angleRad * 180.0 / Math.PI;

        // Find a matching type
        var conduitType = GetType(seg1.ConduitTypeId);
        var fittingType = conduitType?.SelectFitting(angleDeg);

        if (fittingType == null && angleDeg > 5 && angleDeg < 170)
        {
            // Default to elbow
            fittingType = angleDeg > 60 ? FittingType.Elbow90 : FittingType.Elbow45;
        }

        if (fittingType == null) return null;

        return new ConduitFitting
        {
            Type = fittingType.Value,
            Location = seg1.EndPoint,
            AngleDegrees = angleDeg,
            TradeSize = seg1.TradeSize,
            ConnectedSegmentIds = new List<string> { seg1.Id, seg2.Id }
        };
    }
}

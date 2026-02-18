using ElectricalComponentSandbox.Conduit.Core.Geometry;
using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Conduit.Core.Routing;

/// <summary>
/// Routing preferences for auto-route operations.
/// </summary>
public class RoutingOptions
{
    /// <summary>Conduit type ID to use.</summary>
    public string ConduitTypeId { get; set; } = string.Empty;

    /// <summary>Trade size to use.</summary>
    public string TradeSize { get; set; } = "1/2";

    /// <summary>Material type.</summary>
    public ConduitMaterialType Material { get; set; } = ConduitMaterialType.EMT;

    /// <summary>Level ID.</summary>
    public string LevelId { get; set; } = "Level 1";

    /// <summary>Elevation in feet.</summary>
    public double Elevation { get; set; } = 10.0;

    /// <summary>Whether to use A* routing around obstacles.</summary>
    public bool UsePathfinding { get; set; } = false;

    /// <summary>Obstacles to route around.</summary>
    public List<ObstacleBox> Obstacles { get; set; } = new();

    /// <summary>Bounding box for pathfinding grid.</summary>
    public ObstacleBox? RoutingBounds { get; set; }

    /// <summary>Voxel size for A* grid (in feet).</summary>
    public double VoxelSize { get; set; } = 0.5;

    /// <summary>Insert rises/drops for vertical segments.</summary>
    public bool AutoRiseDrop { get; set; } = true;
}

/// <summary>
/// Auto-routing service: takes a polyline pathway and produces a ConduitRun
/// with segments, fittings, and optional rise/drop elements.
/// </summary>
public class AutoRouteService
{
    private readonly ConduitModelStore _store;
    private readonly SmartBendService _bends;

    public AutoRouteService(ConduitModelStore store, SmartBendService? bends = null)
    {
        _store = store;
        _bends = bends ?? new SmartBendService();
    }

    /// <summary>
    /// Routes conduit along a user-specified polyline pathway.
    /// Inserts segments and fittings, returning a complete ConduitRun.
    /// </summary>
    public ConduitRun AutoRoute(IReadOnlyList<XYZ> pathway, RoutingOptions options)
    {
        List<XYZ> routePath;

        if (options.UsePathfinding && options.RoutingBounds != null && options.Obstacles.Count > 0)
        {
            // Use A* to route around obstacles between each pair of waypoints
            routePath = RouteAroundObstacles(pathway, options);
        }
        else
        {
            routePath = new List<XYZ>(pathway);
        }

        // Create segments from path
        var segments = PathSimplifier.CreateSegmentsFromPath(
            routePath,
            options.ConduitTypeId,
            options.TradeSize,
            options.Material,
            options.LevelId);

        // Create run with auto-fitting insertion
        var run = _store.CreateRunFromSegments(segments);
        run.ConduitTypeId = options.ConduitTypeId;
        run.TradeSize = options.TradeSize;
        run.Material = options.Material;
        run.LevelId = options.LevelId;

        return run;
    }

    /// <summary>
    /// Routes from start to end using A* pathfinding around obstacles.
    /// </summary>
    private List<XYZ> RouteAroundObstacles(IReadOnlyList<XYZ> pathway, RoutingOptions options)
    {
        var router = new AStarRouter(options.RoutingBounds!, options.VoxelSize, options.Obstacles);
        var fullPath = new List<XYZ> { pathway[0] };

        for (int i = 0; i < pathway.Count - 1; i++)
        {
            var segPath = router.FindPath(pathway[i], pathway[i + 1]);
            if (segPath != null && segPath.Count > 1)
            {
                fullPath.AddRange(segPath.Skip(1));
            }
            else
            {
                // Fallback: direct connection
                fullPath.Add(pathway[i + 1]);
            }
        }

        return fullPath;
    }

    /// <summary>
    /// Detects vertical segments in a run and tags them as rises or drops
    /// for 2D plan view symbol rendering.
    /// </summary>
    public List<RiseDropInfo> DetectRiseDrops(ConduitRun run)
    {
        var result = new List<RiseDropInfo>();

        foreach (var seg in run.GetSegments(_store))
        {
            var dir = seg.Direction;
            double verticalComponent = Math.Abs(dir.DotProduct(XYZ.BasisZ));

            if (verticalComponent > 0.7) // mostly vertical
            {
                bool isRise = (seg.EndPoint.Z - seg.StartPoint.Z) > 0;
                result.Add(new RiseDropInfo
                {
                    SegmentId = seg.Id,
                    Location = seg.LocationCurve.Evaluate(0.5),
                    IsRise = isRise,
                    VerticalDistance = Math.Abs(seg.EndPoint.Z - seg.StartPoint.Z)
                });
            }
        }

        return result;
    }
}

/// <summary>
/// Information about a vertical rise or drop in a conduit run.
/// </summary>
public class RiseDropInfo
{
    public string SegmentId { get; set; } = string.Empty;
    public XYZ Location { get; set; }
    public bool IsRise { get; set; }
    public double VerticalDistance { get; set; }
}

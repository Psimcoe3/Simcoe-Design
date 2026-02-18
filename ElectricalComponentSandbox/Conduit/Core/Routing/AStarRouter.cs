using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Conduit.Core.Routing;

/// <summary>
/// Represents an axis-aligned obstacle bounding box for pathfinding.
/// </summary>
public class ObstacleBox
{
    public XYZ Min { get; set; }
    public XYZ Max { get; set; }

    public ObstacleBox(XYZ min, XYZ max)
    {
        Min = min;
        Max = max;
    }

    public bool Contains(XYZ point)
    {
        return point.X >= Min.X && point.X <= Max.X &&
               point.Y >= Min.Y && point.Y <= Max.Y &&
               point.Z >= Min.Z && point.Z <= Max.Z;
    }

    /// <summary>
    /// Expands the box by a margin on all sides.
    /// </summary>
    public ObstacleBox Expand(double margin) =>
        new(new XYZ(Min.X - margin, Min.Y - margin, Min.Z - margin),
            new XYZ(Max.X + margin, Max.Y + margin, Max.Z + margin));
}

/// <summary>
/// A* pathfinding on a 3D voxel grid for auto-routing conduit around obstacles.
/// </summary>
public class AStarRouter
{
    private readonly double _voxelSize;
    private readonly List<ObstacleBox> _obstacles;
    private readonly ObstacleBox _bounds;

    public AStarRouter(ObstacleBox bounds, double voxelSize, List<ObstacleBox>? obstacles = null)
    {
        _bounds = bounds;
        _voxelSize = voxelSize;
        _obstacles = obstacles ?? new List<ObstacleBox>();
    }

    public void AddObstacle(ObstacleBox obstacle) => _obstacles.Add(obstacle);

    /// <summary>
    /// Finds a path from start to end avoiding obstacles.
    /// Returns a list of waypoints in world coordinates.
    /// </summary>
    public List<XYZ>? FindPath(XYZ start, XYZ end)
    {
        var startNode = WorldToGrid(start);
        var endNode = WorldToGrid(end);

        if (startNode == endNode)
            return new List<XYZ> { start, end };

        var openSet = new PriorityQueue<GridNode, double>();
        var cameFrom = new Dictionary<GridNode, GridNode>();
        var gScore = new Dictionary<GridNode, double>();
        var visited = new HashSet<GridNode>();

        gScore[startNode] = 0;
        openSet.Enqueue(startNode, Heuristic(startNode, endNode));

        // 6-connected neighbors (axis-aligned)
        var neighbors = new (int dx, int dy, int dz)[]
        {
            (1, 0, 0), (-1, 0, 0),
            (0, 1, 0), (0, -1, 0),
            (0, 0, 1), (0, 0, -1)
        };

        int maxIterations = 100_000;
        int iterations = 0;

        while (openSet.Count > 0 && iterations++ < maxIterations)
        {
            var current = openSet.Dequeue();

            if (current == endNode)
            {
                return ReconstructPath(cameFrom, current, start, end);
            }

            if (!visited.Add(current)) continue;

            foreach (var (dx, dy, dz) in neighbors)
            {
                var neighbor = new GridNode(current.X + dx, current.Y + dy, current.Z + dz);

                if (visited.Contains(neighbor)) continue;
                if (!IsInBounds(neighbor)) continue;
                if (IsBlocked(neighbor)) continue;

                double tentativeG = gScore[current] + _voxelSize;

                if (!gScore.TryGetValue(neighbor, out double existingG) || tentativeG < existingG)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    double fScore = tentativeG + Heuristic(neighbor, endNode);
                    openSet.Enqueue(neighbor, fScore);
                }
            }
        }

        return null; // No path found
    }

    private List<XYZ> ReconstructPath(Dictionary<GridNode, GridNode> cameFrom,
        GridNode current, XYZ worldStart, XYZ worldEnd)
    {
        var gridPath = new List<GridNode> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            gridPath.Add(current);
        }
        gridPath.Reverse();

        // Convert grid path to world coordinates
        var worldPath = new List<XYZ> { worldStart };
        for (int i = 1; i < gridPath.Count - 1; i++)
        {
            worldPath.Add(GridToWorld(gridPath[i]));
        }
        worldPath.Add(worldEnd);

        // Simplify: remove colinear points
        return SimplifyPath(worldPath);
    }

    private static List<XYZ> SimplifyPath(List<XYZ> path)
    {
        if (path.Count <= 2) return path;

        var simplified = new List<XYZ> { path[0] };
        for (int i = 1; i < path.Count - 1; i++)
        {
            var dir1 = (path[i] - path[i - 1]).Normalize();
            var dir2 = (path[i + 1] - path[i]).Normalize();
            if (!dir1.IsAlmostEqualTo(dir2, 0.01))
            {
                simplified.Add(path[i]);
            }
        }
        simplified.Add(path[^1]);
        return simplified;
    }

    private double Heuristic(GridNode a, GridNode b)
    {
        // Manhattan distance in voxel space
        return (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z)) * _voxelSize;
    }

    private bool IsInBounds(GridNode node)
    {
        var world = GridToWorld(node);
        return world.X >= _bounds.Min.X && world.X <= _bounds.Max.X &&
               world.Y >= _bounds.Min.Y && world.Y <= _bounds.Max.Y &&
               world.Z >= _bounds.Min.Z && world.Z <= _bounds.Max.Z;
    }

    private bool IsBlocked(GridNode node)
    {
        var world = GridToWorld(node);
        return _obstacles.Any(obs => obs.Contains(world));
    }

    private GridNode WorldToGrid(XYZ world) =>
        new((int)Math.Round((world.X - _bounds.Min.X) / _voxelSize),
            (int)Math.Round((world.Y - _bounds.Min.Y) / _voxelSize),
            (int)Math.Round((world.Z - _bounds.Min.Z) / _voxelSize));

    private XYZ GridToWorld(GridNode node) =>
        new(_bounds.Min.X + node.X * _voxelSize,
            _bounds.Min.Y + node.Y * _voxelSize,
            _bounds.Min.Z + node.Z * _voxelSize);

    private readonly record struct GridNode(int X, int Y, int Z);
}

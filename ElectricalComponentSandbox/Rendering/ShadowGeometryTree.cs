using System.Windows;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Rendering;

/// <summary>
/// Shadow geometry tree for hit testing on the SkiaSharp canvas.
/// Maintains lightweight bounding boxes and path segments so that
/// mouse hit testing works without a WPF visual tree.
/// 
/// Performance note: for projects with >500 components a spatial index
/// (R-tree or uniform grid) should replace the linear scan. The interface
/// is designed for that future upgrade — callers use <see cref="HitTest"/>
/// and <see cref="QueryRect"/> without knowing the internal structure.
/// </summary>
public sealed class ShadowGeometryTree
{
    // ── Node types ─────────────────────────────────────────────────────────────

    public enum ShadowNodeKind { Component, Markup, GripPoint, SnapPoint }

    public sealed class ShadowNode
    {
        public string Id { get; init; } = string.Empty;
        public ShadowNodeKind Kind { get; init; }
        public Rect BoundingRect { get; init; }
        public IReadOnlyList<Point>? PathPoints { get; init; }
        public object? Source { get; init; }   // original Component / MarkupRecord
    }

    // ── Storage ────────────────────────────────────────────────────────────────

    private readonly List<ShadowNode> _nodes = new();
    private readonly Dictionary<string, ShadowNode> _index = new();

    // ── Population ────────────────────────────────────────────────────────────

    public void Clear()
    {
        _nodes.Clear();
        _index.Clear();
    }

    /// <summary>
    /// Registers an <see cref="ElectricalComponent"/> into the shadow tree.
    /// Call after adding or moving a component.
    /// </summary>
    public void AddOrUpdate(ElectricalComponent component, IReadOnlyList<Point>? screenPoints = null)
    {
        // Build a bounding rect from the component parameters (2D plan projection)
        double hw = component.Parameters.Width / 2;
        double hd = component.Parameters.Depth / 2;
        double cx = component.Position.X;
        double cy = component.Position.Z;   // Z = depth in 2D plan

        var rect = new Rect(cx - hw, cy - hd, component.Parameters.Width, component.Parameters.Depth);
        var node = new ShadowNode
        {
            Id = component.Id,
            Kind = ShadowNodeKind.Component,
            BoundingRect = rect,
            PathPoints = screenPoints,
            Source = component
        };
        Register(node);
    }

    /// <summary>
    /// Registers a <see cref="MarkupRecord"/> into the shadow tree.
    /// </summary>
    public void AddOrUpdate(MarkupRecord markup)
    {
        var node = new ShadowNode
        {
            Id = markup.Id,
            Kind = ShadowNodeKind.Markup,
            BoundingRect = markup.BoundingRect,
            PathPoints = markup.Vertices,
            Source = markup
        };
        Register(node);
    }

    public void AddOrUpdateNode(string id, ShadowNodeKind kind, Rect boundingRect, IReadOnlyList<Point>? pathPoints = null, object? source = null)
    {
        var node = new ShadowNode
        {
            Id = id,
            Kind = kind,
            BoundingRect = boundingRect,
            PathPoints = pathPoints,
            Source = source
        };

        Register(node);
    }

    public void Remove(string id)
    {
        if (_index.TryGetValue(id, out var node))
        {
            _nodes.Remove(node);
            _index.Remove(id);
        }
    }

    // ── Query ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the topmost node whose geometry contains <paramref name="docPoint"/>
    /// within <paramref name="toleranceDu"/> document units.
    /// </summary>
    public ShadowNode? HitTest(Point docPoint, double toleranceDu = 0.2)
    {
        var inflated = new Size(toleranceDu * 2, toleranceDu * 2);

        // Iterate in reverse to respect draw order (last drawn = topmost)
        for (int i = _nodes.Count - 1; i >= 0; i--)
        {
            var node = _nodes[i];
            var test = node.BoundingRect;
            test.Inflate(toleranceDu, toleranceDu);

            if (!test.Contains(docPoint)) continue;

            // For polylines / conduit runs use segment proximity
            if (node.PathPoints is { Count: >= 2 })
            {
                if (IsNearPath(docPoint, node.PathPoints, toleranceDu))
                    return node;
            }
            else
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns all nodes whose bounding rect intersects with <paramref name="docRect"/>.
    /// For crossing selection pass <paramref name="windowOnly"/> = false (touch test).
    /// For window selection pass <paramref name="windowOnly"/> = true (fully inside).
    /// </summary>
    public IReadOnlyList<ShadowNode> QueryRect(Rect docRect, bool windowOnly = false)
    {
        var result = new List<ShadowNode>();
        foreach (var node in _nodes)
        {
            bool match = windowOnly
                ? docRect.Contains(node.BoundingRect)
                : docRect.IntersectsWith(node.BoundingRect);
            if (match) result.Add(node);
        }
        return result;
    }

    /// <summary>Returns all registered node IDs</summary>
    public IReadOnlyList<string> AllIds => _nodes.Select(n => n.Id).ToList();

    // ── Private ────────────────────────────────────────────────────────────────

    private void Register(ShadowNode node)
    {
        if (_index.TryGetValue(node.Id, out var existing))
            _nodes.Remove(existing);
        _nodes.Add(node);
        _index[node.Id] = node;
    }

    private static bool IsNearPath(Point p, IReadOnlyList<Point> pts, double tol)
    {
        double tol2 = tol * tol;
        for (int i = 1; i < pts.Count; i++)
        {
            if (DistanceToSegmentSq(p, pts[i - 1], pts[i]) <= tol2)
                return true;
        }
        return false;
    }

    private static double DistanceToSegmentSq(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-12) return DistanceSq(p, a);
        double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq;
        t = Math.Clamp(t, 0, 1);
        return DistanceSq(p, new Point(a.X + t * dx, a.Y + t * dy));
    }

    private static double DistanceSq(Point a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}

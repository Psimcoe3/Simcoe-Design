namespace ElectricalComponentSandbox.Services;

using System.Windows;
using ElectricalComponentSandbox.Models;

/// <summary>
/// Creates and manages home-run wire annotations on the drawing canvas.
/// Mirrors the Autodesk.Revit.DB.Electrical.Wire vertex API:
///   AppendVertex / InsertVertex / RemoveVertex / AreVertexPointsValid (→ ValidateVertices).
/// </summary>
public class HomeRunWireService
{
    /// <summary>Default half-length of each tick mark in canvas units.</summary>
    public const double DefaultTickHalfLength = 6.0;

    /// <summary>Default centre-to-centre spacing between tick marks along the wire.</summary>
    public const double DefaultTickSpacing = 8.0;

    // ── Creation ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="HomeRunWire"/>. Requires at least two distinct vertices.
    /// </summary>
    /// <param name="circuitId">Circuit ID this wire annotates (may be empty).</param>
    /// <param name="panelId">Panel ID this wire originates from (may be empty).</param>
    /// <param name="vertices">Ordered vertex list; first = panel end, last = device end.</param>
    /// <param name="wiringStyle">Shape style for rendering.</param>
    public HomeRunWire CreateHomeRun(
        string circuitId,
        string panelId,
        IEnumerable<Point> vertices,
        WiringStyle wiringStyle = WiringStyle.Chamfer)
    {
        if (vertices is null) throw new ArgumentNullException(nameof(vertices));
        var pts = vertices.ToList();
        if (!ValidateVertices(pts))
            throw new ArgumentException(
                "HomeRunWire requires at least 2 vertices with no two adjacent coincident points.",
                nameof(vertices));

        return new HomeRunWire
        {
            CircuitId = circuitId ?? string.Empty,
            PanelId = panelId ?? string.Empty,
            WiringStyle = wiringStyle,
            Vertices = pts
        };
    }

    // ── Vertex editing ───────────────────────────────────────────────────────

    /// <summary>
    /// Replaces all vertices on an existing wire.
    /// Validates the replacement list before applying.
    /// </summary>
    public void UpdateVertices(HomeRunWire wire, IEnumerable<Point> vertices)
    {
        ArgumentNullException.ThrowIfNull(wire);
        if (vertices is null) throw new ArgumentNullException(nameof(vertices));
        var pts = vertices.ToList();
        if (!ValidateVertices(pts))
            throw new ArgumentException(
                "Replacement vertices are invalid: need ≥2 non-coincident adjacent points.",
                nameof(vertices));
        wire.Vertices = pts;
    }

    /// <summary>
    /// Appends a vertex to the end of the wire's vertex list.
    /// The new point must not be coincident with the current last vertex.
    /// </summary>
    public void AppendVertex(HomeRunWire wire, Point point)
    {
        ArgumentNullException.ThrowIfNull(wire);
        if (wire.Vertices.Count > 0 && AreCoincident(wire.Vertices[^1], point))
            throw new ArgumentException(
                "Appended vertex is coincident with the existing last vertex.",
                nameof(point));
        wire.Vertices.Add(point);
    }

    /// <summary>
    /// Inserts a vertex at <paramref name="index"/>. Index 0 inserts before the start;
    /// index equal to vertex count appends at the end.
    /// The inserted point must not be coincident with adjacent existing vertices.
    /// </summary>
    public void InsertVertex(HomeRunWire wire, int index, Point point)
    {
        ArgumentNullException.ThrowIfNull(wire);
        if (index < 0 || index > wire.Vertices.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index > 0 && AreCoincident(wire.Vertices[index - 1], point))
            throw new ArgumentException(
                "Inserted vertex is coincident with the preceding vertex.",
                nameof(point));
        if (index < wire.Vertices.Count && AreCoincident(wire.Vertices[index], point))
            throw new ArgumentException(
                "Inserted vertex is coincident with the following vertex.",
                nameof(point));
        wire.Vertices.Insert(index, point);
    }

    /// <summary>
    /// Removes the vertex at <paramref name="index"/>.
    /// The wire must retain at least 2 vertices after removal.
    /// </summary>
    public void RemoveVertex(HomeRunWire wire, int index)
    {
        ArgumentNullException.ThrowIfNull(wire);
        if (index < 0 || index >= wire.Vertices.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (wire.Vertices.Count <= 2)
            throw new InvalidOperationException(
                "Cannot remove vertex: a HomeRunWire requires at least 2 vertices.");
        wire.Vertices.RemoveAt(index);
    }

    // ── Validation ───────────────────────────────────────────────────────────

    /// <summary>
    /// Mirrors Revit <c>Wire.AreVertexPointsValid</c>.
    /// Returns <c>true</c> when the list has at least 2 points and no two
    /// adjacent points are coincident.
    /// </summary>
    public bool ValidateVertices(IReadOnlyList<Point> vertices)
    {
        if (vertices == null || vertices.Count < 2)
            return false;
        for (int i = 0; i < vertices.Count - 1; i++)
        {
            if (AreCoincident(vertices[i], vertices[i + 1]))
                return false;
        }
        return true;
    }

    // ── Tick marks ───────────────────────────────────────────────────────────

    /// <summary>
    /// Computes tick-mark line segments at the panel end (Vertices[0]).
    /// Returns <see cref="HomeRunWire.HotConductors"/> pairs of (Start, End) points,
    /// each a perpendicular slash across the wire at the panel end.
    /// Tick centres are placed at <paramref name="tickSpacing"/> intervals starting
    /// <paramref name="tickSpacing"/> units from Vertices[0] along the first segment.
    /// </summary>
    /// <param name="wire">Wire whose panel end is Vertices[0].</param>
    /// <param name="tickHalfLength">Half-length of each tick mark (canvas units).</param>
    /// <param name="tickSpacing">Centre-to-centre spacing of tick marks along the wire.</param>
    public IReadOnlyList<(Point Start, Point End)> GetTickMarkPoints(
        HomeRunWire wire,
        double tickHalfLength = DefaultTickHalfLength,
        double tickSpacing = DefaultTickSpacing)
    {
        ArgumentNullException.ThrowIfNull(wire);
        var result = new List<(Point, Point)>();

        if (wire.Vertices.Count < 2)
            return result;

        // Unit direction along the first segment (panel end → second vertex)
        var p0 = wire.Vertices[0];
        var p1 = wire.Vertices[1];
        double dx = p1.X - p0.X;
        double dy = p1.Y - p0.Y;
        double segLen = Math.Sqrt(dx * dx + dy * dy);
        if (segLen < 1e-9)
            return result;

        double ux = dx / segLen;   // unit vector along wire
        double uy = dy / segLen;
        double px = -uy;           // unit perpendicular (CCW rotation)
        double py = ux;

        int n = Math.Max(1, wire.HotConductors);
        for (int i = 0; i < n; i++)
        {
            double t = tickSpacing * (i + 1);
            double cx = p0.X + ux * t;
            double cy = p0.Y + uy * t;
            result.Add((
                new Point(cx - px * tickHalfLength, cy - py * tickHalfLength),
                new Point(cx + px * tickHalfLength, cy + py * tickHalfLength)
            ));
        }

        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private const double CoincidenceTolerance = 1e-6;

    private static bool AreCoincident(Point a, Point b)
        => Math.Abs(a.X - b.X) < CoincidenceTolerance
        && Math.Abs(a.Y - b.Y) < CoincidenceTolerance;
}

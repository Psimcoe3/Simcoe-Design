using System.Windows;
using System.Windows.Media;

namespace ElectricalComponentSandbox.Rendering;

/// <summary>
/// Rendering style descriptor passed to draw calls.
/// All colors are ARGB hex strings ("#AARRGGBB") or named colors ("#RRGGBB").
/// </summary>
public sealed class RenderStyle
{
    public string StrokeColor { get; set; } = "#FF000000";
    public double StrokeWidth { get; set; } = 1.0;
    public string? FillColor { get; set; }          // null = no fill
    public float[]? DashPattern { get; set; }       // null = solid
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 12.0;
    public bool Bold { get; set; }
    public double Opacity { get; set; } = 1.0;

    public static RenderStyle Default => new();

    public static RenderStyle Solid(string color, double width = 1.0) =>
        new() { StrokeColor = color, StrokeWidth = width };

    public static RenderStyle Filled(string stroke, string fill, double width = 1.0) =>
        new() { StrokeColor = stroke, FillColor = fill, StrokeWidth = width };

    public static RenderStyle Dashed(string color, double width = 1.0) =>
        new() { StrokeColor = color, StrokeWidth = width, DashPattern = new[] { 6f, 3f } };

    public static RenderStyle Hidden(string color, double width = 0.5) =>
        new() { StrokeColor = color, StrokeWidth = width, DashPattern = new[] { 2f, 4f } };

    public static RenderStyle Center(string color, double width = 0.5) =>
        new() { StrokeColor = color, StrokeWidth = width, DashPattern = new[] { 8f, 3f, 2f, 3f } };
}

/// <summary>
/// Text alignment options for DrawText calls
/// </summary>
public enum TextAlign { Left, Center, Right }

/// <summary>
/// Snap glyph type drawn at candidate snap points
/// </summary>
public enum SnapGlyphType { Endpoint, Midpoint, Center, Intersection, Nearest, Perpendicular, Quadrant }

/// <summary>
/// Abstraction over 2D canvas rendering backend.
/// All coordinates are in Document space (PDF points / drawing units).
/// The implementation handles the Document→Screen transform internally.
/// </summary>
public interface ICanvas2DRenderer
{
    // ── Canvas lifecycle ──────────────────────────────────────────────────────

    /// <summary>Clears the entire canvas to the background color</summary>
    void Clear(string backgroundColor = "#FF1E1E1E");

    /// <summary>Requests a full redraw on the next frame</summary>
    void RequestRedraw();

    /// <summary>Requests a redraw of a specific document-space region</summary>
    void InvalidateRegion(Rect docRect);

    // ── Transform state ───────────────────────────────────────────────────────

    /// <summary>Pushes a transform (e.g. for a component's local coordinate system)</summary>
    void PushTransform(double translateX, double translateY, double rotateDeg = 0, double scale = 1.0);

    /// <summary>Pops the most recently pushed transform</summary>
    void PopTransform();

    // ── Primitive draw calls ──────────────────────────────────────────────────

    /// <summary>Draws a single line segment</summary>
    void DrawLine(Point start, Point end, RenderStyle style);

    /// <summary>Draws a sequence of connected line segments (open polyline)</summary>
    void DrawPolyline(IReadOnlyList<Point> points, RenderStyle style);

    /// <summary>Draws a closed polygon with optional fill</summary>
    void DrawPolygon(IReadOnlyList<Point> points, RenderStyle style);

    /// <summary>Draws an axis-aligned rectangle</summary>
    void DrawRect(Rect rect, RenderStyle style);

    /// <summary>Draws an ellipse; if radiusY == 0 treats as circle with radius = radiusX</summary>
    void DrawEllipse(Point center, double radiusX, double radiusY, RenderStyle style);

    /// <summary>Draws an arc defined by center, radii, start and sweep angles (degrees)</summary>
    void DrawArc(Point center, double radiusX, double radiusY,
                 double startAngleDeg, double sweepAngleDeg, RenderStyle style);

    /// <summary>Draws a cubic Bezier path through the given control points</summary>
    void DrawBezier(IReadOnlyList<Point> controlPoints, RenderStyle style);

    /// <summary>Draws a filled and stroked hatch region</summary>
    void DrawHatch(IReadOnlyList<Point> boundary, HatchPattern pattern, RenderStyle style);

    // ── Text ─────────────────────────────────────────────────────────────────

    /// <summary>Draws a text string at the given document-space anchor point</summary>
    void DrawText(Point anchor, string text, RenderStyle style, TextAlign align = TextAlign.Left);

    /// <summary>Draws a text string with background fill (for labels/dimensions)</summary>
    void DrawTextBox(Point anchor, string text, RenderStyle textStyle,
                     string boxFill = "#CCFFFFFF", double padding = 3.0);

    // ── CAD-specific overlays ─────────────────────────────────────────────────

    /// <summary>Draws a dimension line (linear/aligned) with arrows and text</summary>
    void DrawDimension(Point p1, Point p2, double offset, string valueText,
                       DimensionStyle dimStyle);

    /// <summary>Draws snap glyph at the given document position (drawn in screen space)</summary>
    void DrawSnapGlyph(Point docPos, SnapGlyphType glyphType);

    /// <summary>Draws an ortho/polar tracking guide line across the viewport</summary>
    void DrawTrackingLine(Point docOrigin, double angleDeg);

    /// <summary>Draws a selection rectangle (window or crossing style)</summary>
    void DrawSelectionRect(Rect docRect, bool crossing);

    /// <summary>Draws a grip handle at the given point</summary>
    void DrawGrip(Point docPos, bool hot = false);

    /// <summary>Draws a revision cloud path along the given polyline</summary>
    void DrawRevisionCloud(IReadOnlyList<Point> points, RenderStyle style, double arcRadius = 0.5);

    /// <summary>Draws a leader line with optional arrowhead and callout box</summary>
    void DrawLeader(IReadOnlyList<Point> points, string? calloutText, RenderStyle style);

    // ── Viewport info ─────────────────────────────────────────────────────────

    /// <summary>Current document-space viewport bounds (what's visible)</summary>
    Rect ViewportDocRect { get; }

    /// <summary>Current zoom (screen pixels per document unit)</summary>
    double Zoom { get; }
}

/// <summary>
/// Standard hatch patterns for electrical drawings
/// </summary>
public enum HatchPattern
{
    Solid,
    ANSI31,     // 45° diagonal lines (general material)
    ANSI37,     // Horizontal lines
    NET,        // Grid
    DotSmall,   // Dot pattern
    Electrical, // VV diagonal (electrical conduit area)
}

/// <summary>
/// Dimension style controls appearance of automatic dimension annotations
/// </summary>
public sealed class DimensionStyle
{
    public static DimensionStyle Default { get; } = new();

    public double ArrowSize { get; set; } = 0.1;     // document units
    public double TextHeight { get; set; } = 0.1;    // document units
    public string FontFamily { get; set; } = "Segoe UI";
    public string LineColor { get; set; } = "#FF0000FF";
    public string TextColor { get; set; } = "#FF0000FF";
    public string Prefix { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    /// <summary>Number of decimal places for decimal display; -1 = fractional feet-inches</summary>
    public int Precision { get; set; } = 2;
    /// <summary>Extension line overshoot beyond arrow, in document units</summary>
    public double ExtLineOvershoot { get; set; } = 0.05;
    /// <summary>Gap between geometry and extension line start, in document units</summary>
    public double ExtLineOffset { get; set; } = 0.03;
}

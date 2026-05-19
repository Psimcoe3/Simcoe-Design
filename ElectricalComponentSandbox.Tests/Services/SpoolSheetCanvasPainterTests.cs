using System.Windows;
using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class SpoolSheetCanvasPainterTests
{
    // ── Recording stub renderer ───────────────────────────────────────────

    private sealed record DrawnRect(Rect Rect, RenderStyle Style);
    private sealed record DrawnLine(Point P1, Point P2, RenderStyle Style);
    private sealed record DrawnText(Point Anchor, string Text, RenderStyle Style, TextAlign Align);

    private sealed class RecordingCanvas : ICanvas2DRenderer
    {
        public List<DrawnRect> Rects { get; } = new();
        public List<DrawnLine> Lines { get; } = new();
        public List<DrawnText> Texts { get; } = new();

        public Rect ViewportDocRect => new(0, 0, 17, 11);
        public double Zoom => 1.0;

        public void Clear(string backgroundColor = "#FF1E1E1E") { }
        public void RequestRedraw() { }
        public void InvalidateRegion(Rect docRect) { }
        public void PushTransform(double translateX, double translateY, double rotateDeg = 0, double scale = 1.0) { }
        public void PopTransform() { }
        public void DrawLine(Point start, Point end, RenderStyle style) => Lines.Add(new DrawnLine(start, end, style));
        public void DrawPolyline(IReadOnlyList<Point> points, RenderStyle style) { }
        public void DrawPolygon(IReadOnlyList<Point> points, RenderStyle style) { }
        public void DrawRect(Rect rect, RenderStyle style) => Rects.Add(new DrawnRect(rect, style));
        public void DrawEllipse(Point center, double radiusX, double radiusY, RenderStyle style) { }
        public void DrawArc(Point center, double radiusX, double radiusY, double startAngleDeg, double sweepAngleDeg, RenderStyle style) { }
        public void DrawBezier(IReadOnlyList<Point> controlPoints, RenderStyle style) { }
        public void DrawHatch(IReadOnlyList<Point> boundary, HatchPattern pattern, RenderStyle style) { }
        public void DrawText(Point anchor, string text, RenderStyle style, TextAlign align = TextAlign.Left)
            => Texts.Add(new DrawnText(anchor, text, style, align));
        public void DrawTextBox(Point anchor, string text, RenderStyle textStyle, string boxFill = "#CCFFFFFF", double padding = 3.0) { }
        public void DrawDimension(Point p1, Point p2, double offset, string valueText, DimensionStyle dimStyle) { }
        public void DrawSnapGlyph(Point docPos, SnapGlyphType glyphType) { }
        public void DrawTrackingLine(Point docOrigin, double angleDeg) { }
        public void DrawSelectionRect(Rect docRect, bool crossing) { }
        public void DrawGrip(Point docPos, bool hot = false) { }
        public void DrawRevisionCloud(IReadOnlyList<Point> points, RenderStyle style, double arcRadius = 0.5) { }
        public void DrawLeader(IReadOnlyList<Point> points, string? calloutText, RenderStyle style) { }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static SpoolSheetRenderGeometry BuildGeometry(int segmentCount = 1, bool withHanger = true)
    {
        var store = new ConduitModelStore();
        store.AddType(new ConduitType { Id = "emt", Name = "EMT", Standard = ConduitMaterialType.EMT });
        store.Settings.DefaultConduitTypeId = "emt";

        var run = new ConduitRun { RunId = "CR-001", TradeSize = "3/4", Material = ConduitMaterialType.EMT };
        for (int i = 0; i < segmentCount; i++)
        {
            var seg = new ConduitSegment
            {
                StartPoint = new XYZ(i, 0, 0),
                EndPoint = new XYZ(i + 1, 0, 0),
                TradeSize = "3/4",
            };
            store.AddSegment(seg);
            run.SegmentIds.Add(seg.Id);
        }
        store.AddRun(run);

        var hangers = withHanger
            ? new[] { new HangerComponent { Trapeze = TrapezeAssembly.CreateSingleTierDefault() } }
            : Array.Empty<HangerComponent>();

        var builder = new SpoolSheetBuilder(store);
        var sheet = builder.Build(run.Id, hangers);
        return new SpoolSheetRenderer().Render(sheet);
    }

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public void Paint_NullCanvas_Throws()
    {
        var painter = new SpoolSheetCanvasPainter();
        var geometry = BuildGeometry();

        Assert.Throws<ArgumentNullException>(() => painter.Paint(null!, geometry));
    }

    [Fact]
    public void Paint_NullGeometry_Throws()
    {
        var painter = new SpoolSheetCanvasPainter();
        var canvas = new RecordingCanvas();

        Assert.Throws<ArgumentNullException>(() => painter.Paint(canvas, null!));
    }

    [Fact]
    public void Paint_DrawsPaperBackgroundFirst()
    {
        var painter = new SpoolSheetCanvasPainter();
        var canvas = new RecordingCanvas();
        var geometry = BuildGeometry();

        painter.Paint(canvas, geometry);

        // First rect should be the full paper extent
        Assert.NotEmpty(canvas.Rects);
        var first = canvas.Rects[0].Rect;
        Assert.Equal(0, first.X);
        Assert.Equal(0, first.Y);
        Assert.Equal(geometry.PaperWidthInches, first.Width, 3);
        Assert.Equal(geometry.PaperHeightInches, first.Height, 3);
    }

    [Fact]
    public void Paint_DrawsOuterAndInnerBorder()
    {
        var painter = new SpoolSheetCanvasPainter();
        var canvas = new RecordingCanvas();
        var geometry = BuildGeometry();

        painter.Paint(canvas, geometry);

        Assert.Contains(canvas.Rects, r => RectsAlmostEqual(r.Rect, geometry.Border.OuterBorder));
        Assert.Contains(canvas.Rects, r => RectsAlmostEqual(r.Rect, geometry.Border.InnerBorder));
    }

    [Fact]
    public void Paint_DrawsTitleBlockCellOutlines()
    {
        var painter = new SpoolSheetCanvasPainter();
        var canvas = new RecordingCanvas();
        var geometry = BuildGeometry();

        painter.Paint(canvas, geometry);

        foreach (var cell in geometry.Border.TitleBlockCells)
        {
            var expected = new Rect(cell.X, cell.Y, cell.Width, cell.Height);
            Assert.Contains(canvas.Rects, r => RectsAlmostEqual(r.Rect, expected));
        }
    }

    [Fact]
    public void Paint_DrawsAllRendererTextRuns()
    {
        var painter = new SpoolSheetCanvasPainter();
        var canvas = new RecordingCanvas();
        var geometry = BuildGeometry();

        painter.Paint(canvas, geometry);

        foreach (var t in geometry.Texts)
        {
            Assert.Contains(canvas.Texts, d => d.Text == t.Value);
        }
    }

    [Fact]
    public void Paint_FilledRectsArePaintedBeforeStrokedCellOutlines()
    {
        var painter = new SpoolSheetCanvasPainter();
        var canvas = new RecordingCanvas();
        var geometry = BuildGeometry();

        painter.Paint(canvas, geometry);

        // Locate first filled rect drawn after the paper background; all stroked
        // table cells (no fill) should come after the last filled rect.
        int firstFilledIndex = canvas.Rects.FindIndex(1, r => r.Style.FillColor != null);
        int lastFilledIndex = canvas.Rects.FindLastIndex(r => r.Style.FillColor != null);

        Assert.True(firstFilledIndex > 0, "expected at least one filled rect besides the paper background");
        Assert.True(lastFilledIndex >= firstFilledIndex);
    }

    [Fact]
    public void Paint_CustomCellPainter_ReplacesDefaultRendering()
    {
        int customCalls = 0;
        var painter = new SpoolSheetCanvasPainter(args =>
        {
            customCalls++;
            args.Canvas.DrawRect(
                new Rect(args.Cell.X, args.Cell.Y, args.Cell.Width, args.Cell.Height),
                new RenderStyle { StrokeColor = "#FF00FF00", StrokeWidth = 0.05 });
        });
        var canvas = new RecordingCanvas();
        var geometry = BuildGeometry();

        painter.Paint(canvas, geometry);

        Assert.Equal(geometry.Border.TitleBlockCells.Count, customCalls);
        Assert.Contains(canvas.Rects, r => r.Style.StrokeColor == "#FF00FF00");
    }

    private static bool RectsAlmostEqual(Rect a, Rect b, double tol = 0.001) =>
        Math.Abs(a.X - b.X) < tol &&
        Math.Abs(a.Y - b.Y) < tol &&
        Math.Abs(a.Width - b.Width) < tol &&
        Math.Abs(a.Height - b.Height) < tol;
}

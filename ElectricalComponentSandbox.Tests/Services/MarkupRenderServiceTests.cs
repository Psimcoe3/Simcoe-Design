using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Markup.Services;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class MarkupRenderServiceTests
{
    private readonly MarkupRenderService _sut = new();

    [Fact]
    public void Render_Callout_UsesLeader()
    {
        var renderer = new RecordingRenderer();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Callout,
            TextContent = "NOTE",
            Vertices = { new Point(10, 10), new Point(40, 30) }
        };

        _sut.Render(renderer, markup, DetailLevel.Fine);

        Assert.Equal(1, renderer.LeaderCalls);
    }

    [Fact]
    public void Render_RevisionCloud_UsesRevisionCloud()
    {
        var renderer = new RecordingRenderer();
        var markup = new MarkupRecord
        {
            Type = MarkupType.RevisionCloud,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10) }
        };

        _sut.Render(renderer, markup, DetailLevel.Fine);

        Assert.Equal(1, renderer.RevisionCloudCalls);
    }

    [Fact]
    public void Render_Stamp_DrawsRectAndCenteredText()
    {
        var renderer = new RecordingRenderer();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Stamp,
            TextContent = "APPROVED",
            BoundingRect = new Rect(10, 20, 120, 30)
        };

        _sut.Render(renderer, markup, DetailLevel.Fine);

        Assert.Equal(1, renderer.RectCalls);
        Assert.Equal(1, renderer.TextCalls);
        Assert.Equal(TextAlign.Center, renderer.LastTextAlign);
    }

    [Fact]
    public void Render_TextWithAlignment_UsesPlainText()
    {
        var renderer = new RecordingRenderer();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Text,
            TextContent = "HEADER",
            Vertices = { new Point(50, 60) }
        };
        markup.Metadata.CustomFields[DrawingAnnotationMarkupService.TextAlignField] = TextAlign.Right.ToString();

        _sut.Render(renderer, markup, DetailLevel.Fine);

        Assert.Equal(1, renderer.TextCalls);
        Assert.Equal(0, renderer.TextBoxCalls);
        Assert.Equal(TextAlign.Right, renderer.LastTextAlign);
    }

    [Fact]
    public void Render_Hatch_UsesHatchAndOutline()
    {
        var renderer = new RecordingRenderer();
        var markup = new MarkupRecord
        {
            Type = MarkupType.Hatch,
            Vertices = { new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10) }
        };
        markup.Appearance.HatchPattern = HatchPattern.NET.ToString();

        _sut.Render(renderer, markup, DetailLevel.Fine);

        Assert.Equal(1, renderer.HatchCalls);
        Assert.Equal(1, renderer.PolygonCalls);
    }

    private sealed class RecordingRenderer : ICanvas2DRenderer
    {
        public int LeaderCalls { get; private set; }
        public int RevisionCloudCalls { get; private set; }
        public int RectCalls { get; private set; }
        public int TextCalls { get; private set; }
        public int TextBoxCalls { get; private set; }
        public int HatchCalls { get; private set; }
        public int PolygonCalls { get; private set; }
        public TextAlign LastTextAlign { get; private set; }

        public Rect ViewportDocRect => Rect.Empty;
        public double Zoom => 1.0;

        public void Clear(string backgroundColor = "#FF1E1E1E") { }
        public void RequestRedraw() { }
        public void InvalidateRegion(Rect docRect) { }
        public void PushTransform(double translateX, double translateY, double rotateDeg = 0, double scale = 1.0) { }
        public void PopTransform() { }
        public void DrawLine(Point start, Point end, RenderStyle style) { }
        public void DrawPolyline(IReadOnlyList<Point> points, RenderStyle style) { }
        public void DrawPolygon(IReadOnlyList<Point> points, RenderStyle style) => PolygonCalls++;
        public void DrawRect(Rect rect, RenderStyle style) => RectCalls++;
        public void DrawEllipse(Point center, double radiusX, double radiusY, RenderStyle style) { }
        public void DrawArc(Point center, double radiusX, double radiusY, double startAngleDeg, double sweepAngleDeg, RenderStyle style) { }
        public void DrawBezier(IReadOnlyList<Point> controlPoints, RenderStyle style) { }
        public void DrawHatch(IReadOnlyList<Point> boundary, HatchPattern pattern, RenderStyle style) => HatchCalls++;
        public void DrawText(Point anchor, string text, RenderStyle style, TextAlign align = TextAlign.Left)
        {
            TextCalls++;
            LastTextAlign = align;
        }

        public void DrawTextBox(Point anchor, string text, RenderStyle textStyle, string boxFill = "#CCFFFFFF", double padding = 3.0)
            => TextBoxCalls++;

        public void DrawDimension(Point p1, Point p2, double offset, string valueText, DimensionStyle dimStyle) { }
        public void DrawSnapGlyph(Point docPos, SnapGlyphType glyphType) { }
        public void DrawTrackingLine(Point docOrigin, double angleDeg) { }
        public void DrawSelectionRect(Rect docRect, bool crossing) { }
        public void DrawGrip(Point docPos, bool hot = false) { }
        public void DrawRevisionCloud(IReadOnlyList<Point> points, RenderStyle style, double arcRadius = 0.5) => RevisionCloudCalls++;
        public void DrawLeader(IReadOnlyList<Point> points, string? calloutText, RenderStyle style) => LeaderCalls++;
    }
}

using System.Windows;
using SkiaSharp;

namespace ElectricalComponentSandbox.Rendering;

/// <summary>
/// SkiaSharp-backed implementation of <see cref="ICanvas2DRenderer"/>.
/// Converts all Document-space coordinates to Screen space via
/// <see cref="DrawingContext2D"/> before issuing Skia draw calls.
/// </summary>
public sealed class SkiaCanvas2DRenderer : ICanvas2DRenderer
{
    private SKCanvas _canvas;
    private DrawingContext2D _ctx;
    private readonly Stack<SKMatrix> _transformStack = new();

    internal SKCanvas Canvas => _canvas;

    public SkiaCanvas2DRenderer(SKCanvas canvas, DrawingContext2D ctx)
    {
        _canvas = canvas;
        _ctx = ctx;
    }

    internal void UpdateCanvas(SKCanvas canvas, DrawingContext2D ctx)
    {
        _canvas = canvas;
        _ctx = ctx;
    }

    // ── Viewport ──────────────────────────────────────────────────────────────

    public Rect ViewportDocRect => _ctx.ViewportDocRect;
    public double Zoom => _ctx.Zoom;

    // ── Canvas lifecycle ──────────────────────────────────────────────────────

    public void Clear(string backgroundColor = "#FF1E1E1E")
    {
        _canvas.Clear(ParseColor(backgroundColor));
    }

    public void RequestRedraw() { /* SkiaCanvasHost.RequestRedraw() drives this */ }

    public void InvalidateRegion(Rect docRect) { /* forwarded from host */ }

    // ── Transform ─────────────────────────────────────────────────────────────

    public void PushTransform(double tx, double ty, double rotateDeg = 0, double scale = 1.0)
    {
        _transformStack.Push(_canvas.TotalMatrix);
        var mx = SKMatrix.CreateTranslation((float)tx, (float)ty);
        if (rotateDeg != 0)
            mx = mx.PostConcat(SKMatrix.CreateRotationDegrees((float)rotateDeg));
        if (scale != 1.0)
            mx = mx.PostConcat(SKMatrix.CreateScale((float)scale, (float)scale));
        _canvas.Concat(ref mx);
    }

    public void PopTransform()
    {
        if (_transformStack.Count > 0)
            _canvas.SetMatrix(_transformStack.Pop());
    }

    // ── Primitives ────────────────────────────────────────────────────────────

    public void DrawLine(Point start, Point end, RenderStyle style)
    {
        using var paint = BuildStrokePaint(style);
        var s = ToScreen(start);
        var e = ToScreen(end);
        _canvas.DrawLine(s.X, s.Y, e.X, e.Y, paint);
    }

    public void DrawPolyline(IReadOnlyList<Point> points, RenderStyle style)
    {
        if (points.Count < 2) return;
        using var paint = BuildStrokePaint(style);
        using var path = BuildScreenPath(points, closed: false);
        _canvas.DrawPath(path, paint);
    }

    public void DrawPolygon(IReadOnlyList<Point> points, RenderStyle style)
    {
        if (points.Count < 2) return;
        using var path = BuildScreenPath(points, closed: true);

        if (style.FillColor is not null)
        {
            using var fill = BuildFillPaint(style);
            _canvas.DrawPath(path, fill);
        }

        using var stroke = BuildStrokePaint(style);
        _canvas.DrawPath(path, stroke);
    }

    public void DrawRect(Rect rect, RenderStyle style)
    {
        var tl = ToScreen(new Point(rect.Left, rect.Top));
        var br = ToScreen(new Point(rect.Right, rect.Bottom));
        var skRect = new SKRect(tl.X, tl.Y, br.X, br.Y);

        if (style.FillColor is not null)
        {
            using var fill = BuildFillPaint(style);
            _canvas.DrawRect(skRect, fill);
        }

        using var stroke = BuildStrokePaint(style);
        _canvas.DrawRect(skRect, stroke);
    }

    public void DrawEllipse(Point center, double radiusX, double radiusY, RenderStyle style)
    {
        var sc = ToScreen(center);
        float rx = (float)(radiusX * _ctx.Zoom);
        float ry = (float)((radiusY == 0 ? radiusX : radiusY) * _ctx.Zoom);

        if (style.FillColor is not null)
        {
            using var fill = BuildFillPaint(style);
            _canvas.DrawOval(sc.X, sc.Y, rx, ry, fill);
        }

        using var stroke = BuildStrokePaint(style);
        _canvas.DrawOval(sc.X, sc.Y, rx, ry, stroke);
    }

    public void DrawArc(Point center, double radiusX, double radiusY,
                        double startAngleDeg, double sweepAngleDeg, RenderStyle style)
    {
        var sc = ToScreen(center);
        float rx = (float)(radiusX * _ctx.Zoom);
        float ry = (float)((radiusY == 0 ? radiusX : radiusY) * _ctx.Zoom);
        var oval = new SKRect(sc.X - rx, sc.Y - ry, sc.X + rx, sc.Y + ry);

        using var path = new SKPath();
        path.AddArc(oval, (float)startAngleDeg, (float)sweepAngleDeg);

        using var stroke = BuildStrokePaint(style);
        _canvas.DrawPath(path, stroke);
    }

    public void DrawBezier(IReadOnlyList<Point> controlPoints, RenderStyle style)
    {
        if (controlPoints.Count < 4) return;
        using var path = new SKPath();
        var p0 = ToScreen(controlPoints[0]);
        path.MoveTo(p0.X, p0.Y);

        int i = 1;
        while (i + 2 < controlPoints.Count)
        {
            var c1 = ToScreen(controlPoints[i]);
            var c2 = ToScreen(controlPoints[i + 1]);
            var ep = ToScreen(controlPoints[i + 2]);
            path.CubicTo(c1.X, c1.Y, c2.X, c2.Y, ep.X, ep.Y);
            i += 3;
        }

        using var stroke = BuildStrokePaint(style);
        _canvas.DrawPath(path, stroke);
    }

    public void DrawHatch(IReadOnlyList<Point> boundary, HatchPattern pattern, RenderStyle style)
    {
        using var clipPath = BuildScreenPath(boundary, closed: true);
        _canvas.Save();
        _canvas.ClipPath(clipPath);

        var bounds = GetScreenBounds(boundary);
        using var hatchPaint = BuildHatchPaint(pattern, style);
        _canvas.DrawRect(bounds, hatchPaint);

        _canvas.Restore();
    }

    // ── Text ──────────────────────────────────────────────────────────────────

    public void DrawText(Point anchor, string text, RenderStyle style, TextAlign align = TextAlign.Left)
    {
        using var paint = BuildTextPaint(style);
        var sc = ToScreen(anchor);

        float x = sc.X;
        if (align == TextAlign.Center)
        {
            float w = paint.MeasureText(text);
            x -= w / 2;
        }
        else if (align == TextAlign.Right)
        {
            float w = paint.MeasureText(text);
            x -= w;
        }

        _canvas.DrawText(text, x, sc.Y, paint);
    }

    public void DrawTextBox(Point anchor, string text, RenderStyle textStyle,
                            string boxFill = "#CCFFFFFF", double padding = 3.0)
    {
        using var textPaint = BuildTextPaint(textStyle);
        float w = textPaint.MeasureText(text);
        float h = (float)textStyle.FontSize;
        var sc = ToScreen(anchor);
        float pad = (float)padding;

        var boxRect = new SKRect(sc.X - pad, sc.Y - h - pad, sc.X + w + pad, sc.Y + pad);

        using var boxPaint = new SKPaint { Color = ParseColor(boxFill), IsAntialias = true };
        using var borderPaint = new SKPaint { Color = ParseColor(textStyle.StrokeColor), IsAntialias = true, IsStroke = true, StrokeWidth = 1 };

        _canvas.DrawRoundRect(boxRect, 3, 3, boxPaint);
        _canvas.DrawRoundRect(boxRect, 3, 3, borderPaint);
        _canvas.DrawText(text, sc.X, sc.Y, textPaint);
    }

    // ── CAD overlays ──────────────────────────────────────────────────────────

    public void DrawDimension(Point p1, Point p2, double offset,
                               string valueText, DimensionStyle dimStyle)
    {
        var s1 = ToScreen(p1);
        var s2 = ToScreen(p2);

        // Compute perpendicular direction for offset
        double dx = s2.X - s1.X, dy = s2.Y - s1.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) return;
        double nx = -dy / len, ny = dx / len;

        float screenOffset = (float)(offset * _ctx.Zoom);
        float ox = (float)(nx * screenOffset), oy = (float)(ny * screenOffset);

        var d1 = new SKPoint(s1.X + ox, s1.Y + oy);
        var d2 = new SKPoint(s2.X + ox, s2.Y + oy);

        float arrSize = (float)(dimStyle.ArrowSize * _ctx.Zoom);
        float extOver  = (float)(dimStyle.ExtLineOvershoot * _ctx.Zoom);
        float extOff   = (float)(dimStyle.ExtLineOffset * _ctx.Zoom);

        using var linePaint = new SKPaint
        {
            Color = ParseColor(dimStyle.LineColor),
            StrokeWidth = 1f,
            IsStroke = true,
            IsAntialias = true
        };
        using var textPaint = BuildTextPaint(new RenderStyle
        {
            StrokeColor = dimStyle.TextColor,
            FontFamily = dimStyle.FontFamily,
            FontSize = dimStyle.TextHeight * 72
        });

        // Extension lines
        float extDirX1 = (float)(nx * extOff), extDirY1 = (float)(ny * extOff);
        _canvas.DrawLine(s1.X + extDirX1, s1.Y + extDirY1,
                         d1.X + (float)(nx * extOver), d1.Y + (float)(ny * extOver), linePaint);
        _canvas.DrawLine(s2.X + extDirX1, s2.Y + extDirY1,
                         d2.X + (float)(nx * extOver), d2.Y + (float)(ny * extOver), linePaint);

        // Dimension line
        _canvas.DrawLine(d1, d2, linePaint);

        // Arrowheads
        DrawArrow(_canvas, d1, d2, arrSize, linePaint);
        DrawArrow(_canvas, d2, d1, arrSize, linePaint);

        // Value text at midpoint
        float mx = (d1.X + d2.X) / 2, my = (d1.Y + d2.Y) / 2;
        float tw = textPaint.MeasureText(valueText);
        _canvas.DrawText(valueText, mx - tw / 2, my - 4, textPaint);
    }

    public void DrawSnapGlyph(Point docPos, SnapGlyphType glyphType)
    {
        var sc = ToScreen(docPos);
        float size = 8f;

        using var paint = new SKPaint
        {
            Color = SKColors.Yellow,
            StrokeWidth = 1.5f,
            IsStroke = true,
            IsAntialias = true
        };

        switch (glyphType)
        {
            case SnapGlyphType.Endpoint:
                _canvas.DrawRect(new SKRect(sc.X - size / 2, sc.Y - size / 2, sc.X + size / 2, sc.Y + size / 2), paint);
                break;
            case SnapGlyphType.Midpoint:
                var tri = new SKPath();
                tri.MoveTo(sc.X, sc.Y - size / 2);
                tri.LineTo(sc.X + size / 2, sc.Y + size / 2);
                tri.LineTo(sc.X - size / 2, sc.Y + size / 2);
                tri.Close();
                _canvas.DrawPath(tri, paint);
                break;
            case SnapGlyphType.Center:
                _canvas.DrawCircle(sc.X, sc.Y, size / 2, paint);
                break;
            case SnapGlyphType.Intersection:
                _canvas.DrawLine(sc.X - size / 2, sc.Y - size / 2, sc.X + size / 2, sc.Y + size / 2, paint);
                _canvas.DrawLine(sc.X + size / 2, sc.Y - size / 2, sc.X - size / 2, sc.Y + size / 2, paint);
                break;
            case SnapGlyphType.Nearest:
                _canvas.DrawCircle(sc.X, sc.Y, size / 3, paint);
                _canvas.DrawLine(sc.X, sc.Y - size / 2, sc.X, sc.Y + size / 2, paint);
                break;
            case SnapGlyphType.Perpendicular:
                _canvas.DrawLine(sc.X - size / 2, sc.Y, sc.X + size / 2, sc.Y, paint);
                _canvas.DrawLine(sc.X, sc.Y, sc.X, sc.Y + size / 2, paint);
                break;
            case SnapGlyphType.Quadrant:
                _canvas.DrawLine(sc.X - size / 2, sc.Y, sc.X + size / 2, sc.Y, paint);
                _canvas.DrawLine(sc.X, sc.Y - size / 2, sc.X, sc.Y + size / 2, paint);
                _canvas.DrawCircle(sc.X, sc.Y, size / 2, paint);
                break;
        }
    }

    public void DrawTrackingLine(Point docOrigin, double angleDeg)
    {
        var sc = ToScreen(docOrigin);
        double rad = angleDeg * Math.PI / 180.0;
        float length = 5000f;
        float ex = sc.X + (float)(Math.Cos(rad) * length);
        float ey = sc.Y + (float)(Math.Sin(rad) * length);
        float sx = sc.X - (float)(Math.Cos(rad) * length);
        float sy = sc.Y - (float)(Math.Sin(rad) * length);

        using var paint = new SKPaint
        {
            Color = SKColors.LightGreen.WithAlpha(180),
            StrokeWidth = 1f,
            IsStroke = true,
            PathEffect = SKPathEffect.CreateDash(new[] { 8f, 4f }, 0),
            IsAntialias = true
        };
        _canvas.DrawLine(sx, sy, ex, ey, paint);
    }

    public void DrawSelectionRect(Rect docRect, bool crossing)
    {
        var tl = ToScreen(new Point(docRect.Left, docRect.Top));
        var br = ToScreen(new Point(docRect.Right, docRect.Bottom));
        var r = new SKRect(tl.X, tl.Y, br.X, br.Y);

        using var fillPaint = new SKPaint
        {
            Color = crossing
                ? new SKColor(255, 165, 0, 40)   // orange translucent = crossing
                : new SKColor(0, 120, 255, 40),   // blue translucent = window
            IsAntialias = true
        };
        using var borderPaint = new SKPaint
        {
            Color = crossing ? SKColors.Orange : new SKColor(0, 120, 255),
            StrokeWidth = 1f,
            IsStroke = true,
            PathEffect = crossing ? SKPathEffect.CreateDash(new[] { 5f, 3f }, 0) : null,
            IsAntialias = true
        };

        _canvas.DrawRect(r, fillPaint);
        _canvas.DrawRect(r, borderPaint);
    }

    public void DrawGrip(Point docPos, bool hot = false)
    {
        var sc = ToScreen(docPos);
        float size = hot ? 7f : 5f;
        var r = new SKRect(sc.X - size, sc.Y - size, sc.X + size, sc.Y + size);

        using var fill = new SKPaint
        { Color = hot ? SKColors.Red : SKColors.Cyan, IsAntialias = true };
        using var border = new SKPaint
        { Color = SKColors.White, StrokeWidth = 1f, IsStroke = true, IsAntialias = true };

        _canvas.DrawRect(r, fill);
        _canvas.DrawRect(r, border);
    }

    public void DrawRevisionCloud(IReadOnlyList<Point> points, RenderStyle style, double arcRadius = 0.5)
    {
        if (points.Count < 2) return;
        using var path = new SKPath();
        float r = (float)(arcRadius * _ctx.Zoom);

        for (int i = 0; i < points.Count; i++)
        {
            var a = ToScreen(points[i]);
            var b = ToScreen(points[(i + 1) % points.Count]);
            float cx = (a.X + b.X) / 2;
            float cy = (a.Y + b.Y) / 2;
            var oval = new SKRect(cx - r, cy - r, cx + r, cy + r);

            double ang = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180.0 / Math.PI;
            if (i == 0) path.MoveTo(a.X, a.Y);
            path.ArcTo(oval, (float)ang + 180, 180, false);
        }

        path.Close();
        using var stroke = BuildStrokePaint(style);
        _canvas.DrawPath(path, stroke);
    }

    public void DrawLeader(IReadOnlyList<Point> points, string? calloutText, RenderStyle style)
    {
        if (points.Count < 2) return;
        DrawPolyline(points, style);

        // Arrowhead at first point
        var p0 = ToScreen(points[0]);
        var p1 = ToScreen(points[1]);
        float arrSize = 6f;

        using var arrPaint = BuildStrokePaint(style);
        DrawArrow(_canvas, p0, p1, arrSize, arrPaint);

        if (!string.IsNullOrEmpty(calloutText))
        {
            var last = points[^1];
            DrawTextBox(last, calloutText!, style);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private SKPoint ToScreen(Point doc)
    {
        var s = _ctx.DocumentToScreen(doc);
        return new SKPoint((float)s.X, (float)s.Y);
    }

    private SKPath BuildScreenPath(IReadOnlyList<Point> points, bool closed)
    {
        var path = new SKPath();
        var first = ToScreen(points[0]);
        path.MoveTo(first.X, first.Y);
        for (int i = 1; i < points.Count; i++)
        {
            var p = ToScreen(points[i]);
            path.LineTo(p.X, p.Y);
        }
        if (closed) path.Close();
        return path;
    }

    private SKRect GetScreenBounds(IReadOnlyList<Point> points)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in points)
        {
            var s = ToScreen(p);
            minX = Math.Min(minX, s.X); minY = Math.Min(minY, s.Y);
            maxX = Math.Max(maxX, s.X); maxY = Math.Max(maxY, s.Y);
        }
        return new SKRect(minX, minY, maxX, maxY);
    }

    private static SKPaint BuildStrokePaint(RenderStyle style)
    {
        var paint = new SKPaint
        {
            Color = ParseColor(style.StrokeColor).WithAlpha((byte)(style.Opacity * 255)),
            StrokeWidth = (float)style.StrokeWidth,
            IsStroke = true,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        if (style.DashPattern is { Length: > 0 })
            paint.PathEffect = SKPathEffect.CreateDash(style.DashPattern, 0);
        return paint;
    }

    private static SKPaint BuildFillPaint(RenderStyle style)
    {
        string hex = style.FillColor ?? "#20808080";
        return new SKPaint
        {
            Color = ParseColor(hex).WithAlpha((byte)(style.Opacity * 255)),
            IsAntialias = true
        };
    }

    private static SKPaint BuildTextPaint(RenderStyle style)
    {
        return new SKPaint
        {
            Color = ParseColor(style.StrokeColor),
            TextSize = (float)style.FontSize,
            IsAntialias = true,
            Typeface = style.Bold
                ? SKTypeface.FromFamilyName(style.FontFamily, SKFontStyle.Bold)
                : SKTypeface.FromFamilyName(style.FontFamily)
        };
    }

    private static SKPaint BuildHatchPaint(HatchPattern pattern, RenderStyle style)
    {
        // Build a simple tiled path effect for each hatch type
        using var pathPaint = new SKPaint
        {
            Color = ParseColor(style.StrokeColor),
            StrokeWidth = (float)style.StrokeWidth * 0.5f,
            IsStroke = true
        };

        float tile = 10f;
        using var hatchPath = new SKPath();

        switch (pattern)
        {
            case HatchPattern.ANSI31:
                hatchPath.MoveTo(0, tile); hatchPath.LineTo(tile, 0);
                break;
            case HatchPattern.ANSI37:
                hatchPath.MoveTo(0, tile / 2); hatchPath.LineTo(tile, tile / 2);
                break;
            case HatchPattern.NET:
                hatchPath.MoveTo(tile / 2, 0); hatchPath.LineTo(tile / 2, tile);
                hatchPath.MoveTo(0, tile / 2); hatchPath.LineTo(tile, tile / 2);
                break;
            default:
                hatchPath.MoveTo(0, tile); hatchPath.LineTo(tile, 0);
                break;
        }

        return new SKPaint
        {
            Color = ParseColor(style.StrokeColor),
            StrokeWidth = (float)style.StrokeWidth * 0.5f,
            IsStroke = true,
            PathEffect = SKPathEffect.Create2DPath(
                SKMatrix.CreateScale(tile, tile), hatchPath),
            IsAntialias = true
        };
    }

    private static void DrawArrow(SKCanvas canvas, SKPoint tip, SKPoint away,
                                   float size, SKPaint paint)
    {
        float dx = away.X - tip.X, dy = away.Y - tip.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.1f) return;
        dx /= len; dy /= len;

        float wx = -dy * size * 0.4f, wy = dx * size * 0.4f;

        using var path = new SKPath();
        path.MoveTo(tip.X, tip.Y);
        path.LineTo(tip.X + dx * size + wx, tip.Y + dy * size + wy);
        path.LineTo(tip.X + dx * size - wx, tip.Y + dy * size - wy);
        path.Close();

        using var fill = paint.Clone();
        fill.IsStroke = false;
        canvas.DrawPath(path, fill);
    }

    internal static SKColor ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        return hex.Length == 8
            ? new SKColor(
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                Convert.ToByte(hex[6..8], 16),
                Convert.ToByte(hex[0..2], 16))
            : hex.Length == 6
              ? new SKColor(
                  Convert.ToByte(hex[0..2], 16),
                  Convert.ToByte(hex[2..4], 16),
                  Convert.ToByte(hex[4..6], 16))
              : SKColors.Gray;
    }
}

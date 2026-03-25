using System.Globalization;
using System.Linq;
using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Markup.Services;
using ElectricalComponentSandbox.Rendering;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Dispatches rendering for every <see cref="MarkupRecord"/> type to the
/// <see cref="ICanvas2DRenderer"/>.
/// </summary>
public sealed class MarkupRenderService
{
    /// <summary>
    /// Renders all markups visible on the given layers at the appropriate detail level.
    /// </summary>
    public void RenderAll(ICanvas2DRenderer renderer,
                          IEnumerable<MarkupRecord> markups,
                          IReadOnlyDictionary<string, bool> layerVisibility,
                          DetailLevel detailLevel)
    {
        foreach (var markup in markups)
        {
            if (layerVisibility.TryGetValue(markup.LayerId, out bool visible) && !visible)
                continue;

            Render(renderer, markup, detailLevel);
        }
    }

    /// <summary>
    /// Renders a single <see cref="MarkupRecord"/> at the specified detail level.
    /// </summary>
    public void Render(ICanvas2DRenderer renderer, MarkupRecord markup, DetailLevel detailLevel)
    {
        var style = AppearanceToStyle(markup.Appearance);
        ApplyMarkupStyleOverrides(style, markup);

        if (detailLevel == DetailLevel.Coarse)
        {
            var rep = DetailLevelService.GetRepresentation(markup, DetailLevel.Coarse);
            foreach (var (start, end) in rep.SymbolicLines)
                renderer.DrawLine(start, end, style);
            if (rep.ShowAnnotations && !string.IsNullOrEmpty(markup.TextContent))
                renderer.DrawText(markup.Vertices.FirstOrDefault(), markup.TextContent, style);
            return;
        }

        switch (markup.Type)
        {
            case MarkupType.Polyline:
            case MarkupType.ConduitRun:
                if (markup.Vertices.Count >= 2)
                    renderer.DrawPolyline(markup.Vertices, style);
                break;

            case MarkupType.Polygon:
                if (markup.Vertices.Count >= 3)
                    renderer.DrawPolygon(markup.Vertices, style);
                break;

            case MarkupType.Rectangle:
                if (markup.Vertices.Count >= 2)
                {
                    renderer.DrawRect(new Rect(markup.Vertices[0], markup.Vertices[1]), style);
                }
                else if (markup.BoundingRect != Rect.Empty)
                {
                    renderer.DrawRect(markup.BoundingRect, style);
                }
                break;

            case MarkupType.Circle:
                if (markup.Vertices.Count >= 1)
                    renderer.DrawEllipse(markup.Vertices[0], markup.Radius, markup.Radius, style);
                break;

            case MarkupType.Arc:
                if (markup.Vertices.Count >= 1)
                    renderer.DrawArc(markup.Vertices[0], markup.Radius, markup.Radius,
                        markup.ArcStartDeg, markup.ArcSweepDeg, style);
                break;

            case MarkupType.Text:
                RenderText(renderer, markup, style);
                break;

            case MarkupType.Dimension:
            case MarkupType.Measurement:
                RenderDimension(renderer, markup, style);
                break;

            case MarkupType.Box:
            case MarkupType.Panel:
                RenderComponentOverlay(renderer, markup, style);
                break;

            case MarkupType.Callout:
            case MarkupType.LeaderNote:
                if (markup.Vertices.Count >= 2)
                    renderer.DrawLeader(markup.Vertices, markup.TextContent, style);
                break;

            case MarkupType.RevisionCloud:
                if (markup.Vertices.Count >= 2)
                    renderer.DrawRevisionCloud(markup.Vertices, style);
                break;

            case MarkupType.Stamp:
                RenderStamp(renderer, markup, style);
                break;

            case MarkupType.Hatch:
                RenderHatch(renderer, markup, style);
                break;

            case MarkupType.Hyperlink:
                RenderHyperlink(renderer, markup, style);
                break;
        }
    }

    private static void RenderDimension(ICanvas2DRenderer renderer, MarkupRecord markup, RenderStyle style)
    {
        if (markup.Vertices.Count < 2)
            return;

        string text = string.IsNullOrEmpty(markup.TextContent) ? "<dim>" : markup.TextContent;
        renderer.DrawDimension(
            markup.Vertices[0],
            markup.Vertices[1],
            offset: 0.3,
            text,
            new DimensionStyle
            {
                LineColor = style.StrokeColor,
                TextColor = style.StrokeColor
            });
    }

    private static void RenderComponentOverlay(ICanvas2DRenderer renderer, MarkupRecord markup, RenderStyle style)
    {
        if (markup.BoundingRect != Rect.Empty)
            renderer.DrawRect(markup.BoundingRect, style);
        if (!string.IsNullOrEmpty(markup.TextContent))
            renderer.DrawTextBox(markup.BoundingRect.Location, markup.TextContent, style);
    }

    private static void RenderText(ICanvas2DRenderer renderer, MarkupRecord markup, RenderStyle style)
    {
        if (markup.Vertices.Count < 1 || string.IsNullOrEmpty(markup.TextContent))
            return;

        var align = GetTextAlign(markup);
        var forcePlainText = markup.Metadata.CustomFields.ContainsKey(DrawingAnnotationMarkupService.TextAlignField);
        if (style.FillColor is not null && !forcePlainText)
            renderer.DrawTextBox(markup.Vertices[0], markup.TextContent, style, style.FillColor);
        else
            renderer.DrawText(markup.Vertices[0], markup.TextContent, style, align);
    }

    private static void RenderStamp(ICanvas2DRenderer renderer, MarkupRecord markup, RenderStyle style)
    {
        var rect = markup.BoundingRect;
        if (rect == Rect.Empty && markup.Vertices.Count >= 1)
            rect = new Rect(markup.Vertices[0].X - 60, markup.Vertices[0].Y - 15, 120, 30);
        if (rect == Rect.Empty)
            return;

        renderer.DrawRect(rect, new RenderStyle
        {
            StrokeColor = style.StrokeColor,
            StrokeWidth = Math.Max(1.0, style.StrokeWidth),
            FillColor = style.FillColor ?? "#20FF0000",
            Opacity = style.Opacity
        });

        if (!string.IsNullOrWhiteSpace(markup.TextContent))
        {
            renderer.DrawText(
                new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height * 0.68),
                markup.TextContent,
                new RenderStyle
                {
                    StrokeColor = style.StrokeColor,
                    FontFamily = style.FontFamily,
                    FontSize = style.FontSize,
                    Bold = true,
                    Opacity = style.Opacity
                },
                TextAlign.Center);
        }
    }

    private static void RenderHatch(ICanvas2DRenderer renderer, MarkupRecord markup, RenderStyle style)
    {
        if (markup.Vertices.Count < 3)
            return;

        renderer.DrawHatch(markup.Vertices, ParseHatchPattern(markup.Appearance.HatchPattern), style);
        renderer.DrawPolygon(markup.Vertices, new RenderStyle
        {
            StrokeColor = style.StrokeColor,
            StrokeWidth = Math.Max(0.8, style.StrokeWidth),
            Opacity = style.Opacity
        });
    }

    private static void RenderHyperlink(ICanvas2DRenderer renderer, MarkupRecord markup, RenderStyle style)
    {
        var rect = markup.BoundingRect;
        if (rect == Rect.Empty && markup.Vertices.Count >= 2)
            rect = new Rect(markup.Vertices[0], markup.Vertices[^1]);
        if (rect == Rect.Empty)
            return;

        renderer.DrawRect(rect, new RenderStyle
        {
            StrokeColor = style.StrokeColor,
            StrokeWidth = Math.Max(1.0, style.StrokeWidth),
            DashPattern = new[] { 6f, 3f },
            Opacity = style.Opacity
        });

        if (!string.IsNullOrWhiteSpace(markup.TextContent))
        {
            renderer.DrawText(
                new Point(rect.X + 4.0, rect.Y + 14.0),
                markup.TextContent,
                style,
                TextAlign.Left);
        }
    }

    private static RenderStyle AppearanceToStyle(MarkupAppearance appearance)
    {
        return new RenderStyle
        {
            StrokeColor = appearance.StrokeColor,
            StrokeWidth = appearance.StrokeWidth,
            FillColor = appearance.FillColor == "#00000000" ? null : appearance.FillColor,
            Opacity = appearance.Opacity,
            FontFamily = appearance.FontFamily,
            FontSize = appearance.FontSize,
            DashPattern = ParseDashPattern(appearance.DashArray)
        };
    }

    private static void ApplyMarkupStyleOverrides(RenderStyle style, MarkupRecord markup)
    {
        if (markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.BoldField, out var boldValue) &&
            bool.TryParse(boldValue, out var bold))
        {
            style.Bold = bold;
        }
    }

    private static TextAlign GetTextAlign(MarkupRecord markup)
    {
        if (!markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.TextAlignField, out var alignValue))
            return TextAlign.Left;

        return Enum.TryParse<TextAlign>(alignValue, ignoreCase: true, out var align)
            ? align
            : TextAlign.Left;
    }

    private static float[]? ParseDashPattern(string dashArray)
    {
        if (string.IsNullOrWhiteSpace(dashArray))
            return null;

        var values = new List<float>();
        foreach (var part in dashArray.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0)
                values.Add(value);
        }

        return values.Count > 0 ? values.ToArray() : null;
    }

    private static HatchPattern ParseHatchPattern(string hatchPattern)
    {
        if (string.IsNullOrWhiteSpace(hatchPattern))
            return HatchPattern.Solid;

        return Enum.TryParse<HatchPattern>(hatchPattern, ignoreCase: true, out var pattern)
            ? pattern
            : HatchPattern.Solid;
    }
}

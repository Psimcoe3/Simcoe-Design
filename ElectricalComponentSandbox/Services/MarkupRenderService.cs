using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Markup.Services;
using ElectricalComponentSandbox.Rendering;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Dispatches rendering for every <see cref="MarkupRecord"/> type to the
/// <see cref="ICanvas2DRenderer"/>.  This is the first implementation of
/// markup rendering; previously the data models existed but were never drawn.
///
/// Render order per record:
///   1. Fill (if any)
///   2. Stroke outline
///   3. Dimension / text overlays
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

        // At coarse detail level use fast symbolic lines from DetailLevelService
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
                    var tl = markup.Vertices[0];
                    var br = markup.Vertices[1];
                    renderer.DrawRect(new Rect(tl, br), style);
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

            case MarkupType.Text:
                if (markup.Vertices.Count >= 1 && !string.IsNullOrEmpty(markup.TextContent))
                    renderer.DrawTextBox(markup.Vertices[0], markup.TextContent, style);
                break;

            case MarkupType.Dimension:
                RenderDimension(renderer, markup, style);
                break;

            case MarkupType.Box:
            case MarkupType.Panel:
                RenderComponentOverlay(renderer, markup, style);
                break;
        }
    }

    // ── Specialised renderers ─────────────────────────────────────────────────

    private static void RenderDimension(ICanvas2DRenderer renderer, MarkupRecord markup, RenderStyle style)
    {
        if (markup.Vertices.Count < 2) return;
        string text = string.IsNullOrEmpty(markup.TextContent) ? "<dim>" : markup.TextContent;
        renderer.DrawDimension(
            markup.Vertices[0], markup.Vertices[1],
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RenderStyle AppearanceToStyle(MarkupAppearance appearance)
    {
        return new RenderStyle
        {
            StrokeColor = appearance.StrokeColor,
            StrokeWidth = appearance.StrokeWidth,
            FillColor = appearance.FillColor == "#00000000" ? null : appearance.FillColor,
            Opacity = appearance.Opacity,
            FontFamily = appearance.FontFamily,
            FontSize = appearance.FontSize
        };
    }
}

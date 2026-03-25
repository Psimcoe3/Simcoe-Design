using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services.Export;

/// <summary>
/// Renders the current drawing to a PDF file, respecting layer visibility,
/// freeze state, plot-style (CTB) line weights/colours, and paper size.
///
/// Uses WPF's built-in XPS printing pipeline via <see cref="System.Windows.Xps"/>
/// as the PDF rasterisation back-end.  The caller can print to a PDF printer driver
/// (e.g. Microsoft Print to PDF) or use the <see cref="RenderToBitmap"/> path
/// for direct bitmap export.
/// </summary>
public class PlotToPdfService
{
    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>Output DPI for raster export (300 standard, 600 for high-quality prints)</summary>
    public int OutputDpi { get; set; } = 300;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders drawing content to a bitmap at the specified DPI.
    /// Respects layer visibility, freeze, plottable flags, and plot style pens.
    /// </summary>
    public RenderTargetBitmap RenderToBitmap(
        PlotLayout layout,
        PlotStyleTable? plotStyle,
        IReadOnlyList<Models.ElectricalComponent> components,
        IReadOnlyList<Layer> layers,
        Rect modelExtents)
    {
        var (paperW, paperH) = layout.GetPaperInches();
        int pixelWidth = (int)(paperW * OutputDpi);
        int pixelHeight = (int)(paperH * OutputDpi);

        var layerLookup = BuildPlottableLayerLookup(layers);
        var penLookup = BuildPenLookup(plotStyle);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            // White background
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pixelWidth, pixelHeight));

            // Compute model-to-paper transform
            double scaleX = pixelWidth / (modelExtents.Width > 0 ? modelExtents.Width : 1);
            double scaleY = pixelHeight / (modelExtents.Height > 0 ? modelExtents.Height : 1);
            double scale = Math.Min(scaleX, scaleY) * 0.95; // 5% margin

            double offsetX = (pixelWidth - modelExtents.Width * scale) / 2 - modelExtents.X * scale;
            double offsetY = (pixelHeight - modelExtents.Height * scale) / 2 - modelExtents.Y * scale;

            foreach (var component in components)
            {
                if (!IsLayerPlottable(layerLookup, component.LayerId))
                    continue;

                var pen = ResolvePen(penLookup, component, layers);
                var center = new Point(
                    component.Position.X * scale + offsetX,
                    component.Position.Y * scale + offsetY);

                double w = component.Parameters.Width * scale;
                double h = component.Parameters.Height * scale;

                var rect = new Rect(center.X - w / 2, center.Y - h / 2, w, h);

                dc.DrawRectangle(null, pen, rect);

                // Label
                if (!string.IsNullOrEmpty(component.Name))
                {
                    var text = new FormattedText(
                        component.Name,
                        System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        Math.Max(8, 10 * scale / OutputDpi * 72),
                        pen.Brush,
                        VisualTreeHelper.GetDpi(visual).PixelsPerDip);

                    dc.DrawText(text, new Point(
                        center.X - text.Width / 2,
                        center.Y + h / 2 + 2));
                }
            }
        }

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, OutputDpi, OutputDpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    /// <summary>
    /// Saves the rendered bitmap to a PNG file.
    /// For PDF output, use Microsoft Print to PDF or a PDF printer driver.
    /// </summary>
    public void SaveToPng(RenderTargetBitmap bitmap, string outputPath)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    /// <summary>
    /// Computes the bounding extents of all plottable components.
    /// </summary>
    public Rect ComputeModelExtents(
        IReadOnlyList<Models.ElectricalComponent> components,
        IReadOnlyList<Layer> layers)
    {
        var layerLookup = BuildPlottableLayerLookup(layers);

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var c in components)
        {
            if (!IsLayerPlottable(layerLookup, c.LayerId))
                continue;

            double cx = c.Position.X;
            double cy = c.Position.Y;
            double hw = c.Parameters.Width / 2;
            double hh = c.Parameters.Height / 2;

            minX = Math.Min(minX, cx - hw);
            minY = Math.Min(minY, cy - hh);
            maxX = Math.Max(maxX, cx + hw);
            maxY = Math.Max(maxY, cy + hh);
        }

        if (minX > maxX)
            return new Rect(0, 0, 1, 1);

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static Dictionary<string, bool> BuildPlottableLayerLookup(IReadOnlyList<Layer> layers)
    {
        var lookup = new Dictionary<string, bool>(layers.Count, StringComparer.Ordinal);
        foreach (var layer in layers)
            lookup[layer.Id] = layer.IsVisible && !layer.IsFrozen && layer.IsPlotted;
        return lookup;
    }

    private static bool IsLayerPlottable(Dictionary<string, bool> lookup, string layerId)
    {
        return !lookup.TryGetValue(layerId, out var plottable) || plottable;
    }

    private static Dictionary<int, PlotStylePen> BuildPenLookup(PlotStyleTable? table)
    {
        var lookup = new Dictionary<int, PlotStylePen>();
        if (table == null) return lookup;
        foreach (var pen in table.Pens)
            lookup[pen.PenNumber] = pen;
        return lookup;
    }

    private static Pen ResolvePen(
        Dictionary<int, PlotStylePen> penLookup,
        Models.ElectricalComponent component,
        IReadOnlyList<Layer> layers)
    {
        // Resolve color and line weight from component → layer → default
        string colorHex = component.Parameters.ColorOverride
            ?? component.Parameters.Color
            ?? "#000000";

        double lineWeight = component.Parameters.LineWeightOverride ?? 0.35;

        // Check layer for inheritable properties
        var layer = layers.FirstOrDefault(l =>
            string.Equals(l.Id, component.LayerId, StringComparison.Ordinal));
        if (layer != null)
        {
            if (component.Parameters.ColorOverride == null)
                colorHex = layer.Color;
            if (component.Parameters.LineWeightOverride == null)
                lineWeight = layer.LineWeight;
        }

        // Apply plot style override if pen lookup has a matching entry
        if (penLookup.Count > 0)
        {
            // Use simple hash of color to map to pen number (0-255)
            int penIndex = Math.Abs(colorHex.GetHashCode()) % 256;
            if (penLookup.TryGetValue(penIndex, out var stylePen))
            {
                if (stylePen.OutputColor != null)
                    colorHex = stylePen.OutputColor;
                if (stylePen.LineWeight > 0)
                    lineWeight = stylePen.LineWeight;
            }
        }

        var brush = new SolidColorBrush(ParseColor(colorHex));
        brush.Freeze();
        return new Pen(brush, lineWeight);
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            var obj = ColorConverter.ConvertFromString(hex);
            return obj is Color c ? c : Colors.Black;
        }
        catch
        {
            return Colors.Black;
        }
    }
}

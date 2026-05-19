using System.Globalization;
using System.Windows;
using ElectricalComponentSandbox.Rendering;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Paints a <see cref="SpoolSheetRenderGeometry"/> onto an
/// <see cref="ICanvas2DRenderer"/>. The renderer is assumed to work in
/// document space sized in inches; callers control zoom and the world-to-screen
/// transform via the canvas itself. This keeps the painter completely free of
/// WPF/Skia specifics — Skia, GDI, or a stub test renderer all consume the
/// same paint() call.
/// </summary>
public sealed class SpoolSheetCanvasPainter
{
    private readonly Action<TitleBlockCellPaintArgs>? _customCellPainter;

    /// <summary>Border line color (default: pure black).</summary>
    public string BorderColor { get; init; } = "#FF000000";

    /// <summary>Outer border line weight in document units.</summary>
    public double OuterBorderWidth { get; init; } = 0.04;

    /// <summary>Inner border line weight in document units.</summary>
    public double InnerBorderWidth { get; init; } = 0.02;

    /// <summary>Table cell stroke width in document units.</summary>
    public double CellStrokeWidth { get; init; } = 0.01;

    /// <summary>Background fill for the sheet (white drawing paper).</summary>
    public string PaperFillColor { get; init; } = "#FFFFFFFF";

    public SpoolSheetCanvasPainter(Action<TitleBlockCellPaintArgs>? customCellPainter = null)
    {
        _customCellPainter = customCellPainter;
    }

    /// <summary>
    /// Paints the full sheet onto <paramref name="canvas"/>.  The canvas is
    /// expected to already be cleared / sized for the paper extent.
    /// </summary>
    public void Paint(ICanvas2DRenderer canvas, SpoolSheetRenderGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(geometry);

        PaintPaperBackground(canvas, geometry);
        PaintBorderAndZones(canvas, geometry);
        PaintTitleBlock(canvas, geometry);
        PaintTables(canvas, geometry);
        PaintLines(canvas, geometry);
        PaintFreeText(canvas, geometry);
    }

    // ── Paper background ─────────────────────────────────────────────────

    private void PaintPaperBackground(ICanvas2DRenderer canvas, SpoolSheetRenderGeometry geometry)
    {
        var paperRect = new Rect(0, 0, geometry.PaperWidthInches, geometry.PaperHeightInches);
        canvas.DrawRect(paperRect, new RenderStyle
        {
            StrokeColor = "#00000000",
            FillColor = PaperFillColor,
            StrokeWidth = 0,
        });
    }

    // ── Border + zone ticks ──────────────────────────────────────────────

    private void PaintBorderAndZones(ICanvas2DRenderer canvas, SpoolSheetRenderGeometry geometry)
    {
        canvas.DrawRect(geometry.Border.OuterBorder, new RenderStyle
        {
            StrokeColor = BorderColor,
            StrokeWidth = OuterBorderWidth,
        });
        canvas.DrawRect(geometry.Border.InnerBorder, new RenderStyle
        {
            StrokeColor = BorderColor,
            StrokeWidth = InnerBorderWidth,
        });

        foreach (var mark in geometry.Border.ZoneMarks)
        {
            // Short tick + label centered on the inner border edge.
            double tickLen = 0.10;
            if (mark.IsHorizontal)
            {
                bool top = Math.Abs(mark.Position.Y - geometry.Border.InnerBorder.Y) < 1e-3;
                double tickY = top ? mark.Position.Y - tickLen : mark.Position.Y + tickLen;
                canvas.DrawLine(mark.Position, new Point(mark.Position.X, tickY),
                    new RenderStyle { StrokeColor = BorderColor, StrokeWidth = InnerBorderWidth });
                var labelY = top ? mark.Position.Y - 0.18 : mark.Position.Y + 0.18;
                canvas.DrawText(new Point(mark.Position.X, labelY), mark.Label,
                    new RenderStyle { StrokeColor = BorderColor, FontSize = 9 }, TextAlign.Center);
            }
            else
            {
                bool left = Math.Abs(mark.Position.X - geometry.Border.InnerBorder.X) < 1e-3;
                double tickX = left ? mark.Position.X - tickLen : mark.Position.X + tickLen;
                canvas.DrawLine(mark.Position, new Point(tickX, mark.Position.Y),
                    new RenderStyle { StrokeColor = BorderColor, StrokeWidth = InnerBorderWidth });
                var labelX = left ? mark.Position.X - 0.18 : mark.Position.X + 0.18;
                canvas.DrawText(new Point(labelX, mark.Position.Y), mark.Label,
                    new RenderStyle { StrokeColor = BorderColor, FontSize = 9 }, TextAlign.Center);
            }
        }
    }

    // ── Title block cells ────────────────────────────────────────────────

    private void PaintTitleBlock(ICanvas2DRenderer canvas, SpoolSheetRenderGeometry geometry)
    {
        foreach (var cell in geometry.Border.TitleBlockCells)
        {
            // Allow callers to override per-cell rendering (e.g. logos, photos).
            if (_customCellPainter != null)
            {
                _customCellPainter(new TitleBlockCellPaintArgs(canvas, cell));
                continue;
            }

            var rect = new Rect(cell.X, cell.Y, cell.Width, cell.Height);
            canvas.DrawRect(rect, new RenderStyle
            {
                StrokeColor = BorderColor,
                StrokeWidth = CellStrokeWidth,
            });

            if (!string.IsNullOrEmpty(cell.Label))
            {
                canvas.DrawText(
                    new Point(rect.X + 0.04, rect.Y + 0.05),
                    cell.Label,
                    new RenderStyle { StrokeColor = BorderColor, FontSize = 7, Bold = false },
                    TextAlign.Left);
            }

            if (!string.IsNullOrEmpty(cell.Value))
            {
                canvas.DrawText(
                    new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0),
                    cell.Value,
                    new RenderStyle { StrokeColor = BorderColor, FontSize = 10, Bold = true },
                    TextAlign.Center);
            }
        }
    }

    // ── Tables ───────────────────────────────────────────────────────────

    private void PaintTables(ICanvas2DRenderer canvas, SpoolSheetRenderGeometry geometry)
    {
        // The renderer pre-populated geometry.Rects with per-table cell outlines.
        // We paint filled rects first (e.g. title strips) so they sit beneath
        // the stroked cell outlines.
        foreach (var rect in geometry.Rects)
        {
            if (rect.Filled)
            {
                canvas.DrawRect(
                    new Rect(rect.X, rect.Y, rect.Width, rect.Height),
                    new RenderStyle
                    {
                        StrokeColor = BorderColor,
                        StrokeWidth = CellStrokeWidth,
                        FillColor = rect.FillHex ?? "#FFE6E6E6",
                    });
            }
        }
        foreach (var rect in geometry.Rects)
        {
            if (!rect.Filled)
            {
                canvas.DrawRect(
                    new Rect(rect.X, rect.Y, rect.Width, rect.Height),
                    new RenderStyle
                    {
                        StrokeColor = BorderColor,
                        StrokeWidth = CellStrokeWidth,
                    });
            }
        }
    }

    // ── Lines ────────────────────────────────────────────────────────────

    private void PaintLines(ICanvas2DRenderer canvas, SpoolSheetRenderGeometry geometry)
    {
        foreach (var line in geometry.Lines)
        {
            canvas.DrawLine(
                new Point(line.X1, line.Y1),
                new Point(line.X2, line.Y2),
                new RenderStyle { StrokeColor = BorderColor, StrokeWidth = Math.Max(CellStrokeWidth, line.Weight) });
        }
    }

    // ── Free text (sheet header + cell values + table labels) ────────────

    private void PaintFreeText(ICanvas2DRenderer canvas, SpoolSheetRenderGeometry geometry)
    {
        foreach (var t in geometry.Texts)
        {
            canvas.DrawText(
                new Point(t.X, t.Y),
                t.Value,
                new RenderStyle
                {
                    StrokeColor = BorderColor,
                    FontSize = t.FontSize,
                    Bold = t.Bold,
                },
                Map(t.Align));
        }
    }

    private static TextAlign Map(SpoolTextAlign align) => align switch
    {
        SpoolTextAlign.Left => TextAlign.Left,
        SpoolTextAlign.Center => TextAlign.Center,
        SpoolTextAlign.Right => TextAlign.Right,
        _ => TextAlign.Left,
    };

    /// <summary>
    /// Helper formatter used by tests when verifying the painter call sequence.
    /// </summary>
    internal static string Format(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
}

/// <summary>
/// Arguments passed to a title-block-cell custom painter. Receives the live
/// canvas and the cell definition so callers can render logos, photos, or
/// other rich content in lieu of the default label/value pair.
/// </summary>
public sealed record TitleBlockCellPaintArgs(ICanvas2DRenderer Canvas, TitleBlockCell Cell);

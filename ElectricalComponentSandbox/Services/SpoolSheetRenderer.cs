using System.Globalization;
using System.Windows;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Horizontal text alignment for a rendered cell value.
/// </summary>
public enum SpoolTextAlign
{
    Left,
    Center,
    Right
}

/// <summary>
/// A text run positioned in sheet space (inches from sheet origin).
/// </summary>
public sealed record SpoolText(
    string Value,
    double X,
    double Y,
    double FontSize,
    SpoolTextAlign Align,
    bool Bold);

/// <summary>
/// A rectangle drawn on the sheet (table cells, borders, headers).
/// </summary>
public sealed record SpoolRect(
    double X,
    double Y,
    double Width,
    double Height,
    bool Filled,
    string? FillHex = null);

/// <summary>
/// A line segment drawn on the sheet (dividers, leader lines).
/// </summary>
public sealed record SpoolLine(
    double X1,
    double Y1,
    double X2,
    double Y2,
    double Weight);

/// <summary>
/// A rendered table block with header row plus data rows. The renderer
/// expresses tables as cells so downstream painters do not need to
/// re-implement column-fitting logic.
/// </summary>
public sealed class SpoolTable
{
    public required string Title { get; init; }
    public required Rect Bounds { get; init; }
    public required IReadOnlyList<string> Headers { get; init; }
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
}

/// <summary>
/// Complete geometry for a rendered spool sheet. Layout coordinates are in
/// inches from the top-left corner of the 11x17 sheet; downstream painters
/// can convert to DIPs, points, or pixels using the canvas DPI.
/// </summary>
public sealed class SpoolSheetRenderGeometry
{
    public double PaperWidthInches { get; init; }
    public double PaperHeightInches { get; init; }
    public TitleBlockBorderGeometry Border { get; init; } = new();
    public List<SpoolRect> Rects { get; } = new();
    public List<SpoolLine> Lines { get; } = new();
    public List<SpoolText> Texts { get; } = new();
    public List<SpoolTable> Tables { get; } = new();
}

/// <summary>
/// Lays out a <see cref="SpoolSheet"/> on an ANSI B (11x17) page using the
/// existing <see cref="TitleBlockService"/> for the border + title block, then
/// stacking the bend / cut / hanger / BOM tables in the drawing area. The
/// output is render-agnostic geometry: WPF, PDF, or PNG paths can consume it.
/// </summary>
public sealed class SpoolSheetRenderer
{
    private readonly TitleBlockService _titleBlockService;

    /// <summary>Paper margin (inches) inside the inner border for table layout.</summary>
    public double Padding { get; init; } = 0.25;

    /// <summary>Gap (inches) between consecutive stacked tables.</summary>
    public double TableGap { get; init; } = 0.15;

    /// <summary>Default header row height (inches).</summary>
    public double HeaderRowHeight { get; init; } = 0.25;

    /// <summary>Default data row height (inches).</summary>
    public double DataRowHeight { get; init; } = 0.18;

    public SpoolSheetRenderer(TitleBlockService? titleBlockService = null)
    {
        _titleBlockService = titleBlockService ?? new TitleBlockService();
    }

    /// <summary>
    /// Renders the spool sheet to a geometry bag using ANSI B (11x17) paper.
    /// </summary>
    public SpoolSheetRenderGeometry Render(SpoolSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return Render(sheet, PaperSizeType.ANSI_B);
    }

    /// <summary>
    /// Renders the spool sheet at the requested paper size. The renderer is
    /// validated against 11x17 (spool sheet default) and 24x36 (Arch D plan).
    /// </summary>
    public SpoolSheetRenderGeometry Render(SpoolSheet sheet, PaperSizeType paperSize)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var template = BuildTitleBlockTemplate(sheet, paperSize);
        var border = _titleBlockService.GenerateBorderGeometry(template);

        var geometry = new SpoolSheetRenderGeometry
        {
            PaperWidthInches = template.PaperWidth,
            PaperHeightInches = template.PaperHeight,
            Border = border,
        };

        // Drawing area = inner border above the title block, inset by padding.
        var inner = border.InnerBorder;
        var titleBlock = border.TitleBlockRect;
        var drawingArea = new Rect(
            inner.X + Padding,
            inner.Y + Padding,
            inner.Width - 2 * Padding,
            titleBlock.Y - inner.Y - 2 * Padding);
        geometry.Rects.Add(new SpoolRect(drawingArea.X, drawingArea.Y, drawingArea.Width, drawingArea.Height, Filled: false));

        // Sheet header
        double cursorY = drawingArea.Y + 0.05;
        geometry.Texts.Add(new SpoolText(
            $"SPOOL — {sheet.RunId}    {sheet.Template}",
            drawingArea.X + drawingArea.Width / 2.0,
            cursorY,
            FontSize: 14,
            Align: SpoolTextAlign.Center,
            Bold: true));
        cursorY += 0.30;

        string summary = string.Format(
            CultureInfo.InvariantCulture,
            "{0}\"  {1}    Gross: {2:F2} ft    Adjusted: {3:F2} ft",
            sheet.TradeSize, sheet.Material, sheet.GrossLengthFeet, sheet.AdjustedLengthFeet);
        geometry.Texts.Add(new SpoolText(
            summary,
            drawingArea.X + drawingArea.Width / 2.0,
            cursorY,
            FontSize: 10,
            Align: SpoolTextAlign.Center,
            Bold: false));
        cursorY += 0.25;

        // Stack the tables. Width spans the drawing area; layout flows top→down.
        cursorY = LayoutTable(
            geometry,
            new Rect(drawingArea.X, cursorY, drawingArea.Width, drawingArea.Bottom - cursorY),
            BuildBendScheduleTable(sheet));

        cursorY = LayoutTable(
            geometry,
            new Rect(drawingArea.X, cursorY + TableGap, drawingArea.Width, drawingArea.Bottom - cursorY - TableGap),
            BuildCutListTable(sheet));

        if (sheet.HangerSchedule.Count > 0)
        {
            cursorY = LayoutTable(
                geometry,
                new Rect(drawingArea.X, cursorY + TableGap, drawingArea.Width, drawingArea.Bottom - cursorY - TableGap),
                BuildHangerScheduleTable(sheet));
        }

        if (sheet.TrapezeBom.Lines.Count > 0)
        {
            cursorY = LayoutTable(
                geometry,
                new Rect(drawingArea.X, cursorY + TableGap, drawingArea.Width, drawingArea.Bottom - cursorY - TableGap),
                BuildTrapezeBomTable(sheet));
        }

        if (sheet.ConduitBom.Count > 0)
        {
            cursorY = LayoutTable(
                geometry,
                new Rect(drawingArea.X, cursorY + TableGap, drawingArea.Width, drawingArea.Bottom - cursorY - TableGap),
                BuildConduitBomTable(sheet));
        }

        return geometry;
    }

    // ── Title block ──────────────────────────────────────────────────────

    private static TitleBlockTemplate BuildTitleBlockTemplate(SpoolSheet sheet, PaperSizeType paperSize)
    {
        return new TitleBlockTemplate
        {
            Name = $"SMC Spool — {sheet.Template}",
            PaperSize = paperSize,
            BorderMargin = 0.4,
            TitleBlockHeight = 1.5,
            RevisionHistoryRows = 5,
            CompanyName = "SMC",
            ProjectName = sheet.TitleBlock.ProjectName,
            DrawingNumber = sheet.TitleBlock.SheetNumber,
            SheetNumber = string.IsNullOrEmpty(sheet.TitleBlock.SpoolPackage) ? "1 OF 1" : sheet.TitleBlock.SpoolPackage,
            Status = sheet.TitleBlock.Status,
            Description = sheet.TitleBlock.SheetTitle,
            DrawnBy = sheet.TitleBlock.DrawnBy,
            Date = sheet.TitleBlock.DrawnDateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Scale = sheet.TitleBlock.DrawingScale,
        };
    }

    // ── Table builders ───────────────────────────────────────────────────

    private static SpoolTable BuildBendScheduleTable(SpoolSheet sheet)
    {
        var headers = new[] { "#", "Pattern", "Ang1°", "Ang2°", "Size", "Deduct", "Mark1", "Mark2", "Mark3", "Mark4", "DimA", "DimB", "DimC", "DimD", "Bender", "Notes" };
        var rows = sheet.BendSchedule.Bends
            .Select(b => (IReadOnlyList<string>)new[]
            {
                b.BendNumber.ToString(CultureInfo.InvariantCulture),
                b.Pattern.ToString(),
                F(b.Angle1Degrees),
                F(b.Angle2Degrees),
                b.TradeSize,
                F(b.DeductInches),
                F(b.Mark1Inches),
                F(b.Mark2Inches),
                F(b.Mark3Inches),
                F(b.Mark4Inches),
                F(b.DimAInches),
                F(b.DimBInches),
                F(b.DimCInches),
                F(b.DimDInches),
                b.BenderType,
                b.Notes,
            })
            .ToList();
        return new SpoolTable
        {
            Title = "BEND SCHEDULE",
            Bounds = Rect.Empty,
            Headers = headers,
            Rows = rows,
        };
    }

    private static SpoolTable BuildCutListTable(SpoolSheet sheet)
    {
        var headers = new[] { "Item", "Segment", "Size", "Material", "Gross (in)", "Cut (in)", "Notes" };
        var rows = sheet.CutList
            .Select(c => (IReadOnlyList<string>)new[]
            {
                c.Item.ToString(CultureInfo.InvariantCulture),
                ShortenId(c.SegmentId),
                c.TradeSize,
                c.Material.ToString(),
                F(c.GrossLengthInches),
                F(c.CutLengthInches),
                c.Notes,
            })
            .ToList();
        return new SpoolTable
        {
            Title = "CUT LIST",
            Bounds = Rect.Empty,
            Headers = headers,
            Rows = rows,
        };
    }

    private static SpoolTable BuildHangerScheduleTable(SpoolSheet sheet)
    {
        var headers = new[] { "Item", "Hanger", "Tiers", "Strut", "Rod", "Conduits", "Rod Length (in)" };
        var rows = sheet.HangerSchedule
            .Select(h => (IReadOnlyList<string>)new[]
            {
                h.Item.ToString(CultureInfo.InvariantCulture),
                ShortenId(h.HangerId),
                h.TierCount.ToString(CultureInfo.InvariantCulture),
                h.StrutDescription,
                h.RodDescription,
                h.ConduitCount.ToString(CultureInfo.InvariantCulture),
                F(h.TotalRodLengthInches),
            })
            .ToList();
        return new SpoolTable
        {
            Title = "HANGER SCHEDULE",
            Bounds = Rect.Empty,
            Headers = headers,
            Rows = rows,
        };
    }

    private static SpoolTable BuildTrapezeBomTable(SpoolSheet sheet)
    {
        var headers = new[] { "Code", "Description", "Qty", "Unit", "Total Length (in)" };
        var rows = sheet.TrapezeBom.Lines
            .Select(l => (IReadOnlyList<string>)new[]
            {
                l.ItemCode,
                l.Description,
                l.Quantity.ToString(CultureInfo.InvariantCulture),
                l.Unit,
                F(l.TotalLengthInches),
            })
            .ToList();
        return new SpoolTable
        {
            Title = "TRAPEZE BOM",
            Bounds = Rect.Empty,
            Headers = headers,
            Rows = rows,
        };
    }

    private static SpoolTable BuildConduitBomTable(SpoolSheet sheet)
    {
        var headers = new[] { "Type", "Description", "Size", "Material", "Qty", "Total Length (in)" };
        var rows = sheet.ConduitBom
            .Select(e => (IReadOnlyList<string>)new[]
            {
                e.ItemType,
                e.Description,
                e.TradeSize,
                e.Material,
                e.Quantity.ToString(CultureInfo.InvariantCulture),
                F(e.TotalLengthInches),
            })
            .ToList();
        return new SpoolTable
        {
            Title = "CONDUIT BOM",
            Bounds = Rect.Empty,
            Headers = headers,
            Rows = rows,
        };
    }

    // ── Table layout ─────────────────────────────────────────────────────

    private double LayoutTable(SpoolSheetRenderGeometry geom, Rect available, SpoolTable table)
    {
        if (available.Height <= HeaderRowHeight + DataRowHeight)
        {
            // Out of room — emit a stub indicating overflow.
            geom.Texts.Add(new SpoolText(
                $"{table.Title}  — content truncated, see CSV export",
                available.X,
                available.Y,
                FontSize: 9,
                Align: SpoolTextAlign.Left,
                Bold: false));
            return available.Y + DataRowHeight;
        }

        // Title strip
        double titleHeight = 0.22;
        geom.Rects.Add(new SpoolRect(
            available.X, available.Y, available.Width, titleHeight,
            Filled: true, FillHex: "#E6E6E6"));
        geom.Texts.Add(new SpoolText(
            table.Title,
            available.X + 0.08,
            available.Y + titleHeight / 2.0,
            FontSize: 10,
            Align: SpoolTextAlign.Left,
            Bold: true));

        // Column layout — equal-width columns across the available width.
        int cols = table.Headers.Count;
        double colWidth = cols > 0 ? available.Width / cols : available.Width;

        // Determine how many data rows we can fit beneath the header.
        double rowsAreaTop = available.Y + titleHeight;
        double rowsAreaBottom = available.Y + available.Height;
        double headerY = rowsAreaTop;
        double dataStartY = headerY + HeaderRowHeight;
        int maxRows = Math.Max(0, (int)Math.Floor((rowsAreaBottom - dataStartY) / DataRowHeight));
        int rowsToRender = Math.Min(maxRows, table.Rows.Count);

        // Header cells
        for (int c = 0; c < cols; c++)
        {
            double x = available.X + c * colWidth;
            geom.Rects.Add(new SpoolRect(x, headerY, colWidth, HeaderRowHeight, Filled: false));
            geom.Texts.Add(new SpoolText(
                table.Headers[c],
                x + colWidth / 2.0,
                headerY + HeaderRowHeight / 2.0,
                FontSize: 9,
                Align: SpoolTextAlign.Center,
                Bold: true));
        }

        // Data cells
        for (int r = 0; r < rowsToRender; r++)
        {
            double y = dataStartY + r * DataRowHeight;
            var row = table.Rows[r];
            for (int c = 0; c < cols; c++)
            {
                double x = available.X + c * colWidth;
                geom.Rects.Add(new SpoolRect(x, y, colWidth, DataRowHeight, Filled: false));
                string value = c < row.Count ? row[c] : string.Empty;
                geom.Texts.Add(new SpoolText(
                    value,
                    x + 0.04,
                    y + DataRowHeight / 2.0,
                    FontSize: 8,
                    Align: SpoolTextAlign.Left,
                    Bold: false));
            }
        }

        int overflow = table.Rows.Count - rowsToRender;
        if (overflow > 0)
        {
            double y = dataStartY + rowsToRender * DataRowHeight;
            geom.Texts.Add(new SpoolText(
                $"… {overflow} more row(s) — see CSV",
                available.X + 0.04,
                y + DataRowHeight / 2.0,
                FontSize: 8,
                Align: SpoolTextAlign.Left,
                Bold: false));
        }

        double consumedRows = rowsToRender + (overflow > 0 ? 1 : 0);
        var bounds = new Rect(available.X, available.Y, available.Width,
            titleHeight + HeaderRowHeight + consumedRows * DataRowHeight);
        table.GetType(); // suppress unused capture warning; bounds are exposed via the returned record
        geom.Tables.Add(new SpoolTable
        {
            Title = table.Title,
            Bounds = bounds,
            Headers = table.Headers,
            Rows = table.Rows,
        });

        return bounds.Bottom;
    }

    private static string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);

    private static string ShortenId(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        return id.Length <= 8 ? id : id[..8];
    }
}

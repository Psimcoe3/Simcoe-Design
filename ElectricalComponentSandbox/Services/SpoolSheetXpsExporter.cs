using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Xps.Packaging;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Exports a <see cref="SpoolSheetRenderGeometry"/> to a printable WPF
/// <see cref="FixedDocument"/> or an XPS file on disk.  No external
/// dependencies — XPS is the native Windows print format and the system
/// "Microsoft Print to PDF" driver converts it to PDF directly.
/// </summary>
public sealed class SpoolSheetXpsExporter
{
    /// <summary>WPF device-independent pixels per inch (96 DIP/in is the WPF standard).</summary>
    public const double DipPerInch = 96.0;

    /// <summary>Border color (default black).</summary>
    public Brush BorderBrush { get; init; } = Brushes.Black;

    /// <summary>Sheet paper background color.</summary>
    public Brush PaperBrush { get; init; } = Brushes.White;

    /// <summary>Default font family for sheet text.</summary>
    public string FontFamily { get; init; } = "Segoe UI";

    /// <summary>
    /// Builds a single-page <see cref="FixedDocument"/> representing the
    /// rendered sheet geometry. The document is suitable for
    /// <see cref="System.Windows.Controls.PrintDialog.PrintDocument"/>
    /// or for serializing to XPS.
    /// </summary>
    public FixedDocument BuildFixedDocument(SpoolSheetRenderGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var document = new FixedDocument();
        var pageSize = new Size(
            geometry.PaperWidthInches * DipPerInch,
            geometry.PaperHeightInches * DipPerInch);
        document.DocumentPaginator.PageSize = pageSize;

        var pageContent = new PageContent();
        var fixedPage = new FixedPage
        {
            Width = pageSize.Width,
            Height = pageSize.Height,
            Background = PaperBrush,
        };

        PaintBorder(fixedPage, geometry);
        PaintTitleBlock(fixedPage, geometry);
        PaintRects(fixedPage, geometry);
        PaintLines(fixedPage, geometry);
        PaintTexts(fixedPage, geometry);

        ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
        document.Pages.Add(pageContent);
        return document;
    }

    /// <summary>
    /// Writes the rendered sheet to an XPS file at <paramref name="path"/>.
    /// The XPS document opens in the Windows XPS Viewer and can be
    /// converted to PDF via the system "Microsoft Print to PDF" driver.
    /// </summary>
    public void SaveToFile(SpoolSheetRenderGeometry geometry, string path)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (File.Exists(path)) File.Delete(path);

        var document = BuildFixedDocument(geometry);
        using var package = Package.Open(path, FileMode.Create, FileAccess.ReadWrite);
        using var xps = new XpsDocument(package);
        var writer = XpsDocument.CreateXpsDocumentWriter(xps);
        writer.Write(document);
    }

    // ── Painting helpers ─────────────────────────────────────────────────

    private void PaintBorder(FixedPage page, SpoolSheetRenderGeometry geometry)
    {
        AddRectangle(page, geometry.Border.OuterBorder, strokeThicknessDip: 1.5);
        AddRectangle(page, geometry.Border.InnerBorder, strokeThicknessDip: 1.0);

        foreach (var mark in geometry.Border.ZoneMarks)
        {
            double tickLen = 0.10 * DipPerInch;
            if (mark.IsHorizontal)
            {
                bool top = Math.Abs(mark.Position.Y - geometry.Border.InnerBorder.Y) < 1e-3;
                double tickY = top ? mark.Position.Y * DipPerInch - tickLen : mark.Position.Y * DipPerInch + tickLen;
                AddLine(page,
                    new Point(mark.Position.X * DipPerInch, mark.Position.Y * DipPerInch),
                    new Point(mark.Position.X * DipPerInch, tickY),
                    0.5);
                double labelY = top
                    ? mark.Position.Y * DipPerInch - 0.20 * DipPerInch
                    : mark.Position.Y * DipPerInch + 0.10 * DipPerInch;
                AddText(page, mark.Label,
                    mark.Position.X * DipPerInch, labelY,
                    fontSize: 8, bold: false,
                    horizontal: HorizontalAlignment.Center);
            }
            else
            {
                bool left = Math.Abs(mark.Position.X - geometry.Border.InnerBorder.X) < 1e-3;
                double tickX = left
                    ? mark.Position.X * DipPerInch - tickLen
                    : mark.Position.X * DipPerInch + tickLen;
                AddLine(page,
                    new Point(mark.Position.X * DipPerInch, mark.Position.Y * DipPerInch),
                    new Point(tickX, mark.Position.Y * DipPerInch),
                    0.5);
                double labelX = left
                    ? mark.Position.X * DipPerInch - 0.20 * DipPerInch
                    : mark.Position.X * DipPerInch + 0.10 * DipPerInch;
                AddText(page, mark.Label,
                    labelX, mark.Position.Y * DipPerInch - 6,
                    fontSize: 8, bold: false,
                    horizontal: HorizontalAlignment.Center);
            }
        }
    }

    private void PaintTitleBlock(FixedPage page, SpoolSheetRenderGeometry geometry)
    {
        foreach (var cell in geometry.Border.TitleBlockCells)
        {
            var rect = new Rect(
                cell.X * DipPerInch,
                cell.Y * DipPerInch,
                cell.Width * DipPerInch,
                cell.Height * DipPerInch);
            AddRectangle(page, rect, strokeThicknessDip: 0.5);

            if (!string.IsNullOrEmpty(cell.Label))
            {
                AddText(page, cell.Label,
                    rect.X + 4, rect.Y + 2,
                    fontSize: 7, bold: false,
                    horizontal: HorizontalAlignment.Left);
            }
            if (!string.IsNullOrEmpty(cell.Value))
            {
                AddText(page, cell.Value,
                    rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0 - 5,
                    fontSize: 10, bold: true,
                    horizontal: HorizontalAlignment.Center);
            }
        }
    }

    private void PaintRects(FixedPage page, SpoolSheetRenderGeometry geometry)
    {
        // Filled rects first (so cell strokes overlay them)
        foreach (var rect in geometry.Rects)
        {
            if (!rect.Filled) continue;
            AddFilledRectangle(page,
                new Rect(rect.X * DipPerInch, rect.Y * DipPerInch,
                         rect.Width * DipPerInch, rect.Height * DipPerInch),
                fillHex: rect.FillHex,
                strokeThicknessDip: 0.5);
        }
        foreach (var rect in geometry.Rects)
        {
            if (rect.Filled) continue;
            AddRectangle(page,
                new Rect(rect.X * DipPerInch, rect.Y * DipPerInch,
                         rect.Width * DipPerInch, rect.Height * DipPerInch),
                strokeThicknessDip: 0.5);
        }
    }

    private void PaintLines(FixedPage page, SpoolSheetRenderGeometry geometry)
    {
        foreach (var line in geometry.Lines)
        {
            AddLine(page,
                new Point(line.X1 * DipPerInch, line.Y1 * DipPerInch),
                new Point(line.X2 * DipPerInch, line.Y2 * DipPerInch),
                Math.Max(0.5, line.Weight * DipPerInch));
        }
    }

    private void PaintTexts(FixedPage page, SpoolSheetRenderGeometry geometry)
    {
        foreach (var t in geometry.Texts)
        {
            var horiz = t.Align switch
            {
                SpoolTextAlign.Center => HorizontalAlignment.Center,
                SpoolTextAlign.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left,
            };
            AddText(page, t.Value,
                t.X * DipPerInch, t.Y * DipPerInch - t.FontSize / 2.0,
                fontSize: t.FontSize, bold: t.Bold,
                horizontal: horiz);
        }
    }

    // ── Element factories ────────────────────────────────────────────────

    private void AddRectangle(FixedPage page, Rect rect, double strokeThicknessDip)
    {
        var shape = new System.Windows.Shapes.Rectangle
        {
            Width = rect.Width,
            Height = rect.Height,
            Stroke = BorderBrush,
            StrokeThickness = strokeThicknessDip,
        };
        FixedPage.SetLeft(shape, rect.X);
        FixedPage.SetTop(shape, rect.Y);
        page.Children.Add(shape);
    }

    private void AddFilledRectangle(FixedPage page, Rect rect, string? fillHex, double strokeThicknessDip)
    {
        var fill = TryParseBrush(fillHex) ?? Brushes.LightGray;
        var shape = new System.Windows.Shapes.Rectangle
        {
            Width = rect.Width,
            Height = rect.Height,
            Fill = fill,
            Stroke = BorderBrush,
            StrokeThickness = strokeThicknessDip,
        };
        FixedPage.SetLeft(shape, rect.X);
        FixedPage.SetTop(shape, rect.Y);
        page.Children.Add(shape);
    }

    private void AddLine(FixedPage page, Point p1, Point p2, double thicknessDip)
    {
        var line = new System.Windows.Shapes.Line
        {
            X1 = p1.X,
            Y1 = p1.Y,
            X2 = p2.X,
            Y2 = p2.Y,
            Stroke = BorderBrush,
            StrokeThickness = thicknessDip,
        };
        page.Children.Add(line);
    }

    private void AddText(FixedPage page, string value, double x, double y, double fontSize, bool bold, HorizontalAlignment horizontal)
    {
        if (string.IsNullOrEmpty(value)) return;

        var tb = new System.Windows.Controls.TextBlock
        {
            Text = value,
            FontFamily = new System.Windows.Media.FontFamily(FontFamily),
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = BorderBrush,
        };

        // Measure to support center / right alignment without laying out a Grid.
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = tb.DesiredSize.Width;
        double offsetX = horizontal switch
        {
            HorizontalAlignment.Center => -width / 2.0,
            HorizontalAlignment.Right => -width,
            _ => 0,
        };

        FixedPage.SetLeft(tb, x + offsetX);
        FixedPage.SetTop(tb, y);
        page.Children.Add(tb);
    }

    private static Brush? TryParseBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(color);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Formatter used by tests.
    /// </summary>
    internal static string Format(double inches) => inches.ToString("F2", CultureInfo.InvariantCulture);
}

using System.Windows;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Standard paper size types for engineering and architectural drawings.
/// </summary>
public enum PaperSizeType
{
    ANSI_A,
    ANSI_B,
    ANSI_C,
    ANSI_D,
    ANSI_E,
    ARCH_D,
    ARCH_E,
    ISO_A4,
    ISO_A3,
    ISO_A2,
    ISO_A1,
    ISO_A0
}

/// <summary>
/// A single cell within the title block layout, representing a labeled field.
/// </summary>
public class TitleBlockCell
{
    /// <summary>Field label (e.g., "DRAWING NO", "SCALE").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Field value content.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>X position of the cell in inches from the sheet origin.</summary>
    public double X { get; set; }

    /// <summary>Y position of the cell in inches from the sheet origin.</summary>
    public double Y { get; set; }

    /// <summary>Cell width in inches.</summary>
    public double Width { get; set; }

    /// <summary>Cell height in inches.</summary>
    public double Height { get; set; }
}

/// <summary>
/// Represents a zone tick mark along the border perimeter, used for
/// grid-style coordinate referencing on engineering drawings (e.g., A1, B3).
/// </summary>
public class ZoneMark
{
    /// <summary>Label for this zone mark (e.g., "A", "3").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Position of the tick mark in inches from the sheet origin.</summary>
    public Point Position { get; set; }

    /// <summary>Whether this is a horizontal (true) or vertical (false) zone mark.</summary>
    public bool IsHorizontal { get; set; }
}

/// <summary>
/// Contains the computed geometry for a title block border layout, including
/// outer/inner borders, zone marks, title block rectangle, and individual cells.
/// All coordinates are in inches from the top-left corner of the sheet.
/// </summary>
public class TitleBlockBorderGeometry
{
    /// <summary>The outer border rectangle (full paper extent).</summary>
    public Rect OuterBorder { get; set; }

    /// <summary>The inner border rectangle (inset by the border margin).</summary>
    public Rect InnerBorder { get; set; }

    /// <summary>The title block rectangle in the bottom-right corner of the inner border.</summary>
    public Rect TitleBlockRect { get; set; }

    /// <summary>Zone tick marks along the border (A-H horizontal, 1-6 vertical).</summary>
    public List<ZoneMark> ZoneMarks { get; set; } = new();

    /// <summary>Individual labeled cells within the title block.</summary>
    public List<TitleBlockCell> TitleBlockCells { get; set; } = new();
}

/// <summary>
/// Template definition for a title block, containing paper size configuration,
/// margin settings, and all text field values used when generating drawing borders.
/// </summary>
public class TitleBlockTemplate
{
    /// <summary>Template name (e.g., "Standard D-Size Border").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Paper size selection.</summary>
    public PaperSizeType PaperSize { get; set; } = PaperSizeType.ANSI_D;

    /// <summary>Paper width in inches, computed from <see cref="PaperSize"/>.</summary>
    public double PaperWidth => TitleBlockService.GetStandardPaperSize(PaperSize).Width;

    /// <summary>Paper height in inches, computed from <see cref="PaperSize"/>.</summary>
    public double PaperHeight => TitleBlockService.GetStandardPaperSize(PaperSize).Height;

    /// <summary>Border margin inset from the paper edge, in inches.</summary>
    public double BorderMargin { get; set; } = 0.5;

    /// <summary>Height of the title block area in inches.</summary>
    public double TitleBlockHeight { get; set; } = 1.5;

    /// <summary>Number of revision history rows to include.</summary>
    public int RevisionHistoryRows { get; set; } = 5;

    /// <summary>Company name displayed in the title block.</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Project name displayed in the title block.</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Drawing number identifier.</summary>
    public string DrawingNumber { get; set; } = string.Empty;

    /// <summary>Sheet number (e.g., "1 of 5").</summary>
    public string SheetNumber { get; set; } = string.Empty;

    /// <summary>Name of the person who created the drawing.</summary>
    public string DrawnBy { get; set; } = string.Empty;

    /// <summary>Name of the person who checked/reviewed the drawing.</summary>
    public string CheckedBy { get; set; } = string.Empty;

    /// <summary>Date of the drawing or last revision.</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>Drawing scale (e.g., "1/4\" = 1'-0\"").</summary>
    public string Scale { get; set; } = string.Empty;

    /// <summary>Drawing description or title.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Generates title block and border templates for professional drawing output.
/// Produces geometry for outer/inner borders, zone reference marks, and title
/// block cells matching the format used in AutoCAD, Bluebeam, and Revit sheets.
/// </summary>
public class TitleBlockService
{
    private static readonly string[] HorizontalZoneLabels = { "A", "B", "C", "D", "E", "F", "G", "H" };
    private static readonly string[] VerticalZoneLabels = { "1", "2", "3", "4", "5", "6" };
    private static readonly HashSet<string> LiveBoundFieldLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "PROJECT",
        "DESCRIPTION",
        "DRAWING NO",
        "SHEET"
    };

    /// <summary>
    /// Returns the standard paper dimensions (width, height) in inches for a given paper size type.
    /// All sizes are in landscape orientation.
    /// </summary>
    public static (double Width, double Height) GetStandardPaperSize(PaperSizeType type) => type switch
    {
        PaperSizeType.ANSI_A => (11.0, 8.5),
        PaperSizeType.ANSI_B => (17.0, 11.0),
        PaperSizeType.ANSI_C => (22.0, 17.0),
        PaperSizeType.ANSI_D => (34.0, 22.0),
        PaperSizeType.ANSI_E => (44.0, 34.0),
        PaperSizeType.ARCH_D => (36.0, 24.0),
        PaperSizeType.ARCH_E => (48.0, 36.0),
        PaperSizeType.ISO_A4 => (11.69, 8.27),
        PaperSizeType.ISO_A3 => (16.54, 11.69),
        PaperSizeType.ISO_A2 => (23.39, 16.54),
        PaperSizeType.ISO_A1 => (33.11, 23.39),
        PaperSizeType.ISO_A0 => (46.81, 33.11),
        _ => (34.0, 22.0)
    };

    /// <summary>
    /// Generates the complete border geometry for a given title block template,
    /// including outer/inner borders, zone marks, title block rectangle, and cells.
    /// </summary>
    public TitleBlockBorderGeometry GenerateBorderGeometry(TitleBlockTemplate template)
    {
        var (paperWidth, paperHeight) = GetStandardPaperSize(template.PaperSize);
        double margin = template.BorderMargin;

        var outerBorder = new Rect(0, 0, paperWidth, paperHeight);

        var innerBorder = new Rect(
            margin,
            margin,
            paperWidth - 2 * margin,
            paperHeight - 2 * margin);

        // Title block sits in the bottom-right corner of the inner border
        double titleBlockWidth = innerBorder.Width;
        var titleBlockRect = new Rect(
            innerBorder.X,
            innerBorder.Y + innerBorder.Height - template.TitleBlockHeight,
            titleBlockWidth,
            template.TitleBlockHeight);

        var zoneMarks = GenerateZoneMarks(innerBorder);
        var cells = GenerateTitleBlockCells(titleBlockRect, template);

        return new TitleBlockBorderGeometry
        {
            OuterBorder = outerBorder,
            InnerBorder = innerBorder,
            TitleBlockRect = titleBlockRect,
            ZoneMarks = zoneMarks,
            TitleBlockCells = cells
        };
    }

    /// <summary>
    /// Returns a pre-filled default template for the given paper size, with
    /// placeholder values suitable for a new drawing.
    /// </summary>
    public TitleBlockTemplate GetDefaultTemplate(PaperSizeType paperSize)
    {
        return new TitleBlockTemplate
        {
            Name = $"Standard {paperSize} Border",
            PaperSize = paperSize,
            BorderMargin = 0.5,
            TitleBlockHeight = 1.5,
            RevisionHistoryRows = 5,
            CompanyName = "COMPANY NAME",
            ProjectName = "PROJECT NAME",
            DrawingNumber = "DWG-001",
            SheetNumber = "1 OF 1",
            DrawnBy = string.Empty,
            CheckedBy = string.Empty,
            Date = DateTime.Now.ToString("yyyy-MM-dd"),
            Scale = "AS NOTED",
            Description = "DRAWING DESCRIPTION"
        };
    }

    public TitleBlockTemplate CloneTemplate(TitleBlockTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return new TitleBlockTemplate
        {
            Name = template.Name,
            PaperSize = template.PaperSize,
            BorderMargin = template.BorderMargin,
            TitleBlockHeight = template.TitleBlockHeight,
            RevisionHistoryRows = template.RevisionHistoryRows,
            CompanyName = template.CompanyName,
            ProjectName = template.ProjectName,
            DrawingNumber = template.DrawingNumber,
            SheetNumber = template.SheetNumber,
            DrawnBy = template.DrawnBy,
            CheckedBy = template.CheckedBy,
            Date = template.Date,
            Scale = template.Scale,
            Description = template.Description
        };
    }

    public TitleBlockTemplate BuildResolvedTemplate(
        TitleBlockTemplate template,
        string projectName,
        DrawingSheet sheet,
        int sheetIndex,
        int sheetCount)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(sheet);

        var resolved = CloneTemplate(template);
        resolved.ProjectName = string.IsNullOrWhiteSpace(projectName) ? resolved.ProjectName : projectName.Trim();
        resolved.Description = sheet.Name;
        resolved.DrawingNumber = sheet.Number;
        resolved.SheetNumber = $"{Math.Max(1, sheetIndex)} OF {Math.Max(1, sheetCount)}";
        return resolved;
    }

    public static bool IsLiveBoundFieldLabel(string? label)
        => !string.IsNullOrWhiteSpace(label) && LiveBoundFieldLabels.Contains(label.Trim());

    public static bool TrySetFieldValue(TitleBlockTemplate template, string? label, string? value)
    {
        ArgumentNullException.ThrowIfNull(template);

        var normalizedLabel = NormalizeFieldLabel(label);
        var nextValue = value ?? string.Empty;
        switch (normalizedLabel)
        {
            case "COMPANY":
                template.CompanyName = nextValue;
                return true;
            case "PROJECT":
                template.ProjectName = nextValue;
                return true;
            case "DRAWING NO":
                template.DrawingNumber = nextValue;
                return true;
            case "SHEET":
                template.SheetNumber = nextValue;
                return true;
            case "DRAWN BY":
                template.DrawnBy = nextValue;
                return true;
            case "CHECKED BY":
                template.CheckedBy = nextValue;
                return true;
            case "DATE":
                template.Date = nextValue;
                return true;
            case "SCALE":
                template.Scale = nextValue;
                return true;
            case "DESCRIPTION":
                template.Description = nextValue;
                return true;
            default:
                return false;
        }
    }

    public static string? TryGetFieldValue(TitleBlockTemplate template, string? label)
    {
        ArgumentNullException.ThrowIfNull(template);

        return NormalizeFieldLabel(label) switch
        {
            "COMPANY" => template.CompanyName,
            "PROJECT" => template.ProjectName,
            "DRAWING NO" => template.DrawingNumber,
            "SHEET" => template.SheetNumber,
            "DRAWN BY" => template.DrawnBy,
            "CHECKED BY" => template.CheckedBy,
            "DATE" => template.Date,
            "SCALE" => template.Scale,
            "DESCRIPTION" => template.Description,
            _ => null
        };
    }

    private static string NormalizeFieldLabel(string? label)
        => string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim().ToUpperInvariant();

    /// <summary>
    /// Generates zone tick marks along the inner border perimeter.
    /// Horizontal zones are labeled A through H, vertical zones are labeled 1 through 6.
    /// Marks are placed at the midpoint of each zone segment along both the top/bottom
    /// and left/right edges.
    /// </summary>
    private static List<ZoneMark> GenerateZoneMarks(Rect innerBorder)
    {
        var marks = new List<ZoneMark>();

        int hZones = HorizontalZoneLabels.Length; // 8
        int vZones = VerticalZoneLabels.Length;    // 6

        double hZoneWidth = innerBorder.Width / hZones;
        double vZoneHeight = innerBorder.Height / vZones;

        // Horizontal zone marks along top and bottom edges
        for (int i = 0; i < hZones; i++)
        {
            double centerX = innerBorder.X + (i + 0.5) * hZoneWidth;

            // Top edge mark
            marks.Add(new ZoneMark
            {
                Label = HorizontalZoneLabels[i],
                Position = new Point(centerX, innerBorder.Y),
                IsHorizontal = true
            });

            // Bottom edge mark
            marks.Add(new ZoneMark
            {
                Label = HorizontalZoneLabels[i],
                Position = new Point(centerX, innerBorder.Y + innerBorder.Height),
                IsHorizontal = true
            });
        }

        // Vertical zone marks along left and right edges
        for (int i = 0; i < vZones; i++)
        {
            double centerY = innerBorder.Y + (i + 0.5) * vZoneHeight;

            // Left edge mark
            marks.Add(new ZoneMark
            {
                Label = VerticalZoneLabels[i],
                Position = new Point(innerBorder.X, centerY),
                IsHorizontal = false
            });

            // Right edge mark
            marks.Add(new ZoneMark
            {
                Label = VerticalZoneLabels[i],
                Position = new Point(innerBorder.X + innerBorder.Width, centerY),
                IsHorizontal = false
            });
        }

        return marks;
    }

    /// <summary>
    /// Generates the individual cell rectangles and labels for the title block.
    /// The layout follows a standard engineering title block arrangement with
    /// a company/project header row, metadata fields, and a revision history section.
    /// </summary>
    private static List<TitleBlockCell> GenerateTitleBlockCells(
        Rect titleBlockRect, TitleBlockTemplate template)
    {
        var cells = new List<TitleBlockCell>();

        double tbX = titleBlockRect.X;
        double tbY = titleBlockRect.Y;
        double tbWidth = titleBlockRect.Width;
        double tbHeight = titleBlockRect.Height;

        // Layout strategy:
        // The title block is divided into a right-side info block and a left-side revision block.
        // Right info block width: ~6.5 inches (or half of total width, whichever is smaller)
        double infoBlockWidth = Math.Min(6.5, tbWidth * 0.5);
        double revisionBlockWidth = tbWidth - infoBlockWidth;

        double infoBlockX = tbX + revisionBlockWidth;
        double revisionBlockX = tbX;

        // --- Right-side info block ---
        // Row heights within the info block
        double companyRowHeight = tbHeight * 0.30;
        double descriptionRowHeight = tbHeight * 0.30;
        double metadataRowHeight = tbHeight * 0.40;

        double currentY = tbY;

        // Row 1: Company and Project (side by side)
        double halfInfoWidth = infoBlockWidth / 2.0;

        cells.Add(new TitleBlockCell
        {
            Label = "COMPANY",
            Value = template.CompanyName,
            X = infoBlockX,
            Y = currentY,
            Width = halfInfoWidth,
            Height = companyRowHeight
        });

        cells.Add(new TitleBlockCell
        {
            Label = "PROJECT",
            Value = template.ProjectName,
            X = infoBlockX + halfInfoWidth,
            Y = currentY,
            Width = halfInfoWidth,
            Height = companyRowHeight
        });

        currentY += companyRowHeight;

        // Row 2: Description (full width of info block)
        cells.Add(new TitleBlockCell
        {
            Label = "DESCRIPTION",
            Value = template.Description,
            X = infoBlockX,
            Y = currentY,
            Width = infoBlockWidth,
            Height = descriptionRowHeight
        });

        currentY += descriptionRowHeight;

        // Row 3: Metadata fields in a grid (2 rows x 4 columns)
        double metaCellWidth = infoBlockWidth / 4.0;
        double metaCellHeight = metadataRowHeight / 2.0;

        // Top metadata row: DRAWING NO, SHEET, SCALE, DATE
        cells.Add(new TitleBlockCell
        {
            Label = "DRAWING NO",
            Value = template.DrawingNumber,
            X = infoBlockX,
            Y = currentY,
            Width = metaCellWidth,
            Height = metaCellHeight
        });

        cells.Add(new TitleBlockCell
        {
            Label = "SHEET",
            Value = template.SheetNumber,
            X = infoBlockX + metaCellWidth,
            Y = currentY,
            Width = metaCellWidth,
            Height = metaCellHeight
        });

        cells.Add(new TitleBlockCell
        {
            Label = "SCALE",
            Value = template.Scale,
            X = infoBlockX + 2 * metaCellWidth,
            Y = currentY,
            Width = metaCellWidth,
            Height = metaCellHeight
        });

        cells.Add(new TitleBlockCell
        {
            Label = "DATE",
            Value = template.Date,
            X = infoBlockX + 3 * metaCellWidth,
            Y = currentY,
            Width = metaCellWidth,
            Height = metaCellHeight
        });

        currentY += metaCellHeight;

        // Bottom metadata row: DRAWN BY, CHECKED BY (each spanning 2 columns)
        double doubleMetaCellWidth = metaCellWidth * 2.0;

        cells.Add(new TitleBlockCell
        {
            Label = "DRAWN BY",
            Value = template.DrawnBy,
            X = infoBlockX,
            Y = currentY,
            Width = doubleMetaCellWidth,
            Height = metaCellHeight
        });

        cells.Add(new TitleBlockCell
        {
            Label = "CHECKED BY",
            Value = template.CheckedBy,
            X = infoBlockX + doubleMetaCellWidth,
            Y = currentY,
            Width = doubleMetaCellWidth,
            Height = metaCellHeight
        });

        // --- Left-side revision block ---
        // Header row + revision history rows
        double revHeaderHeight = tbHeight * 0.20;
        double revRowHeight = (tbHeight - revHeaderHeight) / Math.Max(1, template.RevisionHistoryRows);

        cells.Add(new TitleBlockCell
        {
            Label = "REVISIONS",
            Value = string.Empty,
            X = revisionBlockX,
            Y = tbY,
            Width = revisionBlockWidth,
            Height = revHeaderHeight
        });

        for (int i = 0; i < template.RevisionHistoryRows; i++)
        {
            cells.Add(new TitleBlockCell
            {
                Label = $"REV {i + 1}",
                Value = string.Empty,
                X = revisionBlockX,
                Y = tbY + revHeaderHeight + i * revRowHeight,
                Width = revisionBlockWidth,
                Height = revRowHeight
            });
        }

        return cells;
    }
}

namespace ElectricalComponentSandbox.Models;

/// <summary>
/// A single pen entry in a CTB-style colour-based plot-style table.
/// Maps a pen number (0–255) to output colour, line weight, and line-end style.
/// </summary>
public class PlotStylePen
{
    public int PenNumber { get; set; }

    /// <summary>Output colour as hex (#RRGGBB). Null = use object colour.</summary>
    public string? OutputColor { get; set; }

    /// <summary>Output line weight in mm. 0 = use object weight.</summary>
    public double LineWeight { get; set; }

    /// <summary>Line end style: 0 = Flat, 1 = Square, 2 = Round</summary>
    public int LineEndStyle { get; set; }

    /// <summary>Screening 0–100 (100 = full colour, 0 = white / invisible)</summary>
    public int Screening { get; set; } = 100;
}

/// <summary>
/// A colour-based plot style table (CTB), containing up to 256 pen definitions.
/// </summary>
public class PlotStyleTable
{
    public string Name { get; set; } = "Default";
    public string Description { get; set; } = string.Empty;
    public List<PlotStylePen> Pens { get; set; } = new();

    /// <summary>Creates a basic monochrome CTB where all pens map to black at 0.25 mm.</summary>
    public static PlotStyleTable CreateMonochrome()
    {
        var table = new PlotStyleTable { Name = "monochrome.ctb", Description = "All colours → black" };
        for (int i = 0; i < 256; i++)
            table.Pens.Add(new PlotStylePen { PenNumber = i, OutputColor = "#000000", LineWeight = 0.25 });
        return table;
    }
}

/// <summary>Standard paper sizes for print / paper-space layout.</summary>
public enum PaperSize
{
    Letter,   // 8.5 × 11 in
    Legal,    // 8.5 × 14 in
    Tabloid,  // 11 × 17 in
    ANSI_C,   // 17 × 22 in
    ANSI_D,   // 22 × 34 in
    ANSI_E,   // 34 × 44 in
    A4,       // 210 × 297 mm
    A3,       // 297 × 420 mm
    A2,       // 420 × 594 mm
    A1,       // 594 × 841 mm
    A0,       // 841 × 1189 mm
    Custom
}

/// <summary>
/// Print / paper-space layout settings.
/// Describes the sheet, plot area, and active CTB table.
/// </summary>
public class PlotLayout
{
    public string Name { get; set; } = "Layout1";
    public PaperSize PaperSize { get; set; } = PaperSize.ANSI_D;

    /// <summary>Custom paper width in inches (only used when PaperSize == Custom)</summary>
    public double CustomWidth { get; set; } = 22;
    /// <summary>Custom paper height in inches</summary>
    public double CustomHeight { get; set; } = 34;

    /// <summary>Plot scale: 1 means 1 drawing-unit = 1 inch on paper</summary>
    public double PlotScale { get; set; } = 1.0;

    /// <summary>Name of the active CTB table</summary>
    public string PlotStyleTableName { get; set; } = "monochrome.ctb";

    public PlotLayout Clone()
    {
        return new PlotLayout
        {
            Name = Name,
            PaperSize = PaperSize,
            CustomWidth = CustomWidth,
            CustomHeight = CustomHeight,
            PlotScale = PlotScale,
            PlotStyleTableName = PlotStyleTableName
        };
    }

    public void ApplyFrom(PlotLayout source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Name = source.Name;
        PaperSize = source.PaperSize;
        CustomWidth = source.CustomWidth;
        CustomHeight = source.CustomHeight;
        PlotScale = source.PlotScale;
        PlotStyleTableName = source.PlotStyleTableName;
    }

    public string GetSummaryText()
    {
        var (width, height) = GetPaperInches();
        return $"{Name} — {PaperSize} ({width:F2}\" x {height:F2}\"), Scale {PlotScale:g}, CTB {PlotStyleTableName}";
    }

    /// <summary>Gets the effective paper dimensions in inches</summary>
    public (double Width, double Height) GetPaperInches() => PaperSize switch
    {
        PaperSize.Letter  => (8.5, 11),
        PaperSize.Legal   => (8.5, 14),
        PaperSize.Tabloid => (11, 17),
        PaperSize.ANSI_C  => (17, 22),
        PaperSize.ANSI_D  => (22, 34),
        PaperSize.ANSI_E  => (34, 44),
        PaperSize.A4      => (8.27, 11.69),
        PaperSize.A3      => (11.69, 16.54),
        PaperSize.A2      => (16.54, 23.39),
        PaperSize.A1      => (23.39, 33.11),
        PaperSize.A0      => (33.11, 46.81),
        _                 => (CustomWidth, CustomHeight)
    };
}

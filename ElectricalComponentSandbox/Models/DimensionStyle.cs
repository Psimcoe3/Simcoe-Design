namespace ElectricalComponentSandbox.Models;

/// <summary>
/// AutoCAD-style dimension style (DIMSTYLE) defining the appearance of
/// dimensions, leaders, and tolerances.
/// </summary>
public class DimensionStyleDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Standard";

    // ── Text ─────────────────────────────────────────────────────────────────

    /// <summary>Text height in drawing units</summary>
    public double TextHeight { get; set; } = 0.125;

    /// <summary>Font family for dimension text</summary>
    public string FontFamily { get; set; } = "Arial";

    /// <summary>Text color (hex). Null = ByLayer.</summary>
    public string? TextColor { get; set; }

    /// <summary>Text placement above/centered/below extension line</summary>
    public DimensionTextPlacement TextPlacement { get; set; } = DimensionTextPlacement.Above;

    /// <summary>Gap between dimension line and text</summary>
    public double TextGap { get; set; } = 0.0625;

    // ── Lines ────────────────────────────────────────────────────────────────

    /// <summary>Dimension line color (hex). Null = ByLayer.</summary>
    public string? DimensionLineColor { get; set; }

    /// <summary>Extension line color (hex). Null = ByLayer.</summary>
    public string? ExtensionLineColor { get; set; }

    /// <summary>Extension line offset from origin point</summary>
    public double ExtensionLineOffset { get; set; } = 0.0625;

    /// <summary>Extension line extension beyond dimension line</summary>
    public double ExtensionLineExtension { get; set; } = 0.125;

    /// <summary>Dimension line weight in points. 0 = ByLayer.</summary>
    public double LineWeight { get; set; } = 0;

    // ── Arrows ───────────────────────────────────────────────────────────────

    /// <summary>Arrow/tick size in drawing units</summary>
    public double ArrowSize { get; set; } = 0.125;

    /// <summary>Arrow type for dimension lines</summary>
    public ArrowType ArrowType { get; set; } = ArrowType.ClosedFilled;

    // ── Units ────────────────────────────────────────────────────────────────

    /// <summary>Linear unit format</summary>
    public DimensionUnitFormat UnitFormat { get; set; } = DimensionUnitFormat.Architectural;

    /// <summary>Number of decimal places (for Decimal format) or denominator (for Fractional)</summary>
    public int Precision { get; set; } = 16;

    /// <summary>Unit suffix appended to dimension text (e.g. " ft", " m")</summary>
    public string UnitSuffix { get; set; } = string.Empty;

    /// <summary>Scale factor applied to measured length before display</summary>
    public double LinearScaleFactor { get; set; } = 1.0;

    // ── Angular ──────────────────────────────────────────────────────────────

    /// <summary>Angular unit format</summary>
    public AngularUnitFormat AngularFormat { get; set; } = AngularUnitFormat.DecimalDegrees;

    /// <summary>Angular precision (decimal places)</summary>
    public int AngularPrecision { get; set; } = 0;

    // ── Tolerances ───────────────────────────────────────────────────────────

    /// <summary>Tolerance display mode</summary>
    public ToleranceMode ToleranceMode { get; set; } = ToleranceMode.None;

    /// <summary>Upper tolerance value</summary>
    public double ToleranceUpper { get; set; } = 0;

    /// <summary>Lower tolerance value</summary>
    public double ToleranceLower { get; set; } = 0;
}

public enum DimensionTextPlacement
{
    Above,
    Centered,
    Below
}

public enum ArrowType
{
    ClosedFilled,
    ClosedBlank,
    Open,
    Dot,
    Tick,
    None
}

public enum DimensionUnitFormat
{
    Decimal,
    Architectural,
    Engineering,
    Fractional,
    Scientific
}

public enum AngularUnitFormat
{
    DecimalDegrees,
    DegreesMinutesSeconds,
    Gradians,
    Radians
}

public enum ToleranceMode
{
    None,
    Symmetrical,
    Deviation,
    Limits
}

/// <summary>
/// Text style definition, analogous to AutoCAD STYLE command.
/// </summary>
public class TextStyle
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Standard";
    public string FontFamily { get; set; } = "Arial";
    public double Height { get; set; } = 0.125;
    public double WidthFactor { get; set; } = 1.0;
    public double ObliqueAngle { get; set; } = 0;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
}

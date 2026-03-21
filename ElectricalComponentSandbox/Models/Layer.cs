namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Line type patterns following standard CAD conventions
/// </summary>
public enum LineType
{
    Continuous,
    Dashed,
    Dotted,
    Phantom,
    Hidden,
    Center,
    DashDot,
    DashDotDot
}

/// <summary>
/// Represents a drawing layer for organizing components.
/// Extended with professional CAD layer properties (lineweight, linetype, freeze, plot).
/// </summary>
public class Layer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Layer";
    public string Color { get; set; } = "#808080";
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; } = false;

    // ── Extended CAD Layer Properties ─────────────────────────────────────────

    /// <summary>Line weight in points (0 = default/ByLayer).  Standard values: 0, 0.13, 0.18, 0.25, 0.35, 0.50, 0.70, 1.00</summary>
    public double LineWeight { get; set; } = 0.0;

    /// <summary>Default line type for this layer</summary>
    public LineType LineType { get; set; } = LineType.Continuous;

    /// <summary>
    /// Frozen layers are invisible AND excluded from regeneration / snap geometry.
    /// Differs from IsVisible: frozen also skips snap candidates.
    /// </summary>
    public bool IsFrozen { get; set; } = false;

    /// <summary>Whether this layer outputs to print/PDF.  Non-plotted layers are visible on screen only.</summary>
    public bool IsPlotted { get; set; } = true;

    /// <summary>Optional description visible in the layer manager</summary>
    public string Description { get; set; } = string.Empty;

    public static Layer CreateDefault()
    {
        return new Layer
        {
            Id = "default",
            Name = "Default",
            Color = "#808080",
            IsVisible = true,
            IsLocked = false,
            IsPlotted = true
        };
    }

    public static Layer CreateMarkupDefault()
    {
        return new Layer
        {
            Id = "markup-default",
            Name = "Markup",
            Color = "#FF0000",
            IsVisible = true,
            IsLocked = false,
            IsPlotted = true
        };
    }
}

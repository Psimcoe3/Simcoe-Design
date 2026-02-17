namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Represents a PDF file imported as an underlay for tracing/reference
/// </summary>
public class PdfUnderlay
{
    public string FilePath { get; set; } = string.Empty;
    public int PageNumber { get; set; } = 1;
    public double Opacity { get; set; } = 0.5;
    public bool IsLocked { get; set; } = true;
    public double Scale { get; set; } = 1.0;
    public double OffsetX { get; set; } = 0.0;
    public double OffsetY { get; set; } = 0.0;

    /// <summary>
    /// Page rotation in degrees (0, 90, 180, 270)
    /// </summary>
    public double RotationDegrees { get; set; } = 0.0;
    
    /// <summary>
    /// Indicates whether scale has been calibrated using two-point measurement
    /// </summary>
    public bool IsCalibrated { get; set; } = false;
    
    /// <summary>
    /// Calibrated pixels-per-unit ratio (set by two-point calibration)
    /// </summary>
    public double PixelsPerUnit { get; set; } = 1.0;

    /// <summary>
    /// Separate X-axis scale factor for non-uniform calibration (doc units per real unit)
    /// </summary>
    public double ScaleX { get; set; } = 1.0;

    /// <summary>
    /// Separate Y-axis scale factor for non-uniform calibration (doc units per real unit)
    /// </summary>
    public double ScaleY { get; set; } = 1.0;
}

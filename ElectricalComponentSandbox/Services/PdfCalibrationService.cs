using System.Windows;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Calibrates PDF underlay scale using two picked points and a known real-world distance
/// </summary>
public class PdfCalibrationService
{
    /// <summary>
    /// Result of a calibration operation
    /// </summary>
    public class CalibrationResult
    {
        public double PixelsPerUnit { get; set; }
        public double PixelDistance { get; set; }
        public double RealDistance { get; set; }
        public bool IsValid { get; set; }
    }
    
    /// <summary>
    /// Computes the pixels-per-unit ratio given two screen points and a known real distance
    /// </summary>
    /// <param name="point1">First picked point in pixel coordinates</param>
    /// <param name="point2">Second picked point in pixel coordinates</param>
    /// <param name="knownDistance">Known real-world distance between the two points (in project units)</param>
    /// <returns>Calibration result with computed scale</returns>
    public CalibrationResult Calibrate(Point point1, Point point2, double knownDistance)
    {
        if (knownDistance <= 0)
        {
            return new CalibrationResult { IsValid = false };
        }
        
        double dx = point2.X - point1.X;
        double dy = point2.Y - point1.Y;
        double pixelDistance = Math.Sqrt(dx * dx + dy * dy);
        
        if (pixelDistance < 1e-6)
        {
            return new CalibrationResult { IsValid = false };
        }
        
        double pixelsPerUnit = pixelDistance / knownDistance;
        
        return new CalibrationResult
        {
            PixelsPerUnit = pixelsPerUnit,
            PixelDistance = pixelDistance,
            RealDistance = knownDistance,
            IsValid = true
        };
    }
}

using System.Windows;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Manages three coordinate spaces and transforms between them:
///   Screen (device px) ↔ Document (PDF points, user space) ↔ Real-world (ft, in)
///
/// Calibration: two picked Document-space points + known real-world distance
/// Supports separate X/Y scale factors for non-uniform drawings.
/// </summary>
public class CoordinateTransformService
{
    // ── Screen ↔ Document transform parameters ──

    /// <summary>Pan offset (document origin in screen pixels)</summary>
    public Point PanOffset { get; set; } = new(0, 0);

    /// <summary>Uniform zoom level (screen px per document point)</summary>
    public double Zoom { get; set; } = 1.0;

    // ── Document ↔ Real-world calibration ──

    /// <summary>Document units per real-world unit along X axis</summary>
    public double DocUnitsPerRealX { get; set; } = 1.0;

    /// <summary>Document units per real-world unit along Y axis</summary>
    public double DocUnitsPerRealY { get; set; } = 1.0;

    /// <summary>Whether calibration has been performed</summary>
    public bool IsCalibrated { get; set; }

    // ────────── Screen ↔ Document ──────────

    /// <summary>
    /// Converts a screen-space point to document-space
    /// </summary>
    public Point ScreenToDocument(Point screen)
    {
        if (Zoom == 0) return screen;
        return new Point(
            (screen.X - PanOffset.X) / Zoom,
            (screen.Y - PanOffset.Y) / Zoom);
    }

    /// <summary>
    /// Converts a document-space point to screen-space
    /// </summary>
    public Point DocumentToScreen(Point doc)
    {
        return new Point(
            doc.X * Zoom + PanOffset.X,
            doc.Y * Zoom + PanOffset.Y);
    }

    /// <summary>
    /// Converts a screen-space distance to document-space distance
    /// </summary>
    public double ScreenToDocumentDistance(double screenDist)
    {
        if (Zoom == 0) return screenDist;
        return screenDist / Zoom;
    }

    // ────────── Document ↔ Real-world ──────────

    /// <summary>
    /// Converts a document-space point to real-world coordinates
    /// </summary>
    public Point DocumentToRealWorld(Point doc)
    {
        return new Point(
            DocUnitsPerRealX != 0 ? doc.X / DocUnitsPerRealX : doc.X,
            DocUnitsPerRealY != 0 ? doc.Y / DocUnitsPerRealY : doc.Y);
    }

    /// <summary>
    /// Converts a real-world coordinate to document-space
    /// </summary>
    public Point RealWorldToDocument(Point real)
    {
        return new Point(
            real.X * DocUnitsPerRealX,
            real.Y * DocUnitsPerRealY);
    }

    /// <summary>
    /// Converts a document-space distance to real-world distance.
    /// Uses the average of X and Y scale for isotropic measurement.
    /// </summary>
    public double DocumentToRealWorldDistance(double docDist)
    {
        double avgScale = (DocUnitsPerRealX + DocUnitsPerRealY) / 2.0;
        return avgScale != 0 ? docDist / avgScale : docDist;
    }

    /// <summary>
    /// Converts a document-space distance to real-world using per-axis scale
    /// for a vector direction
    /// </summary>
    public double DocumentToRealWorldDistance(double docDx, double docDy)
    {
        double rwDx = DocUnitsPerRealX != 0 ? docDx / DocUnitsPerRealX : docDx;
        double rwDy = DocUnitsPerRealY != 0 ? docDy / DocUnitsPerRealY : docDy;
        return Math.Sqrt(rwDx * rwDx + rwDy * rwDy);
    }

    // ────────── Screen → Real-world (convenience) ──────────

    /// <summary>
    /// Converts a screen-space point directly to real-world coordinates
    /// </summary>
    public Point ScreenToRealWorld(Point screen)
    {
        return DocumentToRealWorld(ScreenToDocument(screen));
    }

    // ────────── Calibration ──────────

    /// <summary>
    /// Calibrates the Document→Real-world transform using two picked
    /// document-space points and a known real-world distance.
    /// Uniform scale (same X and Y).
    /// </summary>
    public CalibrationInfo CalibrateUniform(Point docPoint1, Point docPoint2, double knownRealDistance)
    {
        if (knownRealDistance <= 0)
            return new CalibrationInfo { IsValid = false };

        double docDist = GeometryMath.Distance(docPoint1, docPoint2);
        if (docDist < 1e-6)
            return new CalibrationInfo { IsValid = false };

        double scale = docDist / knownRealDistance;
        DocUnitsPerRealX = scale;
        DocUnitsPerRealY = scale;
        IsCalibrated = true;

        return new CalibrationInfo
        {
            IsValid = true,
            DocDistance = docDist,
            RealDistance = knownRealDistance,
            ScaleX = scale,
            ScaleY = scale
        };
    }

    /// <summary>
    /// Calibrates with separate X and Y scales (for non-uniform drawings).
    /// Requires two pairs of points: one primarily horizontal, one primarily vertical.
    /// </summary>
    public CalibrationInfo CalibrateSeparateXY(
        Point docH1, Point docH2, double knownRealDistX,
        Point docV1, Point docV2, double knownRealDistY)
    {
        if (knownRealDistX <= 0 || knownRealDistY <= 0)
            return new CalibrationInfo { IsValid = false };

        double docDx = Math.Abs(docH2.X - docH1.X);
        double docDy = Math.Abs(docV2.Y - docV1.Y);

        if (docDx < 1e-6 || docDy < 1e-6)
            return new CalibrationInfo { IsValid = false };

        DocUnitsPerRealX = docDx / knownRealDistX;
        DocUnitsPerRealY = docDy / knownRealDistY;
        IsCalibrated = true;

        return new CalibrationInfo
        {
            IsValid = true,
            DocDistance = GeometryMath.Distance(docH1, docH2),
            RealDistance = knownRealDistX,
            ScaleX = DocUnitsPerRealX,
            ScaleY = DocUnitsPerRealY
        };
    }
}

/// <summary>
/// Result of a calibration operation
/// </summary>
public class CalibrationInfo
{
    public bool IsValid { get; set; }
    public double DocDistance { get; set; }
    public double RealDistance { get; set; }
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }
}

using System.Windows;
using ElectricalComponentSandbox.Markup.Services;

namespace ElectricalComponentSandbox.Rendering;

/// <summary>
/// Holds the current 2D canvas transform state:
///   • Pan and zoom (Screen ↔ Document)
///   • Active layer style (color, line weight, linetype)
///   • Delegates to <see cref="CoordinateTransformService"/> for real-world conversions
///
/// All draw calls on <see cref="ICanvas2DRenderer"/> use Document-space coordinates.
/// This class converts between Document and Screen as needed.
/// </summary>
public sealed class DrawingContext2D
{
    // ── Transform ─────────────────────────────────────────────────────────────

    /// <summary>Pan offset in screen pixels (where Document origin lands on screen)</summary>
    public double PanX { get; set; } = 0;
    public double PanY { get; set; } = 0;

    /// <summary>Zoom level: screen pixels per document unit</summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>Viewport dimensions in screen pixels (updated by SkiaCanvasHost)</summary>
    public double ViewportWidth { get; set; }
    public double ViewportHeight { get; set; }

    // ── Calibration / real-world ──────────────────────────────────────────────

    /// <summary>Shared coordinate transform service (reused from Markup system)</summary>
    public CoordinateTransformService CoordTransform { get; } = new();

    /// <summary>Syncs CoordinateTransformService from current pan/zoom values</summary>
    public void SyncCoordTransform()
    {
        CoordTransform.PanOffset = new Point(PanX, PanY);
        CoordTransform.Zoom = Zoom;
    }

    // ── Active drawing style ──────────────────────────────────────────────────

    /// <summary>Style for the currently active layer/object override</summary>
    public RenderStyle ActiveStyle { get; set; } = RenderStyle.Default;

    // ── Conversions ───────────────────────────────────────────────────────────

    public Point DocumentToScreen(Point doc)
        => new(doc.X * Zoom + PanX, doc.Y * Zoom + PanY);

    public Point ScreenToDocument(Point screen)
        => Zoom == 0 ? screen : new((screen.X - PanX) / Zoom, (screen.Y - PanY) / Zoom);

    public double DocumentToScreenDist(double docDist) => docDist * Zoom;
    public double ScreenToDocumentDist(double screenDist) => Zoom == 0 ? screenDist : screenDist / Zoom;

    /// <summary>Document-space rect visible in the current viewport</summary>
    public Rect ViewportDocRect =>
        new(ScreenToDocument(new Point(0, 0)),
            ScreenToDocument(new Point(ViewportWidth, ViewportHeight)));

    // ── Zoom helpers ──────────────────────────────────────────────────────────

    /// <summary>Zooms about a screen-space anchor point</summary>
    public void ZoomAbout(Point screenAnchor, double factor)
    {
        var docBefore = ScreenToDocument(screenAnchor);
        Zoom *= factor;
        Zoom = Math.Max(0.01, Math.Min(1000.0, Zoom));
        var screenAfter = DocumentToScreen(docBefore);
        PanX += screenAnchor.X - screenAfter.X;
        PanY += screenAnchor.Y - screenAfter.Y;
        SyncCoordTransform();
    }

    /// <summary>Pans by a screen-space delta</summary>
    public void Pan(double dx, double dy)
    {
        PanX += dx;
        PanY += dy;
        SyncCoordTransform();
    }

    /// <summary>Detail level based on current zoom (matches Revit convention)</summary>
    public Markup.Services.DetailLevel CurrentDetailLevel =>
        Zoom >= 10.0 ? Markup.Services.DetailLevel.Fine :
        Zoom >= 2.0  ? Markup.Services.DetailLevel.Medium :
                       Markup.Services.DetailLevel.Coarse;
}

using System.IO;
using ElectricalComponentSandbox.Rendering;
using SkiaSharp;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Exports a <see cref="SpoolSheetRenderGeometry"/> to a real PDF file using
/// SkiaSharp's built-in <see cref="SKDocument.CreatePdf(SKWStream)"/>. Reuses
/// the existing <see cref="SkiaCanvas2DRenderer"/> + <see cref="SpoolSheetCanvasPainter"/>
/// stack so the PDF is pixel-identical to the on-screen preview — no separate
/// PDF-specific drawing code to drift.
/// </summary>
public sealed class SpoolSheetPdfExporter
{
    /// <summary>PDF points per inch — fixed at 72 by the PDF spec.</summary>
    public const double PdfPointsPerInch = 72.0;

    /// <summary>Painter used to translate geometry into Skia draw calls.</summary>
    public SpoolSheetCanvasPainter Painter { get; init; } = new();

    /// <summary>
    /// Writes the geometry to a PDF file at <paramref name="path"/>.
    /// Overwrites any existing file at the destination.
    /// </summary>
    public void SaveToFile(SpoolSheetRenderGeometry geometry, string path)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.Create(path);
        SaveToStream(geometry, stream);
    }

    /// <summary>
    /// Writes the geometry as a PDF document to the supplied stream.
    /// </summary>
    public void SaveToStream(SpoolSheetRenderGeometry geometry, Stream output)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(output);

        using var managedWStream = new SKManagedWStream(output);
        using var document = SKDocument.CreatePdf(managedWStream)
            ?? throw new InvalidOperationException("SkiaSharp could not create a PDF document.");

        float pageWidthPts = (float)(geometry.PaperWidthInches * PdfPointsPerInch);
        float pageHeightPts = (float)(geometry.PaperHeightInches * PdfPointsPerInch);
        var canvas = document.BeginPage(pageWidthPts, pageHeightPts)
            ?? throw new InvalidOperationException("SkiaSharp could not begin a PDF page.");

        // DrawingContext2D translates document-space coordinates → screen pixels.
        // We want geometry inches → PDF points, so set Zoom = PdfPointsPerInch
        // with no pan and a viewport sized to the page (in points).
        var ctx = new DrawingContext2D
        {
            Zoom = PdfPointsPerInch,
            PanX = 0,
            PanY = 0,
            ViewportWidth = pageWidthPts,
            ViewportHeight = pageHeightPts,
        };

        var renderer = new SkiaCanvas2DRenderer(canvas, ctx);
        Painter.Paint(renderer, geometry);

        document.EndPage();
        document.Close();
    }
}

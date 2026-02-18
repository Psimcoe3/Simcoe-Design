using System.Collections.Generic;
using System.IO;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using PDFtoImage;

namespace ElectricalComponentSandbox.Tests.Services;

/// <summary>
/// Integration tests for PDF import and rendering using the actual PDFtoImage pipeline.
/// Requires the target PDF to be present on disk.
/// These tests are skipped (via xUnit 2.x [Fact(Skip=...)] on missing file)
/// but since we confirmed the file exists, all should run.
/// </summary>
public class PdfImportIntegrationTests
{
    private const string TestPdfPath = @"C:\Users\Paul\Downloads\E1-L37-Level 37 Conduit.pdf";

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Guard: fail fast with a clear message if the PDF is not present.</summary>
    private static void RequirePdf() =>
        Assert.True(File.Exists(TestPdfPath),
            $"Test PDF not found: '{TestPdfPath}'. Copy the file there to enable these tests.");

    // ── PdfUnderlay model ────────────────────────────────────────────────────

    [Fact]
    public void PdfUnderlay_CanBeCreatedWithRealFilePath()
    {
        RequirePdf();

        var underlay = new PdfUnderlay
        {
            FilePath  = TestPdfPath,
            PageNumber = 1,
            Opacity   = 0.5,
            IsLocked  = true
        };

        Assert.Equal(TestPdfPath, underlay.FilePath);
        Assert.Equal(1, underlay.PageNumber);
        Assert.Equal(0.5, underlay.Opacity);
        Assert.True(underlay.IsLocked);
    }

    // ── Rendering via PDFtoImage ─────────────────────────────────────────────

    [Fact]
    public void Pdf_CanBeReadFromDisk()
    {
        RequirePdf();

        var bytes = File.ReadAllBytes(TestPdfPath);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0, "PDF file should not be empty");
    }

    [Fact]
    public void Pdf_HasValidPageCount()
    {
        RequirePdf();

        var bytes = File.ReadAllBytes(TestPdfPath);
        var pageCount = Conversion.GetPageCount(bytes);

        Assert.True(pageCount > 0, $"Page count should be > 0, got {pageCount}");
        Console.WriteLine($"[INFO] Page count: {pageCount}");
    }

    [Fact]
    public void Pdf_FirstPageDimensions_AreNonZero()
    {
        RequirePdf();

        var bytes = File.ReadAllBytes(TestPdfPath);
        var size = Conversion.GetPageSize(bytes, page: 0);

        Assert.True(size.Width  > 0, $"PDF page width should be > 0, got {size.Width}");
        Assert.True(size.Height > 0, $"PDF page height should be > 0, got {size.Height}");
        Console.WriteLine($"[INFO] Page 1 dimensions (pts): {size.Width} x {size.Height}");
    }

    [Fact]
    public void Pdf_FirstPage_RendersToImage()
    {
        RequirePdf();

        var bytes = File.ReadAllBytes(TestPdfPath);

        using var skBitmap = Conversion.ToImage(bytes, page: 0);

        Assert.NotNull(skBitmap);
        Assert.True(skBitmap.Width  > 0, $"Rendered bitmap width should be > 0, got {skBitmap.Width}");
        Assert.True(skBitmap.Height > 0, $"Rendered bitmap height should be > 0, got {skBitmap.Height}");
        Console.WriteLine($"[INFO] Rendered bitmap size: {skBitmap.Width} x {skBitmap.Height} px");
    }

    [Fact]
    public void Pdf_FirstPage_RendersToValidPngStream()
    {
        RequirePdf();

        var bytes = File.ReadAllBytes(TestPdfPath);

        using var skBitmap = Conversion.ToImage(bytes, page: 0);
        using var skImage  = skBitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var ms       = new MemoryStream();
        skImage.SaveTo(ms);

        Assert.True(ms.Length > 0, "PNG stream should not be empty");
        Console.WriteLine($"[INFO] PNG stream size: {ms.Length / 1024} KB");
    }

    [Fact]
    public void Pdf_AllPages_CanBeRendered()
    {
        RequirePdf();

        var bytes     = File.ReadAllBytes(TestPdfPath);
        var pageCount = Conversion.GetPageCount(bytes);

        var results = new List<(int Page, int Width, int Height)>();
        for (int i = 0; i < pageCount; i++)
        {
            using var bmp = Conversion.ToImage(bytes, page: i);
            Assert.True(bmp.Width  > 0);
            Assert.True(bmp.Height > 0);
            results.Add((i + 1, bmp.Width, bmp.Height));
        }

        foreach (var r in results)
            Console.WriteLine($"[INFO] Page {r.Page}: {r.Width} x {r.Height} px");

        Assert.Equal(pageCount, results.Count);
    }

    // ── PdfUnderlay model round-trip ─────────────────────────────────────────

    [Fact]
    public void PdfUnderlay_DefaultsAndCalibration_Workflow()
    {
        RequirePdf();

        var underlay = new PdfUnderlay
        {
            FilePath   = TestPdfPath,
            PageNumber = 1
        };

        // Defaults
        Assert.Equal(0.5,  underlay.Opacity);
        Assert.Equal(1.0,  underlay.Scale);
        Assert.Equal(0.0,  underlay.OffsetX);
        Assert.Equal(0.0,  underlay.OffsetY);
        Assert.False(underlay.IsCalibrated);
        Assert.Equal(1.0,  underlay.PixelsPerUnit);
        Assert.Equal(0.0,  underlay.RotationDegrees);

        // Simulate calibration workflow
        var service = new PdfCalibrationService();
        var result  = service.Calibrate(
            new System.Windows.Point(0, 0),
            new System.Windows.Point(960, 0),
            40.0); // 40 ft spans 960 px → 24 px/ft

        Assert.True(result.IsValid);
        Assert.Equal(24.0, result.PixelsPerUnit, precision: 6);

        underlay.IsCalibrated   = true;
        underlay.PixelsPerUnit  = result.PixelsPerUnit;

        Assert.True(underlay.IsCalibrated);
        Assert.Equal(24.0, underlay.PixelsPerUnit, precision: 6);
        Console.WriteLine($"[INFO] Calibration: {result.PixelsPerUnit} px/unit over {result.PixelDistance} px");
    }

    // ── Page number bounds ────────────────────────────────────────────────────

    [Fact]
    public void Pdf_InvalidPageNumber_ThrowsOrHandled()
    {
        RequirePdf();

        var bytes     = File.ReadAllBytes(TestPdfPath);
        var pageCount = Conversion.GetPageCount(bytes);

        // Attempt to render a page beyond the document length
        Assert.ThrowsAny<Exception>(() =>
        {
            using var _ = Conversion.ToImage(bytes, page: pageCount); // 0-based, so pageCount is 1 past the end
        });
    }
}


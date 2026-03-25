using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Microsoft.Win32;
using PDFtoImage;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private BitmapSource? _cachedPdfBitmap;
    private string? _cachedPdfPath;
    private int _cachedPdfPage = -1;
    private ModelVisual3D? _pdfUnderlayVisual3D;
    private const double PdfUnderlayPlaneY = -0.02;

    /// <summary>
    /// Auto-fits the imported PDF/image so it fits within the 2000x2000 design canvas.
    /// Also resets the canvas zoom (PlanCanvasScale) to 1 so the full drawing is visible.
    /// </summary>
    private void AutoFitPdfScale(PdfUnderlay underlay, string filePath)
    {
        const double canvasSize = 2000.0;
        const double margin = 0.95;

        try
        {
            int imgWidth;
            int imgHeight;
            var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".pdf")
            {
                var bytes = System.IO.File.ReadAllBytes(filePath);
                var size = Conversion.GetPageSize(bytes, page: Math.Max(0, underlay.PageNumber - 1));
                const double renderDpi = 300.0;
                imgWidth = (int)(size.Width * renderDpi / 72.0);
                imgHeight = (int)(size.Height * renderDpi / 72.0);
            }
            else
            {
                using var fs = System.IO.File.OpenRead(filePath);
                var decoder = BitmapDecoder.Create(
                    fs,
                    BitmapCreateOptions.DelayCreation,
                    BitmapCacheOption.None);
                var frame = decoder.Frames[0];
                imgWidth = frame.PixelWidth;
                imgHeight = frame.PixelHeight;
            }

            if (imgWidth <= 0 || imgHeight <= 0)
                return;

            double fitScale = canvasSize * margin / Math.Max(imgWidth, imgHeight);
            underlay.Scale = Math.Round(fitScale, 6);
            underlay.IsCalibrated = false;
            underlay.PixelsPerUnit = 1.0;
            underlay.ScaleX = 1.0;
            underlay.ScaleY = 1.0;
            CancelPdfCalibrationMode(logCancellation: false);

            PlanCanvasScale.ScaleX = 1.0;
            PlanCanvasScale.ScaleY = 1.0;

            ActionLogService.Instance.Log(LogCategory.View, "PDF auto-fitted",
                $"Image: {imgWidth}x{imgHeight} pts, FitScale: {underlay.Scale:F6}");
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.Log(LogCategory.Error, "AutoFitPdfScale failed", ex.Message);
        }
    }

    private void InvalidatePdfCache()
    {
        _cachedPdfBitmap = null;
        _cachedPdfPath = null;
        _cachedPdfPage = -1;
    }

    private BitmapSource? GetUnderlayBitmap()
    {
        if (_viewModel.PdfUnderlay == null || string.IsNullOrEmpty(_viewModel.PdfUnderlay.FilePath))
            return null;

        if (!System.IO.File.Exists(_viewModel.PdfUnderlay.FilePath))
            return null;

        var filePath = _viewModel.PdfUnderlay.FilePath;
        var pageNumber = Math.Max(0, _viewModel.PdfUnderlay.PageNumber - 1);

        if (_cachedPdfBitmap != null && _cachedPdfPath == filePath && _cachedPdfPage == pageNumber)
            return _cachedPdfBitmap;

        BitmapSource? bitmap = null;
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

        if (extension == ".pdf")
        {
            var pdfBytes = System.IO.File.ReadAllBytes(filePath);
            using var skBitmap = Conversion.ToImage(pdfBytes, page: pageNumber);
            using var image = skBitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var memStream = new System.IO.MemoryStream();

            image.SaveTo(memStream);
            memStream.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = memStream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            bitmap = bitmapImage;
        }
        else if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".bmp")
        {
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            bitmap = bitmapImage;
        }

        _cachedPdfBitmap = bitmap;
        _cachedPdfPath = filePath;
        _cachedPdfPage = pageNumber;
        return bitmap;
    }

    private static Point RotateCanvasPoint(Point point, double angleDegrees)
    {
        if (Math.Abs(angleDegrees) < 0.001)
            return point;

        var radians = angleDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        return new Point(
            (point.X * cos) - (point.Y * sin),
            (point.X * sin) + (point.Y * cos));
    }

    private static Point[] BuildUnderlayCanvasCorners(PdfUnderlay underlay, double scaledWidth, double scaledHeight)
    {
        var localTopLeft = new Point(0, 0);
        var localTopRight = new Point(scaledWidth, 0);
        var localBottomRight = new Point(scaledWidth, scaledHeight);
        var localBottomLeft = new Point(0, scaledHeight);

        var topLeftCanvas = RotateCanvasPoint(localTopLeft, underlay.RotationDegrees);
        topLeftCanvas.Offset(underlay.OffsetX, underlay.OffsetY);

        var topRightCanvas = RotateCanvasPoint(localTopRight, underlay.RotationDegrees);
        topRightCanvas.Offset(underlay.OffsetX, underlay.OffsetY);

        var bottomRightCanvas = RotateCanvasPoint(localBottomRight, underlay.RotationDegrees);
        bottomRightCanvas.Offset(underlay.OffsetX, underlay.OffsetY);

        var bottomLeftCanvas = RotateCanvasPoint(localBottomLeft, underlay.RotationDegrees);
        bottomLeftCanvas.Offset(underlay.OffsetX, underlay.OffsetY);

        return
        [
            topLeftCanvas,
            topRightCanvas,
            bottomRightCanvas,
            bottomLeftCanvas
        ];
    }

    private bool TryGetUnderlayCanvasFrame(out UnderlayCanvasFrame frame)
    {
        frame = default;

        try
        {
            var underlay = _viewModel.PdfUnderlay;
            if (underlay == null)
                return false;

            var bitmap = GetUnderlayBitmap();
            if (bitmap == null || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
                return false;

            var scaledWidth = bitmap.PixelWidth * underlay.Scale;
            var scaledHeight = bitmap.PixelHeight * underlay.Scale;
            if (scaledWidth <= 0 || scaledHeight <= 0)
                return false;

            frame = new UnderlayCanvasFrame(
                underlay,
                scaledWidth,
                scaledHeight,
                BuildUnderlayCanvasCorners(underlay, scaledWidth, scaledHeight));

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void AddPdfUnderlayToViewport()
    {
        try
        {
            if (!TryGetUnderlayCanvasFrame(out var frame))
                return;

            var bitmap = GetUnderlayBitmap();
            if (bitmap == null)
                return;

            var topLeftWorld = CanvasToWorld(frame.Corners[0]);
            topLeftWorld.Y = PdfUnderlayPlaneY;
            var topRightWorld = CanvasToWorld(frame.Corners[1]);
            topRightWorld.Y = PdfUnderlayPlaneY;
            var bottomRightWorld = CanvasToWorld(frame.Corners[2]);
            bottomRightWorld.Y = PdfUnderlayPlaneY;
            var bottomLeftWorld = CanvasToWorld(frame.Corners[3]);
            bottomLeftWorld.Y = PdfUnderlayPlaneY;

            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection
                {
                    topLeftWorld,
                    topRightWorld,
                    bottomRightWorld,
                    bottomLeftWorld
                },
                TextureCoordinates = new PointCollection
                {
                    new Point(0, 0),
                    new Point(1, 0),
                    new Point(1, 1),
                    new Point(0, 1)
                },
                TriangleIndices = new Int32Collection
                {
                    0, 1, 2,
                    0, 2, 3
                }
            };

            var brush = new ImageBrush(bitmap)
            {
                Opacity = frame.Underlay.Opacity,
                Stretch = Stretch.Fill
            };
            var material = new DiffuseMaterial(brush);

            var model = new GeometryModel3D(mesh, material)
            {
                BackMaterial = material
            };

            _pdfUnderlayVisual3D = new ModelVisual3D
            {
                Content = model
            };

            Viewport.Children.Add(_pdfUnderlayVisual3D);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ActionLogService.Instance.Log(LogCategory.Error, "Invalid page number for 3D PDF underlay",
                $"Page {_viewModel.PdfUnderlay?.PageNumber ?? 1} does not exist in the document. {ex.Message}");
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.Log(LogCategory.Error, "Failed to render 3D PDF/Image underlay", ex.Message);
        }
    }

    private void DrawPdfUnderlay()
    {
        if (_viewModel.PdfUnderlay == null)
            return;

        try
        {
            var bitmap = GetUnderlayBitmap();
            if (bitmap != null)
            {
                var image = new Image
                {
                    Source = bitmap,
                    Opacity = _viewModel.PdfUnderlay.Opacity,
                    Stretch = Stretch.None,
                    RenderTransformOrigin = new Point(0, 0)
                };

                var transformGroup = new TransformGroup();
                var scale = _viewModel.PdfUnderlay.Scale;
                transformGroup.Children.Add(new ScaleTransform(scale, scale));

                if (_viewModel.PdfUnderlay.RotationDegrees != 0)
                {
                    transformGroup.Children.Add(new RotateTransform(_viewModel.PdfUnderlay.RotationDegrees));
                }

                image.RenderTransform = transformGroup;

                Canvas.SetLeft(image, _viewModel.PdfUnderlay.OffsetX);
                Canvas.SetTop(image, _viewModel.PdfUnderlay.OffsetY);

                PlanCanvas.Children.Insert(0, image);
            }
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ActionLogService.Instance.Log(LogCategory.Error, "Invalid page number for PDF underlay",
                $"Page {_viewModel.PdfUnderlay.PageNumber} does not exist in the document. {ex.Message}");
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.Log(LogCategory.Error, "Failed to render PDF/Image underlay", ex.Message);
        }
    }

    private void ImportPdf_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Import PDF dialog requested");
        var dialog = new OpenFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf|Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|All Files (*.*)|*.*",
            Title = "Import PDF/Image Underlay"
        };

        if (dialog.ShowDialog() == true)
        {
            var underlay = new PdfUnderlay
            {
                FilePath = dialog.FileName,
                Opacity = PdfOpacitySlider.Value,
                IsLocked = PdfLockCheck.IsChecked ?? true
            };

            try
            {
                var ext = System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
                if (ext == ".pdf")
                {
                    var bytes = System.IO.File.ReadAllBytes(dialog.FileName);
                    underlay.TotalPageCount = Conversion.GetPageCount(bytes);
                }
            }
            catch
            {
            }

            AutoFitPdfScale(underlay, dialog.FileName);

            _viewModel.PdfUnderlay = underlay;
            SyncPdfCalibrationState();
            UpdatePdfPageIndicator();
            ActionLogService.Instance.Log(LogCategory.FileOperation, "PDF imported",
                $"File: {dialog.FileName}, Pages: {underlay.TotalPageCount}, FitScale: {underlay.Scale:F4}");

            Update2DCanvas();
            UpdateViewport();

            MessageBox.Show($"Underlay imported: {System.IO.Path.GetFileName(dialog.FileName)}\n" +
                $"Auto-fitted to canvas (scale {underlay.Scale:F4}).\n" +
                "Use 'Calibrate Scale' to set the real-world drawing scale.",
                "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void PdfOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_viewModel?.PdfUnderlay != null)
        {
            ActionLogService.Instance.Log(LogCategory.View, "PDF opacity changed", $"Value: {e.NewValue:F2}");
            _viewModel.PdfUnderlay.Opacity = e.NewValue;
            Update2DCanvas();
            UpdateViewport();
        }
    }

    private void PdfLock_Changed(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.PdfUnderlay != null)
        {
            var locked = PdfLockCheck.IsChecked ?? true;
            ActionLogService.Instance.Log(LogCategory.View, "PDF lock changed", $"Locked: {locked}");
            _viewModel.PdfUnderlay.IsLocked = locked;
        }
    }

    private void SyncPdfCalibrationState()
    {
        var transform = SkiaBackground.DrawingContext.CoordTransform;
        _viewModel.DimensionService.UnitSuffix = _viewModel.UnitSystemName == "Metric" ? " m" : " ft";

        if (_viewModel.PdfUnderlay is not { IsCalibrated: true } underlay ||
            underlay.ScaleX <= 0 ||
            underlay.ScaleY <= 0)
        {
            transform.DocUnitsPerRealX = 1.0;
            transform.DocUnitsPerRealY = 1.0;
            transform.IsCalibrated = false;
            _viewModel.DimensionService.ScaleFactor = 1.0;
            return;
        }

        transform.DocUnitsPerRealX = underlay.ScaleX;
        transform.DocUnitsPerRealY = underlay.ScaleY;
        transform.IsCalibrated = true;

        var averageScale = (underlay.ScaleX + underlay.ScaleY) * 0.5;
        _viewModel.DimensionService.ScaleFactor = averageScale > 0
            ? 1.0 / averageScale
            : 1.0;
    }

    private bool TryGetUnderlayDocumentPoint(Point canvasPoint, out Point documentPoint)
    {
        documentPoint = default;

        var underlay = _viewModel.PdfUnderlay;
        var bitmap = GetUnderlayBitmap();
        if (underlay == null || bitmap == null || underlay.Scale <= 0)
            return false;

        var localPoint = new Point(canvasPoint.X - underlay.OffsetX, canvasPoint.Y - underlay.OffsetY);
        var unrotatedPoint = RotateCanvasPoint(localPoint, -underlay.RotationDegrees);
        documentPoint = new Point(unrotatedPoint.X / underlay.Scale, unrotatedPoint.Y / underlay.Scale);

        return documentPoint.X >= 0 &&
               documentPoint.Y >= 0 &&
               documentPoint.X <= bitmap.PixelWidth &&
               documentPoint.Y <= bitmap.PixelHeight;
    }

    private void BeginPdfCalibrationMode()
    {
        _isPdfCalibrationMode = true;
        _pdfCalibrationFirstCanvasPoint = null;
        _pdfCalibrationFirstDocumentPoint = null;
        ClearPdfCalibrationPreview();
        UpdatePlanCanvasCursor();
    }

    private void CancelPdfCalibrationMode(bool logCancellation = true)
    {
        _isPdfCalibrationMode = false;
        _pdfCalibrationFirstCanvasPoint = null;
        _pdfCalibrationFirstDocumentPoint = null;
        ClearPdfCalibrationPreview();
        UpdatePlanCanvasCursor();

        if (logCancellation)
            ActionLogService.Instance.Log(LogCategory.View, "PDF calibration cancelled");
    }

    private void ClearPdfCalibrationPreview()
    {
        if (_pdfCalibrationPreviewLine != null)
        {
            PlanCanvas.Children.Remove(_pdfCalibrationPreviewLine);
            _pdfCalibrationPreviewLine = null;
        }

        if (_pdfCalibrationStartMarker != null)
        {
            PlanCanvas.Children.Remove(_pdfCalibrationStartMarker);
            _pdfCalibrationStartMarker = null;
        }
    }

    private void UpdatePdfCalibrationPreview(Point currentCanvasPoint)
    {
        if (!_isPdfCalibrationMode || _pdfCalibrationFirstCanvasPoint is not { } startCanvasPoint)
            return;

        if (_pdfCalibrationPreviewLine == null)
        {
            _pdfCalibrationPreviewLine = new Line
            {
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 2.0,
                StrokeDashArray = new DoubleCollection { 6, 3 },
                IsHitTestVisible = false
            };
            Panel.SetZIndex(_pdfCalibrationPreviewLine, 450);
            PlanCanvas.Children.Add(_pdfCalibrationPreviewLine);
        }

        _pdfCalibrationPreviewLine.X1 = startCanvasPoint.X;
        _pdfCalibrationPreviewLine.Y1 = startCanvasPoint.Y;
        _pdfCalibrationPreviewLine.X2 = currentCanvasPoint.X;
        _pdfCalibrationPreviewLine.Y2 = currentCanvasPoint.Y;
    }

    private void MarkPdfCalibrationStart(Point canvasPoint)
    {
        if (_pdfCalibrationStartMarker == null)
        {
            _pdfCalibrationStartMarker = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Brushes.OrangeRed,
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };
            Panel.SetZIndex(_pdfCalibrationStartMarker, 451);
            PlanCanvas.Children.Add(_pdfCalibrationStartMarker);
        }

        Canvas.SetLeft(_pdfCalibrationStartMarker, canvasPoint.X - (_pdfCalibrationStartMarker.Width * 0.5));
        Canvas.SetTop(_pdfCalibrationStartMarker, canvasPoint.Y - (_pdfCalibrationStartMarker.Height * 0.5));
        UpdatePdfCalibrationPreview(canvasPoint);
    }

    private bool HandlePdfCalibrationCanvasClick(Point canvasPoint)
    {
        if (!_isPdfCalibrationMode)
            return false;

        if (!TryGetUnderlayDocumentPoint(canvasPoint, out var documentPoint))
        {
            MessageBox.Show(
                "Pick calibration points directly on the PDF underlay.",
                "Calibration Point Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return true;
        }

        if (_pdfCalibrationFirstDocumentPoint == null || _pdfCalibrationFirstCanvasPoint == null)
        {
            _pdfCalibrationFirstDocumentPoint = documentPoint;
            _pdfCalibrationFirstCanvasPoint = canvasPoint;
            MarkPdfCalibrationStart(canvasPoint);

            ActionLogService.Instance.Log(LogCategory.View, "PDF calibration first point picked",
                $"Canvas: ({canvasPoint.X:F1}, {canvasPoint.Y:F1}), Document: ({documentPoint.X:F1}, {documentPoint.Y:F1})");
            return true;
        }

        var input = PromptInput(
            "Calibrate Scale",
            "Enter the real-world distance between the two picked points.",
            "10'-0\"");

        if (string.IsNullOrWhiteSpace(input))
        {
            CancelPdfCalibrationMode();
            return true;
        }

        double knownDistance;
        try
        {
            knownDistance = ParseLengthInput(input, "Calibration distance");
        }
        catch (FormatException ex)
        {
            CancelPdfCalibrationMode(logCancellation: false);
            MessageBox.Show(ex.Message, "Invalid Distance", MessageBoxButton.OK, MessageBoxImage.Warning);
            return true;
        }

        var result = _viewModel.CalibrationService.Calibrate(_pdfCalibrationFirstDocumentPoint.Value, documentPoint, knownDistance);
        if (!result.IsValid || _viewModel.PdfUnderlay == null)
        {
            CancelPdfCalibrationMode(logCancellation: false);
            MessageBox.Show(
                "Calibration failed. Pick two distinct points and enter a positive real-world distance.",
                "Calibration Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return true;
        }

        _viewModel.PdfUnderlay.IsCalibrated = true;
        _viewModel.PdfUnderlay.PixelsPerUnit = result.PixelsPerUnit;
        _viewModel.PdfUnderlay.ScaleX = result.PixelsPerUnit;
        _viewModel.PdfUnderlay.ScaleY = result.PixelsPerUnit;
        _viewModel.PdfUnderlay.Scale = CanvasWorldScale / result.PixelsPerUnit;
        SyncPdfCalibrationState();

        ActionLogService.Instance.Log(LogCategory.View, "PDF calibrated",
            $"PixelsPerUnit: {result.PixelsPerUnit:F4}, CanvasScale: {_viewModel.PdfUnderlay.Scale:F4}, PixelDistance: {result.PixelDistance:F2}, RealDistance: {knownDistance:F4}");

        CancelPdfCalibrationMode(logCancellation: false);
        Update2DCanvas();
        UpdateViewport();

        var unitsLabel = _viewModel.UnitSystemName == "Metric" ? "m" : "ft";
        MessageBox.Show(
            $"Underlay calibrated.\n\nDrawing scale: {result.PixelsPerUnit:F3} px/{unitsLabel}\nMeasured span: {knownDistance:F3} {unitsLabel}",
            "Calibration Complete",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        return true;
    }

    private void CalibrateScale_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.View, "Calibrate scale requested");
        if (_viewModel.PdfUnderlay == null)
        {
            MessageBox.Show("Please import a PDF underlay first.", "No Underlay",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ExitConflictingAuthoringModes();
        CancelPendingPlacement(logCancellation: false);
        BeginPdfCalibrationMode();

        MessageBox.Show("PDF Scale Calibration:\n\n" +
            "1. Click the first known point on the PDF.\n" +
            "2. Click the second point.\n" +
            "3. Enter the real-world distance.\n\n" +
            "Use a long, reliable span such as gridline spacing, wall-to-wall dimension, or equipment layout control points.",
            "Calibrate Scale", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PdfPrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.PdfUnderlay is not { TotalPageCount: > 1 } pdf)
            return;
        if (pdf.PageNumber <= 1)
            return;

        pdf.PageNumber--;
        AutoFitPdfScale(pdf, pdf.FilePath);
        SyncPdfCalibrationState();
        UpdatePdfPageIndicator();
        _cachedPdfPage = -1;
        Update2DCanvas();
        UpdateViewport();
    }

    private void PdfNextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.PdfUnderlay is not { TotalPageCount: > 1 } pdf)
            return;
        if (pdf.PageNumber >= pdf.TotalPageCount)
            return;

        pdf.PageNumber++;
        AutoFitPdfScale(pdf, pdf.FilePath);
        SyncPdfCalibrationState();
        UpdatePdfPageIndicator();
        _cachedPdfPage = -1;
        Update2DCanvas();
        UpdateViewport();
    }

    private void UpdatePdfPageIndicator()
    {
        var pdf = _viewModel.PdfUnderlay;
        if (pdf is null)
        {
            PdfPageIndicator.Text = "1 / 1";
            return;
        }

        PdfPageIndicator.Text = $"{pdf.PageNumber} / {pdf.TotalPageCount}";
    }
}

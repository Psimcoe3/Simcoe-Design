using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ElectricalComponentSandbox.Rendering;
using Microsoft.Win32;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Self-contained WPF window that previews a rendered spool sheet on a
/// SkiaSharp-backed canvas. Provides a toolbar with Fit / Zoom / Print /
/// Export-XPS / Export-PDF commands so a shop technician can review and
/// route a spool to fabrication without leaving the app.
///
/// Constructed entirely in code — no XAML resource — so it can be opened
/// from anywhere (MainViewModel, an unrelated command, or a unit test) and
/// edited without breaking the project's XAML compile.
/// </summary>
public sealed class SpoolSheetPreviewWindow : Window
{
    private readonly SpoolSheetRenderGeometry _geometry;
    private readonly SkiaCanvasHost _canvasHost;
    private readonly SpoolSheetCanvasPainter _painter;
    private bool _initialFitDone;

    /// <summary>Margin in document units (inches) when fitting to view.</summary>
    public double FitMarginInches { get; init; } = 0.5;

    /// <summary>Zoom step applied by the ± toolbar buttons.</summary>
    public double ZoomStep { get; init; } = 1.25;

    public SpoolSheetPreviewWindow(SpoolSheetRenderGeometry geometry, string? suggestedFileName = null)
    {
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        SuggestedFileName = string.IsNullOrWhiteSpace(suggestedFileName)
            ? "spool-sheet"
            : suggestedFileName!.Trim();

        Title = $"Spool Sheet Preview — {SuggestedFileName}";
        Width = 1100;
        Height = 750;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _canvasHost = new SkiaCanvasHost();
        _canvasHost.RenderFrame += OnRender;
        _canvasHost.SizeChanged += OnCanvasSizeChanged;
        _canvasHost.MouseWheel += OnMouseWheel;

        _painter = new SpoolSheetCanvasPainter();

        var root = new DockPanel();
        DockPanel.SetDock(BuildToolBar(), Dock.Top);
        root.Children.Add(BuildToolBar());
        root.Children.Add(_canvasHost);
        Content = root;
    }

    /// <summary>Base filename (without extension) used by export dialogs.</summary>
    public string SuggestedFileName { get; }

    // ── Toolbar ──────────────────────────────────────────────────────────

    private ToolBar BuildToolBar()
    {
        var tb = new ToolBar();
        tb.Items.Add(MakeButton("Fit", "Fit sheet to view", FitToView));
        tb.Items.Add(MakeButton("+",   "Zoom in",  () => ZoomBy(ZoomStep)));
        tb.Items.Add(MakeButton("−",   "Zoom out", () => ZoomBy(1.0 / ZoomStep)));
        tb.Items.Add(new Separator());
        tb.Items.Add(MakeButton("Print…",      "Print to selected printer", Print));
        tb.Items.Add(MakeButton("Export XPS…", "Save as XPS document",      ExportXps));
        tb.Items.Add(MakeButton("Export PDF…", "Save as PDF document",      ExportPdf));
        return tb;
    }

    private static Button MakeButton(string text, string tooltip, Action onClick)
    {
        var b = new Button { Content = text, ToolTip = tooltip, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(2) };
        b.Click += (_, _) => onClick();
        return b;
    }

    // ── Canvas rendering ─────────────────────────────────────────────────

    private void OnRender(ICanvas2DRenderer canvas)
    {
        _painter.Paint(canvas, _geometry);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initialFitDone && _canvasHost.ActualWidth > 0 && _canvasHost.ActualHeight > 0)
        {
            FitToView();
            _initialFitDone = true;
        }
        _canvasHost.RequestRedraw();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var anchor = e.GetPosition(_canvasHost);
        double factor = e.Delta > 0 ? ZoomStep : 1.0 / ZoomStep;
        _canvasHost.DrawingContext.ZoomAbout(anchor, factor);
        _canvasHost.RequestRedraw();
        e.Handled = true;
    }

    // ── View manipulation ────────────────────────────────────────────────

    public void FitToView()
    {
        if (_canvasHost.ActualWidth <= 0 || _canvasHost.ActualHeight <= 0)
            return;

        double margin = Math.Max(0, FitMarginInches);
        double docWidth = _geometry.PaperWidthInches + 2 * margin;
        double docHeight = _geometry.PaperHeightInches + 2 * margin;

        double zoomX = _canvasHost.ActualWidth / docWidth;
        double zoomY = _canvasHost.ActualHeight / docHeight;
        double zoom = Math.Min(zoomX, zoomY);

        var ctx = _canvasHost.DrawingContext;
        ctx.Zoom = zoom;
        ctx.PanX = (_canvasHost.ActualWidth - _geometry.PaperWidthInches * zoom) / 2.0;
        ctx.PanY = (_canvasHost.ActualHeight - _geometry.PaperHeightInches * zoom) / 2.0;
        ctx.SyncCoordTransform();
        _canvasHost.RequestRedraw();
    }

    public void ZoomBy(double factor)
    {
        var center = new Point(_canvasHost.ActualWidth / 2.0, _canvasHost.ActualHeight / 2.0);
        _canvasHost.DrawingContext.ZoomAbout(center, factor);
        _canvasHost.RequestRedraw();
    }

    // ── Export / print ───────────────────────────────────────────────────

    private void Print()
    {
        var dialog = new System.Windows.Controls.PrintDialog();
        if (dialog.ShowDialog() != true) return;

        var exporter = new SpoolSheetXpsExporter();
        var document = exporter.BuildFixedDocument(_geometry);
        dialog.PrintDocument(document.DocumentPaginator, SuggestedFileName);
    }

    private void ExportXps()
    {
        var dialog = new SaveFileDialog
        {
            FileName = SuggestedFileName,
            DefaultExt = ".xps",
            Filter = "XPS document (*.xps)|*.xps",
        };
        if (dialog.ShowDialog(this) != true) return;

        new SpoolSheetXpsExporter().SaveToFile(_geometry, dialog.FileName);
    }

    private void ExportPdf()
    {
        var dialog = new SaveFileDialog
        {
            FileName = SuggestedFileName,
            DefaultExt = ".pdf",
            Filter = "PDF document (*.pdf)|*.pdf",
        };
        if (dialog.ShowDialog(this) != true) return;

        new SpoolSheetPdfExporter().SaveToFile(_geometry, dialog.FileName);
    }
}

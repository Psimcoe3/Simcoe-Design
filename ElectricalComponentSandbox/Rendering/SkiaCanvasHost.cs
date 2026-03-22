using System.Windows;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using ElectricalComponentSandbox.Markup.Services;

namespace ElectricalComponentSandbox.Rendering;

/// <summary>
/// WPF FrameworkElement hosting a SkiaSharp SKElement.
/// Acts as the replacement for the old ConduitVisualHost + PlanCanvas pair.
/// Exposes the <see cref="ICanvas2DRenderer"/> for all draw calls and manages
/// the coordinate transform (pan, zoom) via the embedded <see cref="DrawingContext2D"/>.
/// </summary>
public sealed class SkiaCanvasHost : System.Windows.FrameworkElement
{
    private readonly SKElement _skElement;
    private SkiaCanvas2DRenderer? _renderer;
    private readonly DirtyRectTracker _dirtyTracker = new();
    private readonly TileCacheService _tileCache = new();

    public DrawingContext2D DrawingContext { get; } = new();

    public ICanvas2DRenderer? Renderer => _renderer;

    /// <summary>
    /// Subscribe to receive draw calls each frame.
    /// The callback receives the renderer ready to accept draw calls.
    /// </summary>
    public event Action<ICanvas2DRenderer>? RenderFrame;

    public SkiaCanvasHost()
    {
        _skElement = new SKElement
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
        };
        _skElement.PaintSurface += OnPaintSurface;

        // Use a single-child logical host pattern
        AddLogicalChild(_skElement);
        AddVisualChild(_skElement);
    }

    // ── Layout & visual tree ──────────────────────────────────────────────────

    protected override int VisualChildrenCount => 1;

    protected override System.Windows.Media.Visual GetVisualChild(int index)
    {
        if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
        return _skElement;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _skElement.Measure(availableSize);
        return _skElement.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _skElement.Arrange(new Rect(finalSize));
        return finalSize;
    }

    // ── Public surface ────────────────────────────────────────────────────────

    /// <summary>Triggers a full repaint on the next WPF render pass</summary>
    public void RequestRedraw() => _skElement.InvalidateVisual();

    /// <summary>Marks a document-space region as dirty and schedules a repaint</summary>
    public void InvalidateRegion(Rect docRect)
    {
        _dirtyTracker.MarkDirty(docRect);
        _skElement.InvalidateVisual();
    }

    /// <summary>Invalidates the PDF tile cache (e.g. after zoom change)</summary>
    public void InvalidateTileCache()
    {
        _tileCache.InvalidateAll();
        _skElement.InvalidateVisual();
    }

    /// <summary>Initializes the tile grid for a given PDF underlay size</summary>
    public void InitializeTileCache(double underlayWidth, double underlayHeight)
        => _tileCache.Initialize(underlayWidth, underlayHeight);

    /// <summary>Gets visible tiles in the current viewport; callers skip tiles where IsTileValid == true</summary>
    public IEnumerable<(int Col, int Row, System.Windows.Rect TileRect)> GetVisibleTiles()
        => _tileCache.GetVisibleTiles(DrawingContext.ViewportDocRect);

    /// <summary>Marks a tile as rendered so it can be skipped next frame</summary>
    public void MarkTileValid(int col, int row) => _tileCache.MarkTileValid(col, row);

    /// <summary>Whether a tile has been rendered and is still valid</summary>
    public bool IsTileValid(int col, int row) => _tileCache.IsTileValid(col, row);

    // ── SkiaSharp paint ───────────────────────────────────────────────────────

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        // Sync DrawingContext size
        DrawingContext.ViewportWidth = info.Width;
        DrawingContext.ViewportHeight = info.Height;

        // Build renderer if not yet created or canvas changed
        if (_renderer is null || _renderer.Canvas != canvas)
        {
            _renderer = new SkiaCanvas2DRenderer(canvas, DrawingContext);
        }
        else
        {
            _renderer.UpdateCanvas(canvas, DrawingContext);
        }

        // Apply dirty-rect clip region when incremental updates are available
        var dirtyRects = _dirtyTracker.FlushDirtyRects();
        if (dirtyRects.Count > 0)
        {
            canvas.Save();
            using var clipRegion = new SKRegion();
            foreach (var dr in dirtyRects)
            {
                // Convert document-space dirty rect to screen-space clip
                var tl = DrawingContext.DocumentToScreen(dr.TopLeft);
                var br = DrawingContext.DocumentToScreen(dr.BottomRight);
                clipRegion.Op(
                    SKRectI.Round(new SKRect((float)tl.X, (float)tl.Y, (float)br.X, (float)br.Y)),
                    SKRegionOperation.Union);

                // Invalidate tile cache for overlapping tiles
                _tileCache.InvalidateRect(dr);
            }
            canvas.ClipRegion(clipRegion);
        }

        // Clear background
        _renderer.Clear("#FF1A1A2E");

        // Raise draw event — callers do all geometry draw calls here
        RenderFrame?.Invoke(_renderer);

        // Restore clip if we applied dirty-rect clipping
        if (dirtyRects.Count > 0)
        {
            canvas.Restore();
        }
    }
}

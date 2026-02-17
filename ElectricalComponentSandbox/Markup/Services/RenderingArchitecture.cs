using System.Windows;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Tracks dirty rectangles for incremental rendering.
/// Only regions marked dirty need to be re-rendered.
/// </summary>
public class DirtyRectTracker
{
    private readonly List<Rect> _dirtyRects = new();

    /// <summary>
    /// Marks a region as dirty (needs re-render)
    /// </summary>
    public void MarkDirty(Rect rect)
    {
        _dirtyRects.Add(rect);
    }

    /// <summary>
    /// Marks the entire viewport as dirty
    /// </summary>
    public void MarkAllDirty(double viewWidth, double viewHeight)
    {
        _dirtyRects.Clear();
        _dirtyRects.Add(new Rect(0, 0, viewWidth, viewHeight));
    }

    /// <summary>
    /// Gets and clears the current dirty regions
    /// </summary>
    public IReadOnlyList<Rect> FlushDirtyRects()
    {
        var result = new List<Rect>(_dirtyRects);
        _dirtyRects.Clear();
        return result;
    }

    /// <summary>
    /// Returns the bounding rect of all dirty regions (for single-pass rendering)
    /// </summary>
    public Rect GetDirtyBounds()
    {
        if (_dirtyRects.Count == 0) return Rect.Empty;

        var bounds = _dirtyRects[0];
        for (int i = 1; i < _dirtyRects.Count; i++)
        {
            bounds.Union(_dirtyRects[i]);
        }
        return bounds;
    }

    /// <summary>
    /// Whether any region is dirty
    /// </summary>
    public bool HasDirtyRegions => _dirtyRects.Count > 0;
}

/// <summary>
/// Tile-based cache for large PDF underlay bitmaps.
/// Divides the underlay into tiles and only renders visible ones.
/// </summary>
public class TileCacheService
{
    /// <summary>Tile size in pixels</summary>
    public int TileSize { get; set; } = 256;

    private readonly Dictionary<(int Col, int Row), bool> _tileValid = new();
    private int _cols;
    private int _rows;

    /// <summary>
    /// Initializes the tile grid for a given underlay size
    /// </summary>
    public void Initialize(double underlayWidth, double underlayHeight)
    {
        _cols = (int)Math.Ceiling(underlayWidth / TileSize);
        _rows = (int)Math.Ceiling(underlayHeight / TileSize);
        _tileValid.Clear();
    }

    /// <summary>
    /// Gets the tile indices for tiles visible in the given viewport rect (document space)
    /// </summary>
    public IEnumerable<(int Col, int Row, Rect TileRect)> GetVisibleTiles(Rect viewportDocRect)
    {
        int startCol = Math.Max(0, (int)(viewportDocRect.Left / TileSize));
        int endCol = Math.Min(_cols - 1, (int)(viewportDocRect.Right / TileSize));
        int startRow = Math.Max(0, (int)(viewportDocRect.Top / TileSize));
        int endRow = Math.Min(_rows - 1, (int)(viewportDocRect.Bottom / TileSize));

        for (int c = startCol; c <= endCol; c++)
        {
            for (int r = startRow; r <= endRow; r++)
            {
                var rect = new Rect(c * TileSize, r * TileSize, TileSize, TileSize);
                yield return (c, r, rect);
            }
        }
    }

    /// <summary>
    /// Marks a tile as valid (rendered)
    /// </summary>
    public void MarkTileValid(int col, int row) => _tileValid[(col, row)] = true;

    /// <summary>
    /// Checks if a tile has been rendered
    /// </summary>
    public bool IsTileValid(int col, int row) =>
        _tileValid.TryGetValue((col, row), out var valid) && valid;

    /// <summary>
    /// Invalidates all tiles (force full re-render)
    /// </summary>
    public void InvalidateAll() => _tileValid.Clear();

    /// <summary>
    /// Invalidates tiles that overlap a dirty rect
    /// </summary>
    public void InvalidateRect(Rect dirtyRect)
    {
        foreach (var key in _tileValid.Keys.ToList())
        {
            var tileRect = new Rect(key.Col * TileSize, key.Row * TileSize, TileSize, TileSize);
            if (tileRect.IntersectsWith(dirtyRect))
                _tileValid.Remove(key);
        }
    }

    /// <summary>
    /// Gets the total tile grid size
    /// </summary>
    public (int Columns, int Rows) GetGridSize() => (_cols, _rows);
}

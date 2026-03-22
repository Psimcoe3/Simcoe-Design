using System.Windows;
using System.Windows.Input;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Rendering;

/// <summary>
/// Handles all mouse and keyboard interaction for the 2D SkiaSharp canvas:
///   • Pan (middle-mouse drag)
///   • Zoom (mouse wheel)
///   • Snap-aware cursor tracking
///   • Ortho / polar tracking constraint
///   • Window/crossing selection rubber-band
///   • Grip editing (drag grip points)
///   • Tool dispatch (active drawing tool invocation)
///
/// This class is intentionally free of WPF-specific event hookup so it can
/// be unit-tested independently.  Wire it up in MainWindow.xaml.cs by
/// forwarding mouse/keyboard events to the On* methods.
/// </summary>
public sealed class CanvasInteractionController
{
    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly DrawingContext2D _drawCtx;
    private readonly SnapService _snapService;
    private readonly ShadowGeometryTree _shadowTree;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _isPanning;
    private Point _panStartScreen;
    private double _panStartX, _panStartY;

    private bool _isRubberBanding;
    private Point _rubberStart;
    private Point _rubberEnd;

    private bool _isDraggingGrip;
    private string? _gripComponentId;
    private int _gripPointIndex;
    private Point _gripDragStart;

    // Ortho / polar
    public bool IsOrthoActive { get; set; }
    public bool IsPolarActive { get; set; }
    public double PolarIncrementDeg { get; set; } = 45.0;

    // Active snap result for canvas overlay
    public SnapService.SnapResult? LastSnap { get; private set; }

    // Current constrained cursor in Document space
    public Point CursorDocPoint { get; private set; }

    public bool IsRubberBanding => _isRubberBanding;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when selection should be updated with a new set of IDs</summary>
    public event Action<IReadOnlyList<string>, bool /*crossing*/>? SelectionRectCompleted;

    /// <summary>Fired when a grip drag completes (component id, grip index, delta in doc units)</summary>
    public event Action<string, int, Vector>? GripDragCompleted;

    /// <summary>Fired every time the cursor moves — host should call RequestRedraw</summary>
    public event Action? CursorMoved;

    // ── Constructor ───────────────────────────────────────────────────────────

    public CanvasInteractionController(
        DrawingContext2D drawCtx,
        SnapService snapService,
        ShadowGeometryTree shadowTree)
    {
        _drawCtx = drawCtx;
        _snapService = snapService;
        _shadowTree = shadowTree;
    }

    // ── Mouse events ──────────────────────────────────────────────────────────

    public void OnMouseMove(Point screenPos,
                            IEnumerable<Point> snapEndpoints,
                            IEnumerable<(Point A, Point B)> snapSegments)
    {
        var docPos = _drawCtx.ScreenToDocument(screenPos);

        // Pan
        if (_isPanning)
        {
            double dx = screenPos.X - _panStartScreen.X;
            double dy = screenPos.Y - _panStartScreen.Y;
            _drawCtx.PanX = _panStartX + dx;
            _drawCtx.PanY = _panStartY + dy;
            CursorMoved?.Invoke();
            return;
        }

        // Apply ortho/polar constraint
        docPos = ApplyConstraint(docPos);

        // Snap
        LastSnap = _snapService.FindSnapPoint(docPos,
            snapEndpoints, snapSegments);
        CursorDocPoint = LastSnap.Snapped ? LastSnap.SnappedPoint : docPos;

        // Rubber band
        if (_isRubberBanding)
        {
            _rubberEnd = CursorDocPoint;
        }

        CursorMoved?.Invoke();
    }

    public void OnMouseDown(Point screenPos, MouseButton button, ModifierKeys modifiers)
    {
        var docPos = _drawCtx.ScreenToDocument(screenPos);

        if (button == MouseButton.Middle)
        {
            _isPanning = true;
            _panStartScreen = screenPos;
            _panStartX = _drawCtx.PanX;
            _panStartY = _drawCtx.PanY;
            return;
        }

        if (button == MouseButton.Left)
        {
            // Check grip hit first
            var gripHit = _shadowTree.HitTest(docPos, _drawCtx.ScreenToDocumentDist(6));
            if (gripHit != null && gripHit.Kind == ShadowGeometryTree.ShadowNodeKind.GripPoint)
            {
                _isDraggingGrip = true;
                _gripComponentId = gripHit.Id;
                _gripPointIndex = 0; // TODO: encode grip index in ShadowNode
                _gripDragStart = docPos;
                return;
            }

            // Start rubber band selection
            _isRubberBanding = true;
            _rubberStart = _rubberEnd = docPos;
        }
    }

    public void OnMouseUp(Point screenPos, MouseButton button)
    {
        if (button == MouseButton.Middle)
        {
            _isPanning = false;
            return;
        }

        if (button == MouseButton.Left)
        {
            if (_isDraggingGrip && _gripComponentId != null)
            {
                var docPos = _drawCtx.ScreenToDocument(screenPos);
                var delta = new Vector(docPos.X - _gripDragStart.X, docPos.Y - _gripDragStart.Y);
                GripDragCompleted?.Invoke(_gripComponentId, _gripPointIndex, delta);
                _isDraggingGrip = false;
                _gripComponentId = null;
            }

            if (_isRubberBanding)
            {
                _isRubberBanding = false;
                var selRect = new Rect(_rubberStart, _rubberEnd);
                if (selRect.Width > 2 || selRect.Height > 2) // only if user actually dragged
                {
                    bool crossing = _rubberEnd.X < _rubberStart.X; // right→left = crossing
                    var hits = _shadowTree.QueryRect(selRect, windowOnly: !crossing);
                    SelectionRectCompleted?.Invoke(hits.Select(n => n.Id).ToList(), crossing);
                }
            }
        }
    }

    public void OnMouseWheel(Point screenPos, double delta)
    {
        double factor = delta > 0 ? 1.1 : 1.0 / 1.1;
        _drawCtx.ZoomAbout(screenPos, factor);
        CursorMoved?.Invoke();
    }

    public void ClearPreview()
    {
        LastSnap = null;
        CursorDocPoint = default;
        CursorMoved?.Invoke();
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    public void OnKeyDown(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.F8:
                IsOrthoActive = !IsOrthoActive;
                if (IsOrthoActive) IsPolarActive = false;
                CursorMoved?.Invoke();
                break;

            case Key.F10:
                IsPolarActive = !IsPolarActive;
                if (IsPolarActive) IsOrthoActive = false;
                CursorMoved?.Invoke();
                break;

            case Key.F3:
                _snapService.IsEnabled = !_snapService.IsEnabled;
                if (!_snapService.IsEnabled)
                {
                    LastSnap = null;
                    CursorDocPoint = default;
                }
                break;
        }
    }

    // ── Overlay draw ─────────────────────────────────────────────────────────

    /// <summary>
    /// Draw all interaction overlays (snap glyph, tracking line, rubber band, grips)
    /// onto the renderer.  Call at the end of every RenderFrame pass.
    /// </summary>
    public void DrawOverlays(ICanvas2DRenderer renderer)
    {
        // Snap glyph
        if (LastSnap?.Snapped == true)
        {
            var glyphType = LastSnap.Type switch
            {
                SnapService.SnapType.Endpoint     => SnapGlyphType.Endpoint,
                SnapService.SnapType.Midpoint     => SnapGlyphType.Midpoint,
                SnapService.SnapType.Intersection => SnapGlyphType.Intersection,
                SnapService.SnapType.Grid         => SnapGlyphType.Nearest,
                _                                 => SnapGlyphType.Nearest
            };
            renderer.DrawSnapGlyph(LastSnap.SnappedPoint, glyphType);
        }

        // Ortho / polar tracking line
        if ((IsOrthoActive || IsPolarActive) && CursorDocPoint != default)
        {
            double angle = IsOrthoActive
                ? GetOrthoAngle(CursorDocPoint)
                : GetPolarAngle(CursorDocPoint);
            renderer.DrawTrackingLine(CursorDocPoint, angle);
        }

        // Rubber band selection rect
        if (_isRubberBanding)
        {
            var r = new Rect(_rubberStart, _rubberEnd);
            bool crossing = _rubberEnd.X < _rubberStart.X;
            renderer.DrawSelectionRect(r, crossing);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private Point ApplyConstraint(Point docPos)
    {
        if (!IsOrthoActive && !IsPolarActive) return docPos;
        if (CursorDocPoint == default) return docPos;

        var origin = CursorDocPoint; // last committed point (to be improved with tool integration)
        double dx = docPos.X - origin.X;
        double dy = docPos.Y - origin.Y;
        double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        double len = Math.Sqrt(dx * dx + dy * dy);

        double snappedAngle;
        if (IsOrthoActive)
        {
            // Snap to nearest 90°
            snappedAngle = Math.Round(angle / 90.0) * 90.0;
        }
        else
        {
            // Polar: snap to nearest increment
            snappedAngle = Math.Round(angle / PolarIncrementDeg) * PolarIncrementDeg;
        }

        double rad = snappedAngle * Math.PI / 180.0;
        return new Point(origin.X + Math.Cos(rad) * len, origin.Y + Math.Sin(rad) * len);
    }

    private double GetOrthoAngle(Point pos)
    {
        // Approximate: return whichever cardinal is closest to last movement
        return 0; // Simplified; full impl tracks last tool position
    }

    private double GetPolarAngle(Point pos)
    {
        double dx = pos.X, dy = pos.Y;
        double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        return Math.Round(angle / PolarIncrementDeg) * PolarIncrementDeg;
    }
}

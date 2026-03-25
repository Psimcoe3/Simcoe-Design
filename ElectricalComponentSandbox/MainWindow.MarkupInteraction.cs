using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private readonly MarkupInteractionService _markupInteractionService = new();
    private bool _isDraggingMarkup = false;
    private readonly List<MarkupRecord> _draggedMarkups = new();
    private readonly Dictionary<string, MarkupGeometrySnapshot> _markupDragStartSnapshots = new(StringComparer.Ordinal);

    private bool TryStartMarkupSelectionDrag(Point canvasPoint)
    {
        var hit = _canvasInteractionShadowTree.HitTest(canvasPoint, GetMarkupHitTolerance());
        if (hit?.Kind != ShadowGeometryTree.ShadowNodeKind.Markup || hit.Source is not MarkupRecord markup)
            return false;

        SelectMarkupOnCanvas(markup);

        _isDraggingMarkup = true;
        _lastMousePosition = canvasPoint;
        _dragStartCanvasPosition = canvasPoint;
        _mobileSelectionCandidate = _isMobileView;
        _draggedMarkups.Clear();
        _markupDragStartSnapshots.Clear();

        foreach (var candidate in _markupInteractionService.GetSelectionSet(markup, _viewModel.Markups))
        {
            _draggedMarkups.Add(candidate);
            _markupDragStartSnapshots[candidate.Id] = _markupInteractionService.Capture(candidate);
        }

        PlanCanvas.CaptureMouse();
        SkiaBackground.RequestRedraw();
        return true;
    }

    private void UpdateDraggedMarkupPreview(Point canvasPoint)
    {
        if (!_isDraggingMarkup || _draggedMarkups.Count == 0)
            return;

        BeginFastInteractionMode();
        var delta = canvasPoint - _lastMousePosition;
        if (_mobileSelectionCandidate &&
            (Math.Abs(canvasPoint.X - _dragStartCanvasPosition.X) > 4 || Math.Abs(canvasPoint.Y - _dragStartCanvasPosition.Y) > 4))
        {
            _mobileSelectionCandidate = false;
        }

        if (delta.X == 0 && delta.Y == 0)
            return;

        foreach (var markup in _draggedMarkups)
            _markupInteractionService.Translate(markup, new Vector(delta.X, delta.Y));

        _lastMousePosition = canvasPoint;
        SkiaBackground.RequestRedraw();
    }

    private void FinishMarkupSelectionDrag()
    {
        if (!_isDraggingMarkup)
            return;

        var actions = _draggedMarkups
            .Where(markup => _markupDragStartSnapshots.TryGetValue(markup.Id, out var snapshot) &&
                             !MarkupSnapshotsEqual(snapshot, _markupInteractionService.Capture(markup)))
            .Select(markup => new MoveMarkupGeometryAction(
                _markupInteractionService,
                markup,
                _markupDragStartSnapshots[markup.Id],
                _markupInteractionService.Capture(markup)))
            .Cast<IUndoableAction>()
            .ToList();

        _isDraggingMarkup = false;
        _draggedMarkups.Clear();
        _markupDragStartSnapshots.Clear();

        if (actions.Count > 0)
        {
            _viewModel.UndoRedo.Execute(new CompositeAction("Move markup annotation", actions));
            QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
            ActionLogService.Instance.Log(LogCategory.Edit, "Markup moved", $"Count: {actions.Count}");
        }
        else
        {
            SkiaBackground.RequestRedraw();
        }
    }

    private void SelectMarkupOnCanvas(MarkupRecord markup)
    {
        _selectedSketchPrimitive = null;
        _viewModel.SelectedComponentIds.Clear();
        if (_viewModel.SelectedComponent != null)
            _viewModel.SelectedComponent = null;

        _viewModel.MarkupTool.SelectedMarkup = markup;
    }

    private void ClearMarkupSelection()
    {
        _viewModel.MarkupTool.SelectedMarkup = null;
    }

    private void DrawSelectedMarkupOverlay(ICanvas2DRenderer renderer)
    {
        if (_viewModel.MarkupTool.SelectedMarkup is not { } selectedMarkup)
            return;

        var selectionSet = _markupInteractionService.GetSelectionSet(selectedMarkup, _viewModel.Markups);
        var bounds = _markupInteractionService.GetAggregateBounds(selectionSet);
        if (bounds == Rect.Empty)
            return;

        var highlightStyle = new RenderStyle
        {
            StrokeColor = "#FFFF8A00",
            StrokeWidth = 2.0,
            DashPattern = new[] { 8f, 4f }
        };

        renderer.DrawRect(bounds, highlightStyle);
        renderer.DrawGrip(bounds.TopLeft);
        renderer.DrawGrip(bounds.TopRight);
        renderer.DrawGrip(bounds.BottomLeft);
        renderer.DrawGrip(bounds.BottomRight);
    }

    private double GetMarkupHitTolerance()
    {
        return Math.Max(4.0, 8.0 / Math.Max(PlanCanvasScale.ScaleX, 0.1));
    }

    private static bool MarkupSnapshotsEqual(MarkupGeometrySnapshot left, MarkupGeometrySnapshot right)
    {
        if (left.BoundingRect != right.BoundingRect || left.Vertices.Count != right.Vertices.Count)
            return false;

        for (int i = 0; i < left.Vertices.Count; i++)
        {
            if (left.Vertices[i] != right.Vertices[i])
                return false;
        }

        return true;
    }
}

internal sealed class MoveMarkupGeometryAction : IUndoableAction
{
    private readonly MarkupInteractionService _markupInteractionService;
    private readonly MarkupRecord _markup;
    private readonly MarkupGeometrySnapshot _oldSnapshot;
    private readonly MarkupGeometrySnapshot _newSnapshot;

    public MoveMarkupGeometryAction(
        MarkupInteractionService markupInteractionService,
        MarkupRecord markup,
        MarkupGeometrySnapshot oldSnapshot,
        MarkupGeometrySnapshot newSnapshot)
    {
        _markupInteractionService = markupInteractionService;
        _markup = markup;
        _oldSnapshot = oldSnapshot;
        _newSnapshot = newSnapshot;
    }

    public string Description => $"Move {_markup.TypeDisplayText}";

    public void Execute() => _markupInteractionService.Apply(_markup, _newSnapshot);

    public void Undo() => _markupInteractionService.Apply(_markup, _oldSnapshot);
}

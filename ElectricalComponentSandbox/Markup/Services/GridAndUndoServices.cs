using System.Windows;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Grid service for snap-to-grid functionality
/// </summary>
public class GridService
{
    /// <summary>Grid spacing in document units</summary>
    public double GridSpacing { get; set; } = 10.0;

    /// <summary>Whether grid snapping is enabled</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Whether the grid is visible</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Snaps a document-space point to the nearest grid intersection
    /// </summary>
    public Point SnapToGrid(Point docPoint)
    {
        if (!IsEnabled || GridSpacing <= 0)
            return docPoint;

        double x = Math.Round(docPoint.X / GridSpacing) * GridSpacing;
        double y = Math.Round(docPoint.Y / GridSpacing) * GridSpacing;
        return new Point(x, y);
    }

    /// <summary>
    /// Gets grid line positions within a visible document-space rect
    /// </summary>
    public (IReadOnlyList<double> VerticalLines, IReadOnlyList<double> HorizontalLines)
        GetGridLines(Rect visibleDocRect)
    {
        var verticals = new List<double>();
        var horizontals = new List<double>();

        if (GridSpacing <= 0) return (verticals, horizontals);

        double startX = Math.Floor(visibleDocRect.Left / GridSpacing) * GridSpacing;
        double startY = Math.Floor(visibleDocRect.Top / GridSpacing) * GridSpacing;

        for (double x = startX; x <= visibleDocRect.Right; x += GridSpacing)
            verticals.Add(x);

        for (double y = startY; y <= visibleDocRect.Bottom; y += GridSpacing)
            horizontals.Add(y);

        return (verticals, horizontals);
    }
}

/// <summary>
/// Undo/redo actions specific to markup operations
/// </summary>
public class AddMarkupAction : Services.IMarkupUndoableAction
{
    private readonly IList<MarkupRecord> _collection;
    private readonly MarkupRecord _markup;

    public string Description => $"Add {_markup.Type} markup";

    public AddMarkupAction(IList<MarkupRecord> collection, MarkupRecord markup)
    {
        _collection = collection;
        _markup = markup;
    }

    public void Execute() => _collection.Add(_markup);
    public void Undo() => _collection.Remove(_markup);
}

public class RemoveMarkupAction : Services.IMarkupUndoableAction
{
    private readonly IList<MarkupRecord> _collection;
    private readonly MarkupRecord _markup;
    private int _index;

    public string Description => $"Remove {_markup.Type} markup";

    public RemoveMarkupAction(IList<MarkupRecord> collection, MarkupRecord markup)
    {
        _collection = collection;
        _markup = markup;
    }

    public void Execute()
    {
        _index = _collection.IndexOf(_markup);
        _collection.Remove(_markup);
    }

    public void Undo()
    {
        if (_index >= 0 && _index <= _collection.Count)
            _collection.Insert(_index, _markup);
        else
            _collection.Add(_markup);
    }
}

public class MoveMarkupVerticesAction : Services.IMarkupUndoableAction
{
    private readonly MarkupRecord _markup;
    private readonly List<Point> _oldVertices;
    private readonly List<Point> _newVertices;

    public string Description => $"Move {_markup.Type} markup";

    public MoveMarkupVerticesAction(MarkupRecord markup, List<Point> oldVertices, List<Point> newVertices)
    {
        _markup = markup;
        _oldVertices = new List<Point>(oldVertices);
        _newVertices = new List<Point>(newVertices);
    }

    public void Execute()
    {
        _markup.Vertices.Clear();
        _markup.Vertices.AddRange(_newVertices);
        _markup.UpdateBoundingRect();
    }

    public void Undo()
    {
        _markup.Vertices.Clear();
        _markup.Vertices.AddRange(_oldVertices);
        _markup.UpdateBoundingRect();
    }
}

/// <summary>
/// Interface for markup-specific undoable actions (extends existing undo/redo pattern)
/// </summary>
public interface IMarkupUndoableAction
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>
/// Markup-aware undo/redo service
/// </summary>
public class MarkupUndoRedoService
{
    private readonly Stack<IMarkupUndoableAction> _undoStack = new();
    private readonly Stack<IMarkupUndoableAction> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void Execute(IMarkupUndoableAction action)
    {
        action.Execute();
        _undoStack.Push(action);
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var action = _redoStack.Pop();
        action.Execute();
        _undoStack.Push(action);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}

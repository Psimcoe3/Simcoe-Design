namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides undo/redo functionality using a command pattern
/// </summary>
public class UndoRedoService
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();
    
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    
    public void Execute(IUndoableAction action)
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

/// <summary>
/// Interface for undoable actions
/// </summary>
public interface IUndoableAction
{
    string Description { get; }
    void Execute();
    void Undo();
}

/// <summary>
/// Action to add a component to a collection
/// </summary>
public class AddComponentAction : IUndoableAction
{
    private readonly IList<Models.ElectricalComponent> _collection;
    private readonly Models.ElectricalComponent _component;
    
    public string Description => $"Add {_component.Name}";
    
    public AddComponentAction(IList<Models.ElectricalComponent> collection, Models.ElectricalComponent component)
    {
        _collection = collection;
        _component = component;
    }
    
    public void Execute() => _collection.Add(_component);
    public void Undo() => _collection.Remove(_component);
}

/// <summary>
/// Action to remove a component from a collection
/// </summary>
public class RemoveComponentAction : IUndoableAction
{
    private readonly IList<Models.ElectricalComponent> _collection;
    private readonly Models.ElectricalComponent _component;
    private int _index;
    
    public string Description => $"Remove {_component.Name}";
    
    public RemoveComponentAction(IList<Models.ElectricalComponent> collection, Models.ElectricalComponent component)
    {
        _collection = collection;
        _component = component;
    }
    
    public void Execute()
    {
        _index = _collection.IndexOf(_component);
        _collection.Remove(_component);
    }
    
    public void Undo()
    {
        if (_index >= 0 && _index <= _collection.Count)
            _collection.Insert(_index, _component);
        else
            _collection.Add(_component);
    }
}

/// <summary>
/// Action to move a component to a new position
/// </summary>
public class MoveComponentAction : IUndoableAction
{
    private readonly Models.ElectricalComponent _component;
    private readonly System.Windows.Media.Media3D.Point3D _oldPosition;
    private readonly System.Windows.Media.Media3D.Point3D _newPosition;
    
    public string Description => $"Move {_component.Name}";
    
    public MoveComponentAction(Models.ElectricalComponent component,
        System.Windows.Media.Media3D.Point3D oldPosition,
        System.Windows.Media.Media3D.Point3D newPosition)
    {
        _component = component;
        _oldPosition = oldPosition;
        _newPosition = newPosition;
    }
    
    public void Execute() => _component.Position = _newPosition;
    public void Undo() => _component.Position = _oldPosition;
}

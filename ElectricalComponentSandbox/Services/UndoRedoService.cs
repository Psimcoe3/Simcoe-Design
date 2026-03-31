namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides undo/redo functionality using a command pattern
/// </summary>
public class UndoRedoService
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    public event EventHandler? Changed;
    
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    
    public void Execute(IUndoableAction action)
    {
        action.Execute();
        _undoStack.Push(action);
        _redoStack.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
    
    public void Undo()
    {
        if (!CanUndo) return;
        var action = _undoStack.Pop();
        action.Undo();
        _redoStack.Push(action);
        Changed?.Invoke(this, EventArgs.Empty);
    }
    
    public void Redo()
    {
        if (!CanRedo) return;
        var action = _redoStack.Pop();
        action.Execute();
        _undoStack.Push(action);
        Changed?.Invoke(this, EventArgs.Empty);
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

/// <summary>
/// Action to rotate a component
/// </summary>
public class RotateComponentAction : IUndoableAction
{
    private readonly Models.ElectricalComponent _component;
    private readonly System.Windows.Media.Media3D.Vector3D _oldRotation;
    private readonly System.Windows.Media.Media3D.Vector3D _newRotation;

    public string Description => $"Rotate {_component.Name}";

    public RotateComponentAction(Models.ElectricalComponent component,
        System.Windows.Media.Media3D.Vector3D oldRotation,
        System.Windows.Media.Media3D.Vector3D newRotation)
    {
        _component = component;
        _oldRotation = oldRotation;
        _newRotation = newRotation;
    }

    public void Execute() => _component.Rotation = _newRotation;
    public void Undo() => _component.Rotation = _oldRotation;
}

/// <summary>
/// Action to scale a component
/// </summary>
public class ScaleComponentAction : IUndoableAction
{
    private readonly Models.ElectricalComponent _component;
    private readonly System.Windows.Media.Media3D.Vector3D _oldScale;
    private readonly System.Windows.Media.Media3D.Vector3D _newScale;

    public string Description => $"Scale {_component.Name}";

    public ScaleComponentAction(Models.ElectricalComponent component,
        System.Windows.Media.Media3D.Vector3D oldScale,
        System.Windows.Media.Media3D.Vector3D newScale)
    {
        _component = component;
        _oldScale = oldScale;
        _newScale = newScale;
    }

    public void Execute() => _component.Scale = _newScale;
    public void Undo() => _component.Scale = _oldScale;
}

/// <summary>
/// Action to mirror a component about an axis.
/// Mirrors the position relative to a mirror line and optionally flips the scale.
/// </summary>
public class MirrorComponentAction : IUndoableAction
{
    private readonly Models.ElectricalComponent _component;
    private readonly System.Windows.Media.Media3D.Point3D _oldPosition;
    private readonly System.Windows.Media.Media3D.Point3D _newPosition;
    private readonly System.Windows.Media.Media3D.Vector3D _oldScale;
    private readonly System.Windows.Media.Media3D.Vector3D _newScale;

    public string Description => $"Mirror {_component.Name}";

    public MirrorComponentAction(Models.ElectricalComponent component,
        System.Windows.Media.Media3D.Point3D mirroredPosition,
        System.Windows.Media.Media3D.Vector3D mirroredScale)
    {
        _component = component;
        _oldPosition = component.Position;
        _newPosition = mirroredPosition;
        _oldScale = component.Scale;
        _newScale = mirroredScale;
    }

    public void Execute()
    {
        _component.Position = _newPosition;
        _component.Scale = _newScale;
    }

    public void Undo()
    {
        _component.Position = _oldPosition;
        _component.Scale = _oldScale;
    }
}

/// <summary>
/// Action to change a single property on a component (layer, elevation, material, etc.)
/// </summary>
public class PropertyChangeAction<T> : IUndoableAction
{
    private readonly Action<T> _setter;
    private readonly T _oldValue;
    private readonly T _newValue;
    private readonly string _description;

    public string Description => _description;

    public PropertyChangeAction(string description, Action<T> setter, T oldValue, T newValue)
    {
        _description = description;
        _setter = setter;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Execute() => _setter(_newValue);
    public void Undo() => _setter(_oldValue);
}

/// <summary>
/// Groups multiple actions into a single undoable transaction.
/// All sub-actions execute/undo as a unit.
/// </summary>
public class CompositeAction : IUndoableAction
{
    private readonly List<IUndoableAction> _actions;

    public string Description { get; }

    public CompositeAction(string description, IEnumerable<IUndoableAction> actions)
    {
        Description = description;
        _actions = actions.ToList();
    }

    public void Execute()
    {
        foreach (var action in _actions)
            action.Execute();
    }

    public void Undo()
    {
        for (int i = _actions.Count - 1; i >= 0; i--)
            _actions[i].Undo();
    }
}

/// <summary>
/// Undoable layer visibility change.
/// </summary>
public class LayerVisibilityAction : IUndoableAction
{
    private readonly Models.Layer _layer;
    private readonly bool _oldVisible;
    private readonly bool _newVisible;

    public string Description => $"Toggle visibility: {_layer.Name}";

    public LayerVisibilityAction(Models.Layer layer, bool newVisible)
    {
        _layer = layer;
        _oldVisible = layer.IsVisible;
        _newVisible = newVisible;
    }

    public void Execute() => _layer.IsVisible = _newVisible;
    public void Undo() => _layer.IsVisible = _oldVisible;
}

/// <summary>
/// Undoable layer freeze/thaw change.
/// </summary>
public class LayerFreezeAction : IUndoableAction
{
    private readonly Models.Layer _layer;
    private readonly bool _oldFrozen;
    private readonly bool _newFrozen;

    public string Description => $"Toggle freeze: {_layer.Name}";

    public LayerFreezeAction(Models.Layer layer, bool newFrozen)
    {
        _layer = layer;
        _oldFrozen = layer.IsFrozen;
        _newFrozen = newFrozen;
    }

    public void Execute() => _layer.IsFrozen = _newFrozen;
    public void Undo() => _layer.IsFrozen = _oldFrozen;
}

/// <summary>
/// Undoable layer lock change.
/// </summary>
public class LayerLockAction : IUndoableAction
{
    private readonly Models.Layer _layer;
    private readonly bool _oldLocked;
    private readonly bool _newLocked;

    public string Description => $"Toggle lock: {_layer.Name}";

    public LayerLockAction(Models.Layer layer, bool newLocked)
    {
        _layer = layer;
        _oldLocked = layer.IsLocked;
        _newLocked = newLocked;
    }

    public void Execute() => _layer.IsLocked = _newLocked;
    public void Undo() => _layer.IsLocked = _oldLocked;
}

/// <summary>
/// Undoable add to a generic collection (circuits, markups, etc.).
/// </summary>
public class AddItemAction<T> : IUndoableAction
{
    private readonly IList<T> _collection;
    private readonly T _item;
    private readonly string _itemName;

    public string Description => $"Add {_itemName}";

    public AddItemAction(IList<T> collection, T item, string itemName)
    {
        _collection = collection;
        _item = item;
        _itemName = itemName;
    }

    public void Execute() => _collection.Add(_item);
    public void Undo() => _collection.Remove(_item);
}

/// <summary>
/// Undoable remove from a generic collection.
/// </summary>
public class RemoveItemAction<T> : IUndoableAction
{
    private readonly IList<T> _collection;
    private readonly T _item;
    private readonly int _index;
    private readonly string _itemName;

    public string Description => $"Remove {_itemName}";

    public RemoveItemAction(IList<T> collection, T item, string itemName)
    {
        _collection = collection;
        _item = item;
        _index = collection.IndexOf(item);
        _itemName = itemName;
    }

    public void Execute() => _collection.Remove(_item);
    public void Undo()
    {
        if (_index >= 0 && _index <= _collection.Count)
            _collection.Insert(_index, _item);
        else
            _collection.Add(_item);
    }
}

/// <summary>
/// Undoable bulk property change using SelectionFilterService results.
/// </summary>
public class BulkPropertyChangeAction : IUndoableAction
{
    private readonly SelectionFilterService _filterService;
    private readonly IReadOnlyList<Models.ElectricalComponent> _components;
    private readonly BulkPropertyChange _change;
    private BulkPropertyChangeResult? _result;

    public string Description => $"Bulk edit {_components.Count} component(s)";

    public BulkPropertyChangeAction(
        SelectionFilterService filterService,
        IReadOnlyList<Models.ElectricalComponent> components,
        BulkPropertyChange change)
    {
        _filterService = filterService;
        _components = components;
        _change = change;
    }

    public void Execute()
    {
        _result = _filterService.ApplyBulkPropertyChange(_components, _change);
    }

    public void Undo()
    {
        if (_result != null)
            _filterService.RevertBulkPropertyChange(_result);
    }
}

/// <summary>
/// Undoable markup status change.
/// </summary>
public class MarkupStatusAction : IUndoableAction
{
    private readonly Markup.Models.MarkupRecord _markup;
    private readonly Markup.Models.MarkupStatus _oldStatus;
    private readonly Markup.Models.MarkupStatus _newStatus;
    private readonly string? _oldStatusNote;
    private readonly string? _newStatusNote;
    private readonly DateTime _oldModifiedUtc;
    private readonly DateTime _newModifiedUtc;
    private readonly Markup.Models.MarkupReply? _auditReply;

    public string Description => $"Change markup status: {_markup.Metadata.Label}";

    public MarkupStatusAction(
        Markup.Models.MarkupRecord markup,
        Markup.Models.MarkupStatus newStatus,
        string? newStatusNote = null,
        Markup.Models.MarkupReply? auditReply = null,
        DateTime? modifiedUtc = null)
    {
        _markup = markup;
        _oldStatus = markup.Status;
        _newStatus = newStatus;
        _oldStatusNote = markup.StatusNote;
        _newStatusNote = newStatusNote;
        _oldModifiedUtc = markup.Metadata.ModifiedUtc;
        _newModifiedUtc = modifiedUtc ?? DateTime.UtcNow;
        _auditReply = auditReply;
    }

    public void Execute()
    {
        _markup.Status = _newStatus;
        _markup.StatusNote = _newStatusNote;
        _markup.Metadata.ModifiedUtc = _newModifiedUtc;

        if (_auditReply != null && !_markup.Replies.Any(existing => string.Equals(existing.Id, _auditReply.Id, StringComparison.Ordinal)))
            _markup.Replies.Add(_auditReply);
    }

    public void Undo()
    {
        _markup.Status = _oldStatus;
        _markup.StatusNote = _oldStatusNote;
        _markup.Metadata.ModifiedUtc = _oldModifiedUtc;

        if (_auditReply != null)
            _markup.Replies.RemoveAll(existing => string.Equals(existing.Id, _auditReply.Id, StringComparison.Ordinal));
    }
}

/// <summary>
/// Undoable addition of a markup reply.
/// </summary>
public class MarkupReplyAction : IUndoableAction
{
    private readonly Markup.Models.MarkupRecord _markup;
    private readonly Markup.Models.MarkupReply _reply;
    private readonly DateTime _oldModifiedUtc;

    public string Description => $"Add markup reply: {_markup.Metadata.Label}";

    public MarkupReplyAction(Markup.Models.MarkupRecord markup, Markup.Models.MarkupReply reply)
    {
        _markup = markup;
        _reply = reply;
        _oldModifiedUtc = markup.Metadata.ModifiedUtc;
    }

    public void Execute()
    {
        if (!_markup.Replies.Any(existing => string.Equals(existing.Id, _reply.Id, StringComparison.Ordinal)))
            _markup.Replies.Add(_reply);

        _markup.Metadata.ModifiedUtc = _reply.ModifiedUtc;
    }

    public void Undo()
    {
        _markup.Replies.RemoveAll(existing => string.Equals(existing.Id, _reply.Id, StringComparison.Ordinal));
        _markup.Metadata.ModifiedUtc = _oldModifiedUtc;
    }
}

/// <summary>
/// Undoable markup assignment change.
/// </summary>
public class MarkupAssignmentAction : IUndoableAction
{
    private readonly Markup.Models.MarkupRecord _markup;
    private readonly string? _oldAssignedTo;
    private readonly string? _newAssignedTo;
    private readonly DateTime _oldModifiedUtc;
    private readonly DateTime _newModifiedUtc;
    private readonly Markup.Models.MarkupReply? _auditReply;

    public string Description => $"Assign markup: {_markup.Metadata.Label}";

    public MarkupAssignmentAction(
        Markup.Models.MarkupRecord markup,
        string? newAssignedTo,
        Markup.Models.MarkupReply? auditReply = null,
        DateTime? modifiedUtc = null)
    {
        _markup = markup;
        _oldAssignedTo = markup.AssignedTo;
        _newAssignedTo = newAssignedTo;
        _oldModifiedUtc = markup.Metadata.ModifiedUtc;
        _newModifiedUtc = modifiedUtc ?? DateTime.UtcNow;
        _auditReply = auditReply;
    }

    public void Execute()
    {
        _markup.AssignedTo = _newAssignedTo;
        _markup.Metadata.ModifiedUtc = _newModifiedUtc;

        if (_auditReply != null && !_markup.Replies.Any(existing => string.Equals(existing.Id, _auditReply.Id, StringComparison.Ordinal)))
            _markup.Replies.Add(_auditReply);
    }

    public void Undo()
    {
        _markup.AssignedTo = _oldAssignedTo;
        _markup.Metadata.ModifiedUtc = _oldModifiedUtc;

        if (_auditReply != null)
            _markup.Replies.RemoveAll(existing => string.Equals(existing.Id, _auditReply.Id, StringComparison.Ordinal));
    }
}

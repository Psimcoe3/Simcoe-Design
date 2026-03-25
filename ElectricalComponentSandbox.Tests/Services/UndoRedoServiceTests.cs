using System.Collections.ObjectModel;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class UndoRedoServiceTests
{
    [Fact]
    public void Initial_State_CannotUndoOrRedo()
    {
        var service = new UndoRedoService();

        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Execute_AddsToUndoStack()
    {
        var service = new UndoRedoService();
        var list = new ObservableCollection<ElectricalComponent>();
        var comp = new BoxComponent();

        service.Execute(new AddComponentAction(list, comp));

        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
        Assert.Single(list);
    }

    [Fact]
    public void Undo_ReversesAction()
    {
        var service = new UndoRedoService();
        var list = new ObservableCollection<ElectricalComponent>();
        var comp = new BoxComponent();

        service.Execute(new AddComponentAction(list, comp));
        service.Undo();

        Assert.Empty(list);
        Assert.False(service.CanUndo);
        Assert.True(service.CanRedo);
    }

    [Fact]
    public void Redo_ReappliesAction()
    {
        var service = new UndoRedoService();
        var list = new ObservableCollection<ElectricalComponent>();
        var comp = new BoxComponent();

        service.Execute(new AddComponentAction(list, comp));
        service.Undo();
        service.Redo();

        Assert.Single(list);
        Assert.True(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void Execute_ClearsRedoStack()
    {
        var service = new UndoRedoService();
        var list = new ObservableCollection<ElectricalComponent>();
        var comp1 = new BoxComponent();
        var comp2 = new PanelComponent();

        service.Execute(new AddComponentAction(list, comp1));
        service.Undo();
        service.Execute(new AddComponentAction(list, comp2));

        Assert.False(service.CanRedo);
        Assert.Single(list);
    }

    [Fact]
    public void Clear_ResetsStacks()
    {
        var service = new UndoRedoService();
        var list = new ObservableCollection<ElectricalComponent>();
        var comp = new BoxComponent();

        service.Execute(new AddComponentAction(list, comp));
        service.Clear();

        Assert.False(service.CanUndo);
        Assert.False(service.CanRedo);
    }

    [Fact]
    public void RemoveComponentAction_Works()
    {
        var service = new UndoRedoService();
        var list = new ObservableCollection<ElectricalComponent>();
        var comp = new BoxComponent();
        list.Add(comp);

        service.Execute(new RemoveComponentAction(list, comp));

        Assert.Empty(list);

        service.Undo();

        Assert.Single(list);
    }

    [Fact]
    public void MoveComponentAction_Works()
    {
        var service = new UndoRedoService();
        var comp = new BoxComponent { Position = new Point3D(0, 0, 0) };
        var newPos = new Point3D(5, 10, 15);

        service.Execute(new MoveComponentAction(comp, comp.Position, newPos));

        Assert.Equal(newPos, comp.Position);

        service.Undo();

        Assert.Equal(new Point3D(0, 0, 0), comp.Position);
    }

    [Fact]
    public void RotateComponentAction_Works()
    {
        var service = new UndoRedoService();
        var comp = new BoxComponent { Rotation = new Vector3D(0, 0, 0) };
        var newRot = new Vector3D(0, 90, 0);

        service.Execute(new RotateComponentAction(comp, comp.Rotation, newRot));

        Assert.Equal(newRot, comp.Rotation);

        service.Undo();

        Assert.Equal(new Vector3D(0, 0, 0), comp.Rotation);
    }

    [Fact]
    public void ScaleComponentAction_Works()
    {
        var service = new UndoRedoService();
        var comp = new BoxComponent { Scale = new Vector3D(1, 1, 1) };
        var newScale = new Vector3D(2, 3, 4);

        service.Execute(new ScaleComponentAction(comp, comp.Scale, newScale));

        Assert.Equal(newScale, comp.Scale);

        service.Undo();

        Assert.Equal(new Vector3D(1, 1, 1), comp.Scale);
    }

    [Fact]
    public void PropertyChangeAction_Works()
    {
        var service = new UndoRedoService();
        var comp = new BoxComponent { Name = "Panel-A" };
        var oldName = comp.Name;

        service.Execute(new PropertyChangeAction<string>(
            "Rename Panel-A",
            v => comp.Name = v,
            oldName,
            "Panel-B"));

        Assert.Equal("Panel-B", comp.Name);

        service.Undo();

        Assert.Equal("Panel-A", comp.Name);
    }

    [Fact]
    public void CompositeAction_UndoesInReverse()
    {
        var service = new UndoRedoService();
        var comp = new BoxComponent
        {
            Position = new Point3D(0, 0, 0),
            Rotation = new Vector3D(0, 0, 0)
        };

        var actions = new IUndoableAction[]
        {
            new MoveComponentAction(comp, comp.Position, new Point3D(5, 0, 0)),
            new RotateComponentAction(comp, comp.Rotation, new Vector3D(0, 90, 0))
        };

        service.Execute(new CompositeAction("Move and rotate", actions));

        Assert.Equal(new Point3D(5, 0, 0), comp.Position);
        Assert.Equal(new Vector3D(0, 90, 0), comp.Rotation);

        service.Undo();

        Assert.Equal(new Point3D(0, 0, 0), comp.Position);
        Assert.Equal(new Vector3D(0, 0, 0), comp.Rotation);
    }

    [Fact]
    public void LayerVisibilityAction_TogglesAndReverts()
    {
        var service = new UndoRedoService();
        var layer = new Layer { Name = "E-POWER", IsVisible = true };

        service.Execute(new LayerVisibilityAction(layer, false));
        Assert.False(layer.IsVisible);

        service.Undo();
        Assert.True(layer.IsVisible);
    }

    [Fact]
    public void LayerFreezeAction_TogglesAndReverts()
    {
        var service = new UndoRedoService();
        var layer = new Layer { Name = "E-POWER", IsFrozen = false };

        service.Execute(new LayerFreezeAction(layer, true));
        Assert.True(layer.IsFrozen);

        service.Undo();
        Assert.False(layer.IsFrozen);
    }

    [Fact]
    public void LayerLockAction_TogglesAndReverts()
    {
        var service = new UndoRedoService();
        var layer = new Layer { Name = "E-POWER", IsLocked = false };

        service.Execute(new LayerLockAction(layer, true));
        Assert.True(layer.IsLocked);

        service.Undo();
        Assert.False(layer.IsLocked);
    }

    [Fact]
    public void AddItemAction_AddsAndRemoves()
    {
        var service = new UndoRedoService();
        var list = new ObservableCollection<string>();

        service.Execute(new AddItemAction<string>(list, "Circuit 1", "Circuit 1"));
        Assert.Single(list);
        Assert.Equal("Circuit 1", list[0]);

        service.Undo();
        Assert.Empty(list);
    }

    [Fact]
    public void RemoveItemAction_RemovesAndRestores()
    {
        var service = new UndoRedoService();
        var list = new ObservableCollection<string> { "A", "B", "C" };

        service.Execute(new RemoveItemAction<string>(list, "B", "B"));
        Assert.Equal(2, list.Count);
        Assert.DoesNotContain("B", list);

        service.Undo();
        Assert.Equal(3, list.Count);
        Assert.Equal("B", list[1]);
    }

    [Fact]
    public void BulkPropertyChangeAction_AppliesAndReverts()
    {
        var service = new UndoRedoService();
        var filterService = new SelectionFilterService();
        var comp = new BoxComponent { LayerId = "layer-1" };
        var components = new List<ElectricalComponent> { comp };
        var change = new BulkPropertyChange { LayerId = "layer-2" };

        service.Execute(new BulkPropertyChangeAction(filterService, components, change));
        Assert.Equal("layer-2", comp.LayerId);

        service.Undo();
        Assert.Equal("layer-1", comp.LayerId);
    }
}

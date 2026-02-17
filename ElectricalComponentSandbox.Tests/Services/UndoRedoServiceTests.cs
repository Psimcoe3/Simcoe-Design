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
}

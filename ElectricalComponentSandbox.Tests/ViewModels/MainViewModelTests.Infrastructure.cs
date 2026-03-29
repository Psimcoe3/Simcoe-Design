using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests.ViewModels;

public partial class MainViewModelTests
{
    [Fact]
    public void Constructor_InitializesDefaultLayer()
    {
        var vm = new MainViewModel();

        Assert.Single(vm.Layers);
        Assert.Equal("default", vm.Layers[0].Id);
        Assert.NotNull(vm.ActiveLayer);
    }

    [Fact]
    public void AddLayer_CreatesNewLayer()
    {
        var vm = new MainViewModel();

        var layer = vm.AddLayer("Electrical");

        Assert.Equal(2, vm.Layers.Count);
        Assert.Equal("Electrical", layer.Name);
    }

    [Fact]
    public void RemoveLayer_MovesComponentsToDefault()
    {
        var vm = new MainViewModel();
        var layer = vm.AddLayer("Test Layer");
        vm.ActiveLayer = layer;
        vm.AddComponent(ComponentType.Box);
        var comp = vm.Components[0];
        Assert.Equal(layer.Id, comp.LayerId);

        vm.RemoveLayer(layer);

        Assert.Equal("default", comp.LayerId);
        Assert.Single(vm.Layers);
    }

    [Fact]
    public void RemoveLayer_CannotRemoveDefault()
    {
        var vm = new MainViewModel();
        var defaultLayer = vm.Layers[0];

        vm.RemoveLayer(defaultLayer);

        Assert.Single(vm.Layers); // Still there
    }

    [Fact]
    public void UndoRedo_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.UndoRedo);
        Assert.False(vm.UndoRedo.CanUndo);
    }

    [Fact]
    public void AddComponent_IsUndoable()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Box);
        Assert.Single(vm.Components);

        vm.Undo();
        Assert.Empty(vm.Components);

        vm.Redo();
        Assert.Single(vm.Components);
    }

    [Fact]
    public void DeleteSelectedComponent_IsUndoable()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Conduit);
        var component = vm.Components[0];

        vm.DeleteSelectedComponent();
        Assert.Empty(vm.Components);

        vm.Undo();
        Assert.Single(vm.Components);
        Assert.Equal(component.Id, vm.Components[0].Id);
    }

    [Fact]
    public void UnitSystemName_Default_IsImperial()
    {
        var vm = new MainViewModel();

        Assert.Equal("Imperial", vm.UnitSystemName);
    }

    [Fact]
    public void UnitSystemName_CanBeChanged()
    {
        var vm = new MainViewModel();

        vm.UnitSystemName = "Metric";

        Assert.Equal("Metric", vm.UnitSystemName);
    }

    [Fact]
    public void ToProjectModel_CreatesCorrectModel()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);

        var project = vm.ToProjectModel();

        Assert.NotNull(project);
        Assert.Single(project.Components);
    }

    [Fact]
    public void LoadFromProject_RestoresState()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);
        vm.AddComponent(ComponentType.Panel);
        var project = vm.ToProjectModel();

        var vm2 = new MainViewModel();
        vm2.LoadFromProject(project);

        Assert.Equal(2, vm2.Components.Count);
    }
}

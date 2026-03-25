using System.Collections.ObjectModel;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests.ViewModels;

public class LayerManagerViewModelTests
{
    [Fact]
    public void RowPropertyChange_RaisesLayerRowChanged()
    {
        var layer = Layer.CreateDefault();
        var viewModel = new LayerManagerViewModel(new ObservableCollection<Layer> { layer });
        LayerRowChangedEventArgs? received = null;

        viewModel.LayerRowChanged += (_, args) => received = args;

        viewModel.Rows[0].IsFrozen = true;

        Assert.NotNull(received);
        Assert.Same(layer, received!.Layer);
        Assert.Equal(nameof(LayerRowViewModel.IsFrozen), received.PropertyName);
    }

    [Fact]
    public void CollectionChange_RaisesLayerRowChangedAndRefreshesRows()
    {
        var layers = new ObservableCollection<Layer> { Layer.CreateDefault() };
        var viewModel = new LayerManagerViewModel(layers);
        LayerRowChangedEventArgs? received = null;

        viewModel.LayerRowChanged += (_, args) => received = args;

        var electricalLayer = new Layer { Name = "Electrical Layout" };
        layers.Add(electricalLayer);

        Assert.NotNull(received);
        Assert.Null(received!.PropertyName);
        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Contains(viewModel.Rows, row => ReferenceEquals(row.Layer, electricalLayer));
    }

    // ── Layer State Snapshot Tests ──────────────────────────────────────────

    [Fact]
    public void SaveState_CapturesCurrentLayerStates()
    {
        var layer1 = new Layer { Id = "L1", Name = "Power", IsVisible = true, IsFrozen = false };
        var layer2 = new Layer { Id = "L2", Name = "Lighting", IsVisible = false, IsFrozen = true };
        var layers = new ObservableCollection<Layer> { layer1, layer2 };
        var vm = new LayerManagerViewModel(layers);

        vm.SaveState("TestState");

        Assert.Single(vm.SavedStates);
        var snapshot = vm.SavedStates[0];
        Assert.Equal("TestState", snapshot.Name);
        Assert.Equal(2, snapshot.LayerStates.Count);

        var entry1 = snapshot.LayerStates.First(e => e.LayerId == "L1");
        Assert.True(entry1.IsVisible);
        Assert.False(entry1.IsFrozen);

        var entry2 = snapshot.LayerStates.First(e => e.LayerId == "L2");
        Assert.False(entry2.IsVisible);
        Assert.True(entry2.IsFrozen);
    }

    [Fact]
    public void SaveState_OverwritesExistingWithSameName()
    {
        var layer = new Layer { Id = "L1", IsVisible = true };
        var layers = new ObservableCollection<Layer> { layer };
        var vm = new LayerManagerViewModel(layers);

        vm.SaveState("MyState");
        layer.IsVisible = false;
        vm.SaveState("MyState");

        Assert.Single(vm.SavedStates);
        Assert.False(vm.SavedStates[0].LayerStates[0].IsVisible);
    }

    [Fact]
    public void RestoreState_AppliesSnapshotToLayers()
    {
        var layer = new Layer { Id = "L1", IsVisible = true, IsFrozen = false, IsLocked = false, IsPlotted = true };
        var layers = new ObservableCollection<Layer> { layer };
        var vm = new LayerManagerViewModel(layers);

        vm.SaveState("Before");

        // Change layer properties
        layer.IsVisible = false;
        layer.IsFrozen = true;
        layer.IsLocked = true;
        layer.IsPlotted = false;

        // Restore original state
        vm.RestoreState("Before");

        Assert.True(layer.IsVisible);
        Assert.False(layer.IsFrozen);
        Assert.False(layer.IsLocked);
        Assert.True(layer.IsPlotted);
    }

    [Fact]
    public void RestoreState_NonExistentName_DoesNothing()
    {
        var layer = new Layer { Id = "L1", IsVisible = false };
        var layers = new ObservableCollection<Layer> { layer };
        var vm = new LayerManagerViewModel(layers);

        vm.RestoreState("DoesNotExist");

        Assert.False(layer.IsVisible); // unchanged
    }

    [Fact]
    public void DeleteState_RemovesSnapshot()
    {
        var layers = new ObservableCollection<Layer> { new Layer { Id = "L1" } };
        var vm = new LayerManagerViewModel(layers);

        vm.SaveState("ToDelete");
        Assert.Single(vm.SavedStates);

        vm.DeleteState("ToDelete");
        Assert.Empty(vm.SavedStates);
    }

    [Fact]
    public void DeleteState_NonExistentName_DoesNothing()
    {
        var layers = new ObservableCollection<Layer> { new Layer { Id = "L1" } };
        var vm = new LayerManagerViewModel(layers);

        vm.SaveState("Keep");
        vm.DeleteState("NoSuchState");

        Assert.Single(vm.SavedStates);
    }

    [Fact]
    public void FreezeAllExcept_FreezesOtherLayers()
    {
        var layer1 = new Layer { Id = "L1", IsFrozen = false };
        var layer2 = new Layer { Id = "L2", IsFrozen = false };
        var layer3 = new Layer { Id = "L3", IsFrozen = false };
        var layers = new ObservableCollection<Layer> { layer1, layer2, layer3 };
        var vm = new LayerManagerViewModel(layers);

        vm.FreezeAllExcept("L2");

        Assert.True(vm.Rows[0].IsFrozen);
        Assert.False(vm.Rows[1].IsFrozen);
        Assert.True(vm.Rows[2].IsFrozen);
    }

    [Fact]
    public void SetPlotAll_SetsAllRowsPlotted()
    {
        var layer1 = new Layer { Id = "L1", IsPlotted = true };
        var layer2 = new Layer { Id = "L2", IsPlotted = false };
        var layers = new ObservableCollection<Layer> { layer1, layer2 };
        var vm = new LayerManagerViewModel(layers);

        vm.SetPlotAll(false);

        Assert.All(vm.Rows, r => Assert.False(r.IsPlotted));

        vm.SetPlotAll(true);

        Assert.All(vm.Rows, r => Assert.True(r.IsPlotted));
    }
}

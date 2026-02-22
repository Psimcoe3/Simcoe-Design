using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox.Tests.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void Constructor_InitializesLibraryComponents()
    {
        var vm = new MainViewModel();
        var expectedTemplates = ElectricalComponentCatalog.CreateLibraryTemplates();

        Assert.Equal(expectedTemplates.Count, vm.LibraryComponents.Count);
        foreach (var template in expectedTemplates)
        {
            Assert.Contains(vm.LibraryComponents, c => c.Type == template.Type && c.Name == template.Name);
        }
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Conduit);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Box);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Panel);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Support);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.CableTray);
        Assert.Contains(vm.LibraryComponents, c => c.Type == ComponentType.Hanger);
    }

    [Fact]
    public void Constructor_ComponentsIsEmpty()
    {
        var vm = new MainViewModel();

        Assert.Empty(vm.Components);
    }

    [Fact]
    public void Constructor_DefaultSettings()
    {
        var vm = new MainViewModel();

        Assert.True(vm.ShowGrid);
        Assert.True(vm.SnapToGrid);
        Assert.Equal(1.0, vm.GridSize);
        Assert.Null(vm.SelectedComponent);
    }

    [Fact]
    public void AddComponent_Conduit_AddsAndSelects()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Conduit);

        Assert.Single(vm.Components);
        Assert.IsType<ConduitComponent>(vm.Components[0]);
        Assert.Equal(vm.Components[0], vm.SelectedComponent);
    }

    [Fact]
    public void AddComponent_Box_AddsCorrectType()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Box);

        Assert.Single(vm.Components);
        Assert.IsType<BoxComponent>(vm.Components[0]);
    }

    [Fact]
    public void AddComponent_Panel_AddsCorrectType()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Panel);

        Assert.Single(vm.Components);
        Assert.IsType<PanelComponent>(vm.Components[0]);
    }

    [Fact]
    public void AddComponent_Support_AddsCorrectType()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Support);

        Assert.Single(vm.Components);
        Assert.IsType<SupportComponent>(vm.Components[0]);
    }

    [Fact]
    public void AddComponent_MultipleTimes_AddsAll()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Conduit);
        vm.AddComponent(ComponentType.Box);
        vm.AddComponent(ComponentType.Panel);

        Assert.Equal(3, vm.Components.Count);
    }

    [Fact]
    public void DeleteSelectedComponent_RemovesComponent()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Conduit);
        Assert.Single(vm.Components);

        vm.DeleteSelectedComponent();

        Assert.Empty(vm.Components);
        Assert.Null(vm.SelectedComponent);
    }

    [Fact]
    public void DeleteSelectedComponent_NothingSelected_DoesNothing()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Conduit);
        vm.SelectedComponent = null;

        vm.DeleteSelectedComponent();

        Assert.Single(vm.Components);
    }

    [Fact]
    public void MoveComponent_UpdatesPosition()
    {
        var vm = new MainViewModel();
        vm.SnapToGrid = false;
        vm.AddComponent(ComponentType.Conduit);

        vm.MoveComponent(new Vector3D(5, 10, 15));

        Assert.Equal(new Point3D(5, 10, 15), vm.SelectedComponent!.Position);
    }

    [Fact]
    public void MoveComponent_WithSnapToGrid_SnapsPosition()
    {
        var vm = new MainViewModel();
        vm.SnapToGrid = true;
        vm.GridSize = 2.0;
        vm.AddComponent(ComponentType.Conduit);

        vm.MoveComponent(new Vector3D(3.3, 4.7, 5.1));

        // Expect snapped values: round(3.3/2)*2=4, round(4.7/2)*2=4, round(5.1/2)*2=6
        Assert.Equal(4.0, vm.SelectedComponent!.Position.X);
        Assert.Equal(4.0, vm.SelectedComponent.Position.Y);
        Assert.Equal(6.0, vm.SelectedComponent.Position.Z);
    }

    [Fact]
    public void MoveComponent_NoSelection_DoesNothing()
    {
        var vm = new MainViewModel();
        vm.SelectedComponent = null;

        vm.MoveComponent(new Vector3D(5, 5, 5)); // Should not throw
    }

    [Fact]
    public void RotateComponent_UpdatesRotation()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);

        vm.RotateComponent(new Vector3D(45, 90, 180));

        Assert.Equal(new Vector3D(45, 90, 180), vm.SelectedComponent!.Rotation);
    }

    [Fact]
    public void ScaleComponent_UpdatesScale()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);

        vm.ScaleComponent(new Vector3D(2, 3, 4));

        Assert.Equal(new Vector3D(2, 3, 4), vm.SelectedComponent!.Scale);
    }

    [Fact]
    public void SelectedComponent_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.AddComponent(ComponentType.Conduit);

        Assert.Equal(nameof(vm.SelectedComponent), propertyName);
    }

    [Fact]
    public void ShowGrid_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.ShowGrid = false;

        Assert.Equal(nameof(vm.ShowGrid), propertyName);
    }

    [Fact]
    public void SnapToGrid_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.SnapToGrid = false;

        Assert.Equal(nameof(vm.SnapToGrid), propertyName);
    }

    [Fact]
    public void GridSize_PropertyChanged_Fires()
    {
        var vm = new MainViewModel();
        var propertyName = string.Empty;
        vm.PropertyChanged += (s, e) => propertyName = e.PropertyName;

        vm.GridSize = 5.0;

        Assert.Equal(nameof(vm.GridSize), propertyName);
    }

    [Fact]
    public void GridSize_SetToZero_ClampsToMinimum()
    {
        var vm = new MainViewModel();

        vm.GridSize = 0;

        Assert.Equal(0.1, vm.GridSize);
    }

    [Fact]
    public void FileService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.FileService);
    }

    // ===== New Feature Tests =====

    [Fact]
    public void AddComponent_CableTray_AddsCorrectType()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.CableTray);

        Assert.Single(vm.Components);
        Assert.IsType<CableTrayComponent>(vm.Components[0]);
    }

    [Fact]
    public void AddComponent_Hanger_AddsCorrectType()
    {
        var vm = new MainViewModel();

        vm.AddComponent(ComponentType.Hanger);

        Assert.Single(vm.Components);
        Assert.IsType<HangerComponent>(vm.Components[0]);
    }

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
        vm.AddComponent(ComponentType.Box);

        vm.DeleteSelectedComponent();
        Assert.Empty(vm.Components);

        vm.Undo();
        Assert.Single(vm.Components);
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
        Assert.Equal(UnitSystem.Metric, vm.UnitConverter.CurrentSystem);
    }

    [Fact]
    public void ToProjectModel_CreatesCorrectModel()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);
        vm.GridSize = 2.0;

        var project = vm.ToProjectModel();

        Assert.Single(project.Components);
        Assert.Equal(2.0, project.GridSize);
        Assert.Single(project.Layers);
    }

    [Fact]
    public void LoadFromProject_RestoresState()
    {
        var vm = new MainViewModel();
        var project = new ProjectModel
        {
            GridSize = 5.0,
            ShowGrid = false,
            UnitSystem = "Metric"
        };
        project.Components.Add(new BoxComponent());
        project.Components.Add(new PanelComponent());
        project.Layers.Add(Layer.CreateDefault());

        vm.LoadFromProject(project);

        Assert.Equal(2, vm.Components.Count);
        Assert.Equal(5.0, vm.GridSize);
        Assert.False(vm.ShowGrid);
        Assert.Equal("Metric", vm.UnitSystemName);
        Assert.Null(vm.SelectedComponent);
    }

    [Fact]
    public void ProjectFileService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.ProjectFileService);
    }

    [Fact]
    public void BomExport_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.BomExport);
    }

    [Fact]
    public void SnapService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.SnapService);
    }

    [Fact]
    public void CalibrationService_IsInitialized()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.CalibrationService);
    }

    [Fact]
    public void PdfUnderlay_Default_IsNull()
    {
        var vm = new MainViewModel();

        Assert.Null(vm.PdfUnderlay);
    }

    [Fact]
    public void PdfUnderlay_CanBeSet()
    {
        var vm = new MainViewModel();
        var underlay = new PdfUnderlay { FilePath = "test.pdf" };

        vm.PdfUnderlay = underlay;

        Assert.NotNull(vm.PdfUnderlay);
        Assert.Equal("test.pdf", vm.PdfUnderlay.FilePath);
    }

    [Fact]
    public void AddComponent_AssignsActiveLayerId()
    {
        var vm = new MainViewModel();
        var newLayer = vm.AddLayer("Test");
        vm.ActiveLayer = newLayer;

        vm.AddComponent(ComponentType.Box);

        Assert.Equal(newLayer.Id, vm.Components[0].LayerId);
    }
}

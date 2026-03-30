using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.ViewModels;
using System.Windows;

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
    public void SelectSingleComponent_SetsPrimaryAndSelectionIds()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);
        vm.AddComponent(ComponentType.Panel);
        var target = vm.Components[0];

        vm.SelectSingleComponent(target);

        Assert.Equal(target, vm.SelectedComponent);
        Assert.Single(vm.SelectedComponentIds);
        Assert.Contains(target.Id, vm.SelectedComponentIds);
    }

    [Fact]
    public void SetSelectedComponents_TracksAllSelectedIds()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);
        vm.AddComponent(ComponentType.Panel);
        vm.AddComponent(ComponentType.Support);

        var selected = vm.Components.Take(2).ToList();

        vm.SetSelectedComponents(selected, selected[1]);

        Assert.Equal(2, vm.SelectedComponentIds.Count);
        Assert.Equal(selected[1], vm.SelectedComponent);
        Assert.All(selected, component => Assert.Contains(component.Id, vm.SelectedComponentIds));
    }

    [Fact]
    public void ToggleComponentSelection_RemovingPrimaryPromotesRemainingSelection()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);
        vm.AddComponent(ComponentType.Panel);

        var first = vm.Components[0];
        var second = vm.Components[1];

        vm.SetSelectedComponents(new[] { first, second }, second);
        var isStillSelected = vm.ToggleComponentSelection(second);

        Assert.False(isStillSelected);
        Assert.Equal(first, vm.SelectedComponent);
        Assert.Single(vm.SelectedComponentIds);
        Assert.Contains(first.Id, vm.SelectedComponentIds);
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
    public void DeleteSelectedComponent_WithMultiSelection_RemovesAllSelectedAndIsUndoable()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Conduit);
        vm.AddComponent(ComponentType.Box);
        vm.AddComponent(ComponentType.Panel);

        var selected = vm.Components.Take(2).ToList();
        vm.SetSelectedComponents(selected, selected[0]);

        vm.DeleteSelectedComponent();

        Assert.Single(vm.Components);
        Assert.Empty(vm.SelectedComponentIds);
        Assert.Null(vm.SelectedComponent);

        vm.Undo();

        Assert.Equal(3, vm.Components.Count);
        Assert.All(selected, component => Assert.Contains(vm.Components, restored => restored.Id == component.Id));
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

    [Fact]
    public void AddSheet_AndSelectSheet_PreservesSheetScopedState()
    {
        var vm = new MainViewModel();
        vm.PdfUnderlay = new PdfUnderlay { FilePath = "sheet1.pdf", PageNumber = 1 };
        vm.NamedViews.Add(new NamedView { Name = "Sheet 1 View", Zoom = 1.1 });
        vm.AddMarkup(new MarkupRecord
        {
            Type = MarkupType.Rectangle,
            Vertices = [new Point(0, 0), new Point(10, 5)]
        });

        var firstSheet = vm.SelectedSheet;
        var secondSheet = vm.AddSheet("Review Sheet");
        vm.PdfUnderlay = new PdfUnderlay { FilePath = "sheet2.pdf", PageNumber = 2 };
        vm.NamedViews.Add(new NamedView { Name = "Sheet 2 View", Zoom = 2.0 });
        vm.AddMarkup(new MarkupRecord
        {
            Type = MarkupType.Text,
            Vertices = [new Point(20, 20)],
            TextContent = "Review"
        });

        Assert.NotNull(firstSheet);
        Assert.NotNull(secondSheet);
        Assert.Equal("sheet2.pdf", vm.PdfUnderlay?.FilePath);
        Assert.Single(vm.Markups);

        var changed = vm.SelectSheet(firstSheet);

        Assert.True(changed);
        Assert.Equal("sheet1.pdf", vm.PdfUnderlay?.FilePath);
        Assert.Single(vm.Markups);
        Assert.Equal("Sheet 1 View", vm.NamedViews[0].Name);
    }

    [Fact]
    public void LoadFromLegacyProject_CreatesSingleDefaultSheet()
    {
        var project = new ProjectModel
        {
            Name = "Legacy Project",
            PdfUnderlay = new PdfUnderlay { FilePath = "legacy.pdf", PageNumber = 4 },
            Markups =
            [
                new MarkupRecord
                {
                    Type = MarkupType.Rectangle,
                    Vertices = [new Point(0, 0), new Point(5, 5)]
                }
            ],
            NamedViews =
            [
                new NamedView { Name = "Legacy View", Zoom = 1.5 }
            ]
        };

        var vm = new MainViewModel();
        vm.LoadFromProject(project);

        Assert.Single(vm.Sheets);
        Assert.Equal("S001 - Legacy Project", vm.SelectedSheet?.DisplayName);
        Assert.Equal("legacy.pdf", vm.PdfUnderlay?.FilePath);
        Assert.Single(vm.Markups);
        Assert.Single(vm.NamedViews);
    }

    [Fact]
    public void ToProjectModel_PersistsActiveSheetId()
    {
        var vm = new MainViewModel();
        var secondSheet = vm.AddSheet("Second");

        var project = vm.ToProjectModel();

        Assert.Equal(secondSheet.Id, project.ActiveSheetId);
    }

    [Fact]
    public void LoadFromProject_RestoresConfiguredActiveSheet()
    {
        var firstSheet = DrawingSheet.CreateDefault(1);
        firstSheet.PdfUnderlay = new PdfUnderlay { FilePath = "first.pdf", PageNumber = 1 };
        var secondSheet = DrawingSheet.CreateDefault(2);
        secondSheet.PdfUnderlay = new PdfUnderlay { FilePath = "second.pdf", PageNumber = 2 };
        var project = new ProjectModel
        {
            Sheets = [firstSheet, secondSheet],
            ActiveSheetId = secondSheet.Id
        };

        var vm = new MainViewModel();
        vm.LoadFromProject(project);

        Assert.Equal(secondSheet.Id, vm.SelectedSheet?.Id);
        Assert.Equal("second.pdf", vm.PdfUnderlay?.FilePath);
    }

    [Fact]
    public void RenameDeleteAndMoveSheet_UpdateSelectionAndOrdering()
    {
        var vm = new MainViewModel();
        var firstSheet = vm.SelectedSheet;
        var secondSheet = vm.AddSheet("Second");
        var thirdSheet = vm.AddSheet("Third");

        var renamed = vm.RenameSheet(secondSheet, "A201", "Lighting Plan");
        var moved = vm.MoveSheet(thirdSheet, -1);
        var deleted = vm.DeleteSheet(firstSheet);

        Assert.True(renamed);
        Assert.True(moved);
        Assert.True(deleted);
        Assert.Equal("A201 - Lighting Plan", secondSheet.DisplayName);
        Assert.Equal(thirdSheet.Id, vm.Sheets[0].Id);
        Assert.Equal(secondSheet.Id, vm.Sheets[1].Id);
        Assert.Equal(thirdSheet.Id, vm.SelectedSheet?.Id);
        Assert.Equal(2, vm.Sheets.Count);
    }
}

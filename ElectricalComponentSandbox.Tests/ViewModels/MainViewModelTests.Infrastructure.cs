using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;
using System.Linq;
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
    public void UpsertProjectParameter_WithFormula_ComputesDerivedValueAndBoundComponent()
    {
        var vm = new MainViewModel();
        var baseParameter = vm.UpsertProjectParameter("Base Width", 2.0);
        var derivedParameter = vm.UpsertProjectParameter("Derived Width", 0.0, formula: "[Base Width] * 2 + 0.5");
        var component = new BoxComponent();
        component.Parameters.SetBinding(ProjectParameterBindingTarget.Width, derivedParameter.Id);
        vm.Components.Add(component);

        vm.ApplyProjectParameterBindings();

        Assert.Equal(baseParameter.Id, vm.GetProjectParameter(baseParameter.Id)?.Id);
        Assert.Equal(4.5, derivedParameter.Value, 6);
        Assert.Equal(4.5, component.Parameters.Width, 6);
    }

    [Fact]
    public void UpsertProjectParameter_RenamePropagatesFormulaReferences()
    {
        var vm = new MainViewModel();
        var baseParameter = vm.UpsertProjectParameter("Base Width", 2.0);
        var derivedParameter = vm.UpsertProjectParameter("Derived Width", 0.0, formula: "[Base Width] * 2");

        vm.UpsertProjectParameter("Primary Width", 2.0, baseParameter.Id);

        var updatedBase = vm.GetProjectParameter(baseParameter.Id);
        var updatedDerived = vm.GetProjectParameter(derivedParameter.Id);
        Assert.NotNull(updatedBase);
        Assert.NotNull(updatedDerived);
        Assert.Equal("Primary Width", updatedBase!.Name);
        Assert.Equal("[Primary Width] * 2", updatedDerived!.Formula);
        Assert.Equal(4.0, updatedDerived.Value, 6);
    }

    [Fact]
    public void UpsertProjectParameter_CircularFormula_ThrowsAndLeavesExistingValuesUnchanged()
    {
        var vm = new MainViewModel();
        var first = vm.UpsertProjectParameter("Width A", 1.0);
        var second = vm.UpsertProjectParameter("Width B", 0.0, formula: "[Width A] * 2");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            vm.UpsertProjectParameter("Width A", 1.0, first.Id, formula: "[Width B] + 1"));

        Assert.Contains("Circular reference", ex.Message, StringComparison.OrdinalIgnoreCase);
        var updatedFirst = vm.GetProjectParameter(first.Id);
        var updatedSecond = vm.GetProjectParameter(second.Id);
        Assert.NotNull(updatedFirst);
        Assert.NotNull(updatedSecond);
        Assert.Equal(string.Empty, updatedFirst!.Formula);
        Assert.Equal("[Width A] * 2", updatedSecond!.Formula);
        Assert.Equal(2.0, updatedSecond.Value, 6);
    }

    [Fact]
    public void PreviewProjectParameter_WithFormula_ReturnsComputedValue()
    {
        var vm = new MainViewModel();
        vm.UpsertProjectParameter("Base Width", 2.0);

        var preview = vm.PreviewProjectParameter("Derived Width", 1.0, formula: "[Base Width] * 2.5");

        Assert.True(preview.CanSave);
        Assert.True(preview.HasFormula);
        Assert.Equal(5.0, preview.Value, 6);
        Assert.Null(preview.ErrorMessage);
    }

    [Fact]
    public void PreviewProjectParameter_WithUnknownReference_ReturnsInlineError()
    {
        var vm = new MainViewModel();

        var preview = vm.PreviewProjectParameter("Derived Width", 1.0, formula: "[Missing Width] * 2.5");

        Assert.False(preview.CanSave);
        Assert.Contains("Unknown parameter", preview.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpsertProjectParameter_WithTextValue_UpdatesBoundComponentAndRefreshesParameterTag()
    {
        var vm = new MainViewModel();
        vm.AddComponent(ComponentType.Box);
        var component = Assert.Single(vm.Components);
        var parameter = vm.UpsertProjectParameter(
            "Shared Material",
            0.0,
            valueKind: ProjectParameterValueKind.Text,
            textValue: "PVC");

        component.Parameters.SetBinding(ProjectParameterBindingTarget.Material, parameter.Id);
        vm.ApplyProjectParameterBindings();

        var tagMarkup = new DrawingAnnotationMarkupService().CreateComponentParameterTagMarkup(
            component.Id,
            ProjectParameterBindingTarget.Material,
            ProjectParameterBindingTarget.Material.GetDisplayName(),
            component.Parameters.Material,
            new Point(20, 30),
            parameter.Id);
        vm.Markups.Add(tagMarkup);

        vm.UpsertProjectParameter(
            "Shared Material",
            0.0,
            parameter.Id,
            valueKind: ProjectParameterValueKind.Text,
            textValue: "Copper");

        Assert.True(vm.TryGetProjectParameterTextValue(parameter.Id, out var updatedValue));
        Assert.Equal("Copper", updatedValue);
        Assert.Equal("Copper", component.Parameters.Material);
        Assert.Equal("Material: Copper", tagMarkup.TextContent);
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

    [Fact]
    public void ProjectBrowserItems_ReflectSheetsAndSheetScopedNamedViews()
    {
        var vm = new MainViewModel();
        vm.NamedViews.Add(new NamedView { Name = "Sheet 1 View", Zoom = 1.1 });
        var firstSheet = vm.SelectedSheet;

        var secondSheet = vm.AddSheet("Review Sheet");
        vm.NamedViews.Add(new NamedView { Name = "Sheet 2 View", Zoom = 2.0 });
        vm.RefreshProjectBrowserItems();

        Assert.NotNull(firstSheet);
        Assert.NotNull(secondSheet);
        Assert.Equal(2, vm.ProjectBrowserItems.Count);
        Assert.Equal(firstSheet.DisplayName, vm.ProjectBrowserItems[0].DisplayName);
        Assert.Equal(secondSheet.DisplayName, vm.ProjectBrowserItems[1].DisplayName);
        Assert.Single(vm.ProjectBrowserItems[0].Children);
        Assert.Single(vm.ProjectBrowserItems[1].Children);
        Assert.Equal("Sheet 1 View", vm.ProjectBrowserItems[0].Children[0].DisplayName);
        Assert.Equal("Sheet 2 View", vm.ProjectBrowserItems[1].Children[0].DisplayName);
        Assert.False(vm.ProjectBrowserItems[0].IsSelected);
        Assert.True(vm.ProjectBrowserItems[1].IsSelected);
    }

    [Fact]
    public void AddLiveScheduleInstance_RendersManagedScheduleAndPersistsToProjectModel()
    {
        var vm = new MainViewModel();
        vm.Components.Add(new BoxComponent { Name = "BOX-A" });
        var sheet = Assert.IsType<DrawingSheet>(vm.SelectedSheet);
        var instance = new LiveScheduleInstance
        {
            Kind = LiveScheduleKind.Equipment,
            Origin = new Point(120, 150)
        };

        vm.AddLiveScheduleInstance(sheet, instance);

        Assert.Single(sheet.LiveSchedules);
        Assert.Contains(vm.Markups, markup =>
            markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.LiveScheduleInstanceIdField, out var instanceId) &&
            string.Equals(instanceId, instance.Id, StringComparison.Ordinal));

        var project = vm.ToProjectModel();

        Assert.Single(project.Sheets);
        Assert.Single(project.Sheets[0].LiveSchedules);
        Assert.Equal(instance.Id, project.Sheets[0].LiveSchedules[0].Id);
    }

    [Fact]
    public void RefreshLiveScheduleMarkups_UpdatesManagedScheduleTextWhenComponentChanges()
    {
        var vm = new MainViewModel();
        var component = new BoxComponent { Name = "BOX-A" };
        vm.Components.Add(component);
        var sheet = Assert.IsType<DrawingSheet>(vm.SelectedSheet);
        var instance = new LiveScheduleInstance
        {
            Kind = LiveScheduleKind.Equipment,
            Origin = new Point(90, 110)
        };

        vm.AddLiveScheduleInstance(sheet, instance);
        component.Name = "BOX-B";

        vm.RefreshLiveScheduleMarkups();

        Assert.DoesNotContain(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "BOX-A");
        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "BOX-B");
    }

    [Fact]
    public void RefreshLiveScheduleMarkups_PreservesMovedScheduleOrigin()
    {
        var vm = new MainViewModel();
        vm.Components.Add(new BoxComponent { Name = "BOX-A" });
        var sheet = Assert.IsType<DrawingSheet>(vm.SelectedSheet);
        var instance = new LiveScheduleInstance
        {
            Kind = LiveScheduleKind.Equipment,
            Origin = new Point(60, 75)
        };
        var interactionService = new MarkupInteractionService();

        vm.AddLiveScheduleInstance(sheet, instance);

        var managedMarkups = vm.Markups.Where(markup =>
            markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.LiveScheduleInstanceIdField, out var instanceId) &&
            string.Equals(instanceId, instance.Id, StringComparison.Ordinal)).ToList();
        foreach (var markup in managedMarkups)
            interactionService.Translate(markup, new Vector(25, 35));

        vm.RefreshLiveScheduleMarkups();

        var border = Assert.Single(vm.Markups.Where(markup =>
            markup.Type == MarkupType.Rectangle &&
            string.Equals(markup.Metadata.Subject, "Table Border", StringComparison.Ordinal) &&
            markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.LiveScheduleInstanceIdField, out var instanceId) &&
            string.Equals(instanceId, instance.Id, StringComparison.Ordinal)));

        Assert.Equal(new Point(85, 110), sheet.LiveSchedules[0].Origin);
        Assert.Equal(85, border.BoundingRect.X, 6);
        Assert.Equal(110, border.BoundingRect.Y, 6);
    }

    [Fact]
    public void RefreshLiveScheduleMarkups_RemovesDeletedLiveScheduleInstances()
    {
        var vm = new MainViewModel();
        vm.Components.Add(new BoxComponent { Name = "BOX-A" });
        var sheet = Assert.IsType<DrawingSheet>(vm.SelectedSheet);
        var instance = new LiveScheduleInstance
        {
            Kind = LiveScheduleKind.Equipment,
            Origin = new Point(30, 45)
        };

        vm.AddLiveScheduleInstance(sheet, instance);

        var managedMarkups = vm.Markups.Where(markup =>
            markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.LiveScheduleInstanceIdField, out var instanceId) &&
            string.Equals(instanceId, instance.Id, StringComparison.Ordinal)).ToList();
        foreach (var markup in managedMarkups)
            vm.Markups.Remove(markup);

        vm.RefreshLiveScheduleMarkups();

        Assert.Empty(sheet.LiveSchedules);
        Assert.DoesNotContain(vm.Markups, markup =>
            markup.Metadata.CustomFields.ContainsKey(DrawingAnnotationMarkupService.LiveScheduleInstanceIdField));
    }

    [Fact]
    public void LoadFromProject_RendersLiveSchedulesUsingPersistedCircuitData()
    {
        var sheet = DrawingSheet.CreateDefault(1);
        sheet.LiveSchedules.Add(new LiveScheduleInstance
        {
            Kind = LiveScheduleKind.CircuitSummary,
            Origin = new Point(140, 160)
        });
        var project = new ProjectModel
        {
            Sheets = [sheet],
            ActiveSheetId = sheet.Id,
            Circuits =
            [
                new Circuit
                {
                    CircuitNumber = "1",
                    Description = "Lighting",
                    PanelId = "panel-1",
                    Phase = "A",
                    Voltage = 120,
                    ConnectedLoadVA = 1800,
                    WireLengthFeet = 75,
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    Wire = new WireSpec { Size = "12", Material = ConductorMaterial.Copper }
                }
            ]
        };

        var vm = new MainViewModel();

        vm.LoadFromProject(project);

        Assert.Single(vm.Circuits);
        Assert.Single(vm.SelectedSheet!.LiveSchedules);
        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "Lighting");
    }

    [Fact]
    public void AddLiveTitleBlockInstance_RendersManagedTitleBlockAndPersistsToProjectModel()
    {
        var vm = new MainViewModel
        {
            ProjectName = "Tower Renovation"
        };
        var sheet = Assert.IsType<DrawingSheet>(vm.SelectedSheet);
        var instance = new LiveTitleBlockInstance
        {
            Origin = new Point(72, 72),
            Template = new TitleBlockService().GetDefaultTemplate(PaperSizeType.ANSI_B)
        };

        vm.AddLiveTitleBlockInstance(sheet, instance);

        Assert.Single(sheet.LiveTitleBlocks);
        Assert.Contains(vm.Markups, markup =>
            markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.LiveTitleBlockInstanceIdField, out var instanceId) &&
            string.Equals(instanceId, instance.Id, StringComparison.Ordinal));
        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "Tower Renovation");

        var project = vm.ToProjectModel();

        Assert.Equal("Tower Renovation", project.Name);
        Assert.Single(project.Sheets[0].LiveTitleBlocks);
    }

    [Fact]
    public void RefreshLiveTitleBlockMarkups_UpdatesBoundFieldsWhenSheetAndProjectChange()
    {
        var vm = new MainViewModel
        {
            ProjectName = "Tower Renovation"
        };
        var sheet = Assert.IsType<DrawingSheet>(vm.SelectedSheet);
        var instance = new LiveTitleBlockInstance
        {
            Origin = new Point(72, 72),
            Template = new TitleBlockService().GetDefaultTemplate(PaperSizeType.ANSI_B)
        };

        vm.AddLiveTitleBlockInstance(sheet, instance);
        vm.RenameSheet(sheet, "E201", "Lighting Plan");
        vm.ProjectName = "Campus Upgrade";

        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "Campus Upgrade");
        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "Lighting Plan");
        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "E201");
        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "1 OF 1");
    }

    [Fact]
    public void UpdateLiveTitleBlockFieldValue_UpdatesUnboundFieldAndPreservesBindings()
    {
        var service = new TitleBlockService();
        var vm = new MainViewModel
        {
            ProjectName = "Tower Renovation"
        };
        var sheet = Assert.IsType<DrawingSheet>(vm.SelectedSheet);
        var template = service.GetDefaultTemplate(PaperSizeType.ANSI_B);
        template.DrawnBy = "Engineer A";
        var instance = new LiveTitleBlockInstance
        {
            Origin = new Point(72, 72),
            Template = template
        };

        vm.AddLiveTitleBlockInstance(sheet, instance);

        var changed = vm.UpdateLiveTitleBlockFieldValue(instance.Id, "DRAWN BY", "Engineer B");

        Assert.True(changed);
        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "Engineer B");
        Assert.DoesNotContain(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "Engineer A");
        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "Tower Renovation");
    }

    [Fact]
    public void AddSheet_RefreshesLiveTitleBlockSheetCountAcrossSheets()
    {
        var vm = new MainViewModel();
        var firstSheet = Assert.IsType<DrawingSheet>(vm.SelectedSheet);
        var instance = new LiveTitleBlockInstance
        {
            Origin = new Point(72, 72),
            Template = new TitleBlockService().GetDefaultTemplate(PaperSizeType.ANSI_B)
        };

        vm.AddLiveTitleBlockInstance(firstSheet, instance);
        vm.AddSheet("Review Sheet");

        Assert.Contains(firstSheet.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "1 OF 2");
    }

    [Fact]
    public void LoadFromProject_RendersLiveTitleBlocksUsingPersistedBindings()
    {
        var service = new TitleBlockService();
        var sheet = DrawingSheet.CreateDefault(1);
        sheet.Number = "A101";
        sheet.Name = "Floor Plan";
        sheet.LiveTitleBlocks.Add(new LiveTitleBlockInstance
        {
            Origin = new Point(72, 72),
            Template = service.GetDefaultTemplate(PaperSizeType.ANSI_B)
        });
        var project = new ProjectModel
        {
            Name = "Tower Renovation",
            Sheets = [sheet],
            ActiveSheetId = sheet.Id
        };

        var vm = new MainViewModel();

        vm.LoadFromProject(project);

        Assert.Equal("Tower Renovation", vm.ProjectName);
        Assert.Single(vm.SelectedSheet!.LiveTitleBlocks);
        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "Tower Renovation");
        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "Floor Plan");
        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent == "A101");
    }

    [Fact]
    public void AddAndRemoveSheetRevision_RefreshesLiveTitleBlockRevisionRows()
    {
        var vm = new MainViewModel
        {
            ProjectName = "Tower Renovation"
        }; 
        var sheet = Assert.IsType<DrawingSheet>(vm.SelectedSheet);
        var instance = new LiveTitleBlockInstance
        {
            Origin = new Point(72, 72),
            Template = new TitleBlockService().GetDefaultTemplate(PaperSizeType.ANSI_B)
        };

        vm.AddLiveTitleBlockInstance(sheet, instance);
        var revision = vm.AddSheetRevision(sheet, "Issued for review", "Paul", revisionNumber: "A", revisionDate: "2026-03-31");

        Assert.Contains(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent.Contains("Issued for review", StringComparison.Ordinal));

        var removed = vm.RemoveSheetRevision(sheet, revision.Id);

        Assert.True(removed);
        Assert.DoesNotContain(vm.Markups, markup => markup.Type == MarkupType.Text && markup.TextContent.Contains("Issued for review", StringComparison.Ordinal));
    }

    [Fact]
    public void SetSheetStatus_TracksApprovalMetadata()
    {
        var vm = new MainViewModel();
        var sheet = Assert.IsType<DrawingSheet>(vm.SelectedSheet);

        var changed = vm.SetSheetStatus(sheet, DrawingSheetStatus.Approved, "Reviewer");

        Assert.True(changed);
        Assert.Equal(DrawingSheetStatus.Approved, sheet.Status);
        Assert.Equal("Reviewer", sheet.ModifiedBy);
        Assert.Equal("Reviewer", sheet.ApprovedBy);
        Assert.NotNull(sheet.ApprovedUtc);
    }
}

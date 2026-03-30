using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;
using Newtonsoft.Json.Linq;

namespace ElectricalComponentSandbox.Tests;

public class MainWindowComponentSelectionTests
{
    [Fact]
    public void UpdatePropertiesPanelForTesting_MultiSelectionShowsSharedAndMixedValues()
    {
        var outcome = RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].Parameters.Width = 2.5;
            selected[1].Parameters.Width = 2.5;
            selected[0].Parameters.Material = "Steel";
            selected[1].Parameters.Material = "PVC";

            viewModel.SetSelectedComponents(selected, selected[0]);
            window.UpdatePropertiesPanelForTesting();

            var widthTextBox = FindRequired<TextBox>(window, "WidthTextBox");
            var materialTextBox = FindRequired<TextBox>(window, "MaterialTextBox");
            var summaryTextBlock = FindRequired<TextBlock>(window, "MultiSelectionSummaryTextBlock");
            var nameTextBox = FindRequired<TextBox>(window, "NameTextBox");
            var applyButton = FindRequired<Button>(window, "ApplyPropertiesButton");

            return (
                widthText: widthTextBox.Text,
                materialText: materialTextBox.Text,
                summaryVisibility: summaryTextBlock.Visibility,
                summaryText: summaryTextBlock.Text,
                nameEnabled: nameTextBox.IsEnabled,
                buttonCaption: applyButton.Content?.ToString());
        });

        Assert.Equal("2'-6\"", outcome.widthText);
        Assert.Equal(string.Empty, outcome.materialText);
        Assert.Equal(Visibility.Visible, outcome.summaryVisibility);
        Assert.Contains("Editing 2 selected components", outcome.summaryText);
        Assert.False(outcome.nameEnabled);
        Assert.Equal("Apply Shared Changes", outcome.buttonCaption);
    }

    [Fact]
    public void ApplyPropertiesForTesting_MultiSelectionUpdatesOnlyFilledSharedFields()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].Parameters.Width = 1.0;
            selected[1].Parameters.Width = 3.0;
            selected[0].Parameters.Material = "Steel";
            selected[1].Parameters.Material = "PVC";
            selected[0].Parameters.Color = "Red";
            selected[1].Parameters.Color = "Blue";

            viewModel.SetSelectedComponents(selected, selected[0]);
            window.UpdatePropertiesPanelForTesting();

            FindRequired<TextBox>(window, "WidthTextBox").Text = "4.25";
            FindRequired<TextBox>(window, "MaterialTextBox").Text = "Copper";
            FindRequired<TextBox>(window, "ColorTextBox").Text = string.Empty;

            var catalogCleared = window.ApplyPropertiesForTesting();

            Assert.False(catalogCleared);
            Assert.All(selected, component =>
            {
                Assert.Equal(4.25, component.Parameters.Width, 6);
                Assert.Equal("Copper", component.Parameters.Material);
            });
            Assert.Equal("Red", selected[0].Parameters.Color);
            Assert.Equal("Blue", selected[1].Parameters.Color);

            return 0;
        });
    }

    [Fact]
    public void TryMoveSelectedComponentsForTesting_AbsoluteModeAppliesPrimaryDeltaToAllSelected()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].Position = new Point3D(1, 2, 3);
            selected[1].Position = new Point3D(4, 5, 6);

            viewModel.SetSelectedComponents(selected, selected[0]);

            var moved = window.TryMoveSelectedComponentsForTesting(new Vector3D(10, 20, 30), isAbsolute: true);

            Assert.True(moved);
            Assert.Equal(new Point3D(10, 20, 30), selected[0].Position);
            Assert.Equal(new Point3D(13, 23, 33), selected[1].Position);

            viewModel.UndoRedo.Undo();

            Assert.Equal(new Point3D(1, 2, 3), selected[0].Position);
            Assert.Equal(new Point3D(4, 5, 6), selected[1].Position);
            return 0;
        });
    }

    [Fact]
    public void TryRotateSelectedComponentsForTesting_AppliesAngleToEntireSelection()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].Rotation = new Vector3D(0, 15, 0);
            selected[1].Rotation = new Vector3D(5, 30, 10);

            viewModel.SetSelectedComponents(selected, selected[0]);

            var rotated = window.TryRotateSelectedComponentsForTesting(45);

            Assert.True(rotated);
            Assert.Equal(60, selected[0].Rotation.Y, 6);
            Assert.Equal(75, selected[1].Rotation.Y, 6);

            viewModel.UndoRedo.Undo();

            Assert.Equal(15, selected[0].Rotation.Y, 6);
            Assert.Equal(30, selected[1].Rotation.Y, 6);
            return 0;
        });
    }

    [Fact]
    public void TryMirrorSelectedComponentsAcrossXAxisForTesting_MirrorsEntireSelectionAndUndoRestores()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].Position = new Point3D(2, 0, 3);
            selected[1].Position = new Point3D(-5, 1, 7);
            selected[0].Scale = new Vector3D(1, 1, 1);
            selected[1].Scale = new Vector3D(2, 1, 3);

            viewModel.SetSelectedComponents(selected, selected[0]);

            var mirrored = window.TryMirrorSelectedComponentsAcrossXAxisForTesting();

            Assert.True(mirrored);
            Assert.Equal(-2, selected[0].Position.X, 6);
            Assert.Equal(5, selected[1].Position.X, 6);
            Assert.Equal(-1, selected[0].Scale.X, 6);
            Assert.Equal(-2, selected[1].Scale.X, 6);

            viewModel.UndoRedo.Undo();

            Assert.Equal(2, selected[0].Position.X, 6);
            Assert.Equal(-5, selected[1].Position.X, 6);
            Assert.Equal(1, selected[0].Scale.X, 6);
            Assert.Equal(2, selected[1].Scale.X, 6);
            return 0;
        });
    }

    [Fact]
    public void DuplicateSelectedComponentsForTesting_DuplicatesEntireSelectionAndSelectsCopies()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].Position = new Point3D(1, 0, 2);
            selected[1].Position = new Point3D(4, 0, 6);
            viewModel.SetSelectedComponents(selected, selected[0]);

            var copies = window.DuplicateSelectedComponentsForTesting();

            Assert.Equal(2, copies.Count);
            Assert.Equal(4, viewModel.Components.Count);
            Assert.Collection(copies,
                first =>
                {
                    Assert.Equal("Box 1 (Copy)", first.Name);
                    Assert.Equal(new Point3D(3, 0, 4), first.Position);
                },
                second =>
                {
                    Assert.Equal("Box 2 (Copy)", second.Name);
                    Assert.Equal(new Point3D(6, 0, 8), second.Position);
                });
            Assert.Equal(2, viewModel.SelectedComponentIds.Count);
            Assert.All(copies, copy => Assert.Contains(copy.Id, viewModel.SelectedComponentIds));

            viewModel.UndoRedo.Undo();

            Assert.Equal(2, viewModel.Components.Count);
            return 0;
        });
    }

    [Fact]
    public void CreateRectangularArrayForTesting_MultiSelectionCreatesTranslatedGroupCopies()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].Position = new Point3D(1, 0, 1);
            selected[1].Position = new Point3D(3, 0, 4);
            viewModel.SetSelectedComponents(selected, selected[0]);

            var copies = window.CreateRectangularArrayForTesting(rows: 2, columns: 2, rowSpacing: 10, columnSpacing: 20);

            Assert.Equal(6, copies.Count);
            Assert.Equal(8, viewModel.Components.Count);
            Assert.Contains(copies, copy => copy.Name == "Box 1 (Array)" && copy.Position == new Point3D(21, 0, 1));
            Assert.Contains(copies, copy => copy.Name == "Box 2 (Array)" && copy.Position == new Point3D(23, 0, 4));
            Assert.Contains(copies, copy => copy.Name == "Box 1 (Array)" && copy.Position == new Point3D(1, 0, 11));
            Assert.Contains(copies, copy => copy.Name == "Box 2 (Array)" && copy.Position == new Point3D(23, 0, 14));

            viewModel.UndoRedo.Undo();

            Assert.Equal(2, viewModel.Components.Count);
            return 0;
        });
    }

    [Fact]
    public void CreatePolarArrayForTesting_MultiSelectionRotatesGroupAroundCenter()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].Position = new Point3D(10, 0, 0);
            selected[1].Position = new Point3D(10, 0, 2);
            selected[0].Rotation = new Vector3D(0, 0, 0);
            selected[1].Rotation = new Vector3D(0, 15, 0);
            viewModel.SetSelectedComponents(selected, selected[0]);

            var copies = window.CreatePolarArrayForTesting(new Point3D(0, 0, 0), count: 4, totalAngleDegrees: 360, rotateItems: true);

            Assert.Equal(6, copies.Count);
            Assert.Equal(8, viewModel.Components.Count);

            var primaryQuarterTurn = Assert.Single(copies.Where(copy => copy.Name == "Box 1 (Array)" && AreClose(copy.Position, new Point3D(0, 0, 10))));
            var secondaryQuarterTurn = Assert.Single(copies.Where(copy => copy.Name == "Box 2 (Array)" && AreClose(copy.Position, new Point3D(-2, 0, 10))));
            Assert.Equal(90, primaryQuarterTurn.Rotation.Y, 6);
            Assert.Equal(105, secondaryQuarterTurn.Rotation.Y, 6);

            viewModel.UndoRedo.Undo();

            Assert.Equal(2, viewModel.Components.Count);
            return 0;
        });
    }

    [Fact]
    public void SelectByTypeForTesting_WithMixedSelection_ReturnsAllMatchingTypes()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            var panel = CreateComponent(ComponentType.Panel, "Panel 1", 20, 0, 0, Colors.Green);
            var conduit = CreateComponent(ComponentType.Conduit, "Conduit 1", 30, 0, 0, Colors.Gray);
            viewModel.Components.Add(panel);
            viewModel.Components.Add(conduit);
            viewModel.SetSelectedComponents(new ElectricalComponent[] { selected[0], panel }, panel);

            var matches = window.SelectByTypeForTesting();

            Assert.Equal(3, matches.Count);
            Assert.Contains(matches, component => component.Name == "Box 1");
            Assert.Contains(matches, component => component.Name == "Box 2");
            Assert.Contains(matches, component => component.Name == "Panel 1");
            Assert.DoesNotContain(matches, component => component.Name == "Conduit 1");
            return 0;
        });
    }

    [Fact]
    public void SelectSimilarForTesting_WithMultiSelection_ReturnsUnionOfSimilarSignaturesAndKeepsSelection()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].LayerId = "power";
            selected[1].LayerId = "lighting";

            var peerPowerBox = CreateComponent(ComponentType.Box, "Peer Power Box", 20, 0, 0, Colors.Orange);
            peerPowerBox.LayerId = "power";
            var peerLightingBox = CreateComponent(ComponentType.Box, "Peer Lighting Box", 30, 0, 0, Colors.Purple);
            peerLightingBox.LayerId = "lighting";
            var unmatchedPanel = CreateComponent(ComponentType.Panel, "Panel 1", 40, 0, 0, Colors.Green);
            unmatchedPanel.LayerId = "power";

            viewModel.Components.Add(peerPowerBox);
            viewModel.Components.Add(peerLightingBox);
            viewModel.Components.Add(unmatchedPanel);
            viewModel.SetSelectedComponents(selected, selected[0]);

            var matches = window.SelectSimilarForTesting();

            Assert.Equal(4, matches.Count);
            Assert.Contains(matches, component => component.Name == "Box 1");
            Assert.Contains(matches, component => component.Name == "Box 2");
            Assert.Contains(matches, component => component.Name == "Peer Power Box");
            Assert.Contains(matches, component => component.Name == "Peer Lighting Box");
            Assert.DoesNotContain(matches, component => component.Name == "Panel 1");
            return 0;
        });
    }

    [Fact]
    public void ExportSelectedComponentsToJsonForTesting_WithMultiSelection_WritesJsonArray()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"ecs-export-{Guid.NewGuid():N}.json");
            try
            {
                viewModel.SetSelectedComponents(selected, selected[0]);

                window.ExportSelectedComponentsToJsonSynchronouslyForTesting(filePath);

                Assert.True(File.Exists(filePath));
                var json = File.ReadAllText(filePath);
                var array = JArray.Parse(json);
                Assert.Equal(2, array.Count);
                Assert.Equal("Box 1", array[0]?["Name"]?.Value<string>());
                Assert.Equal("Box 2", array[1]?["Name"]?.Value<string>());
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }

            return 0;
        });
    }

    [Fact]
    public void ApplyBulkPropertyChangeForTesting_WithMultiSelection_UpdatesEntireSelectionAndUndoRestores()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].LayerId = "power";
            selected[1].LayerId = "lighting";
            selected[0].Parameters.Material = "Steel";
            selected[1].Parameters.Material = "PVC";
            selected[0].Parameters.Color = "Red";
            selected[1].Parameters.Color = "Blue";
            selected[0].Parameters.Elevation = 1.0;
            selected[1].Parameters.Elevation = 2.5;
            selected[0].Parameters.LineWeightOverride = 0.35;
            selected[1].Parameters.LineWeightOverride = null;

            viewModel.SetSelectedComponents(selected, selected[1]);

            var applied = window.ApplyBulkPropertyChangeForTesting(new BulkPropertyChange
            {
                LayerId = "distribution",
                Elevation = 8.5,
                Material = "Copper",
                Color = "Orange",
                LineWeight = 0.7
            });

            Assert.True(applied);
            Assert.All(selected, component =>
            {
                Assert.Equal("distribution", component.LayerId);
                Assert.Equal(8.5, component.Parameters.Elevation, 6);
                Assert.Equal("Copper", component.Parameters.Material);
                Assert.Equal("Orange", component.Parameters.Color);
                Assert.True(component.Parameters.LineWeightOverride.HasValue);
                Assert.Equal(0.7, component.Parameters.LineWeightOverride.Value, 6);
            });

            viewModel.UndoRedo.Undo();

            Assert.Equal("power", selected[0].LayerId);
            Assert.Equal("lighting", selected[1].LayerId);
            Assert.Equal(1.0, selected[0].Parameters.Elevation, 6);
            Assert.Equal(2.5, selected[1].Parameters.Elevation, 6);
            Assert.Equal("Steel", selected[0].Parameters.Material);
            Assert.Equal("PVC", selected[1].Parameters.Material);
            Assert.Equal("Red", selected[0].Parameters.Color);
            Assert.Equal("Blue", selected[1].Parameters.Color);
            var restoredLineWeight = selected[0].Parameters.LineWeightOverride;
            Assert.True(restoredLineWeight.HasValue);
            Assert.Equal(0.35, restoredLineWeight.GetValueOrDefault(), 6);
            Assert.Null(selected[1].Parameters.LineWeightOverride);
            return 0;
        });
    }

    [Fact]
    public void ApplyBulkPropertyChangeForTesting_WithPrimaryOnlySelection_UsesFallbackSelection()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            viewModel.SelectedComponent = selected[0];
            viewModel.SelectedComponentIds.Clear();

            var applied = window.ApplyBulkPropertyChangeForTesting(new BulkPropertyChange
            {
                Material = "Aluminum"
            });

            Assert.True(applied);
            Assert.Equal("Aluminum", selected[0].Parameters.Material);
            Assert.Equal("Steel", selected[1].Parameters.Material);

            viewModel.UndoRedo.Undo();

            Assert.Equal("Steel", selected[0].Parameters.Material);
            return 0;
        });
    }

    [Fact]
    public void UpdateStatusBar_WithPrimaryOnlySelection_ShowsFallbackSelectionCountAndLayer()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].LayerId = "default";
            viewModel.SelectedComponent = selected[0];
            viewModel.SelectedComponentIds.Clear();

            window.UpdateStatusBar();

            var selectionCount = FindRequired<TextBlock>(window, "SelectionCountText");
            var activeLayer = FindRequired<TextBlock>(window, "ActiveLayerText");

            Assert.Equal("Selected: 1", selectionCount.Text);
            Assert.Equal("Layer: Default", activeLayer.Text);
            return 0;
        });
    }

    [Fact]
    public void UpdateStatusBar_WithMultiSelectionAcrossLayers_ShowsMixedLayerSummary()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].LayerId = "default";
            selected[1].LayerId = "lighting";
            if (!viewModel.Layers.Any(layer => layer.Id == "lighting"))
                viewModel.Layers.Add(new Layer { Id = "lighting", Name = "Lighting" });

            viewModel.SetSelectedComponents(selected, selected[0]);

            window.UpdateStatusBar();

            var selectionCount = FindRequired<TextBlock>(window, "SelectionCountText");
            var activeLayer = FindRequired<TextBlock>(window, "ActiveLayerText");

            Assert.Equal("Selected: 2", selectionCount.Text);
            Assert.Equal("Layer: Mixed", activeLayer.Text);
            return 0;
        });
    }

    [Fact]
    public void UpdateWorkspaceOverviewForTesting_WithNoSelection_ShowsBeginnerGuidance()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            viewModel.ClearComponentSelection();

            window.UpdateWorkspaceOverviewForTesting();

            var focusText = FindRequired<TextBlock>(window, "WorkspaceFocusTextBlock");
            var nextStepText = FindRequired<TextBlock>(window, "WorkspaceNextStepTextBlock");
            var guidanceText = FindRequired<TextBlock>(window, "WorkspaceGuidanceTextBlock");

            Assert.Contains("Nothing is selected yet", focusText.Text);
            Assert.Contains("Choose a component from the catalog or start a conduit route", nextStepText.Text);
            Assert.Contains("Start with a reference drawing", guidanceText.Text);
            return 0;
        });
    }

    [Fact]
    public void UpdateWorkspaceOverviewForTesting_WithMultiSelection_ShowsGroupEditingGuidance()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            viewModel.SetSelectedComponents(selected, selected[0]);

            window.UpdateWorkspaceOverviewForTesting();

            var focusText = FindRequired<TextBlock>(window, "WorkspaceFocusTextBlock");
            var nextStepText = FindRequired<TextBlock>(window, "WorkspaceNextStepTextBlock");
            var hintText = FindRequired<TextBlock>(window, "WorkspaceHintTextBlock");

            Assert.Contains("2 components are selected", focusText.Text);
            Assert.Contains("Refine the current selection", nextStepText.Text);
            Assert.Contains("edit the selection", hintText.Text);
            return 0;
        });
    }

    [Fact]
    public void TopMenu_UsesConsolidatedWorkflowGroups()
    {
        RunWithSelectedComponentsWindow((window, _, _) =>
        {
            var topMenu = FindRequired<Menu>(window, "TopMenu");
            var topLevelHeaders = topMenu.Items.OfType<MenuItem>().Select(item => item.Header?.ToString()).ToArray();

            Assert.Equal(new[] { "File", "Edit", "View", "Tools", "Electrical", "References", "Markup" }, topLevelHeaders);

            var fileMenu = topMenu.Items.OfType<MenuItem>().First(item => string.Equals(item.Header?.ToString(), "File", StringComparison.Ordinal));
            var editMenu = topMenu.Items.OfType<MenuItem>().First(item => string.Equals(item.Header?.ToString(), "Edit", StringComparison.Ordinal));
            var toolsMenu = topMenu.Items.OfType<MenuItem>().First(item => string.Equals(item.Header?.ToString(), "Tools", StringComparison.Ordinal));
            var markupMenu = topMenu.Items.OfType<MenuItem>().First(item => string.Equals(item.Header?.ToString(), "Markup", StringComparison.Ordinal));

            Assert.Contains(fileMenu.Items.OfType<MenuItem>(), item => string.Equals(item.Header?.ToString(), "Import", StringComparison.Ordinal));
            Assert.Contains(fileMenu.Items.OfType<MenuItem>(), item => string.Equals(item.Header?.ToString(), "Export", StringComparison.Ordinal));
            Assert.Contains(editMenu.Items.OfType<MenuItem>(), item => string.Equals(item.Header?.ToString(), "Clipboard & Delete", StringComparison.Ordinal));
            Assert.Contains(editMenu.Items.OfType<MenuItem>(), item => string.Equals(item.Header?.ToString(), "Markup Edit", StringComparison.Ordinal));
            Assert.Contains(toolsMenu.Items.OfType<MenuItem>(), item => string.Equals(item.Header?.ToString(), "Add Parts", StringComparison.Ordinal));
            Assert.Contains(toolsMenu.Items.OfType<MenuItem>(), item => string.Equals(item.Header?.ToString(), "Route & Sketch", StringComparison.Ordinal));
            Assert.Contains(markupMenu.Items.OfType<MenuItem>(), item => string.Equals(item.Header?.ToString(), "Add Annotation", StringComparison.Ordinal));
            Assert.Contains(markupMenu.Items.OfType<MenuItem>(), item => string.Equals(item.Header?.ToString(), "Review Actions", StringComparison.Ordinal));
            return 0;
        });
    }

    [Fact]
    public void UpdateContextualInspectorForTesting_WithComponentSelection_ShowsComponentInspectorMode()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            viewModel.SetSelectedComponents(selected, selected[0]);

            window.UpdateContextualInspectorForTesting();

            var titleText = FindRequired<TextBlock>(window, "InspectorTitleTextBlock");
            var summaryText = FindRequired<TextBlock>(window, "InspectorSummaryTextBlock");
            var hintText = FindRequired<TextBlock>(window, "InspectorHintTextBlock");
            var rightTabs = FindRequired<TabControl>(window, "RightPanelTabs");

            Assert.Equal("Selection Inspector", titleText.Text);
            Assert.Contains("Editing 2 selected components", summaryText.Text);
            Assert.Contains("Component Details", ((TabItem)rightTabs.SelectedItem!).Header?.ToString());
            Assert.Contains("dimensions", hintText.Text, StringComparison.OrdinalIgnoreCase);
            return 0;
        });
    }

    [Fact]
    public void UpdateContextualInspectorForTesting_WithComponentSelection_ShowsTaskActions()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            viewModel.SetSelectedComponents(selected, selected[0]);

            window.UpdateContextualInspectorForTesting();

            var taskTitle = FindRequired<TextBlock>(window, "InspectorTaskTitleTextBlock");
            var primaryButton = FindRequired<Button>(window, "InspectorPrimaryActionButton");
            var secondaryButton = FindRequired<Button>(window, "InspectorSecondaryActionButton");
            var tertiaryButton = FindRequired<Button>(window, "InspectorTertiaryActionButton");

            Assert.Equal("Current Selection Action", taskTitle.Text);
            Assert.Equal("Apply Shared Changes", primaryButton.Content?.ToString());
            Assert.Equal("Zoom Selection", secondaryButton.Content?.ToString());
            Assert.Equal("Duplicate", tertiaryButton.Content?.ToString());
            return 0;
        });
    }

    [Fact]
    public void InspectorTaskActionButton_WithComponentSelection_DuplicateActionDuplicatesSelection()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            viewModel.SetSelectedComponents(selected, selected[0]);
            window.UpdateContextualInspectorForTesting();

            var duplicateButton = FindRequired<Button>(window, "InspectorTertiaryActionButton");
            duplicateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, duplicateButton));

            Assert.Equal(4, viewModel.Components.Count);
            Assert.Equal(2, viewModel.SelectedComponentIds.Count);
            Assert.Contains(viewModel.Components, component => component.Name == "Box 1 (Copy)");
            Assert.Contains(viewModel.Components, component => component.Name == "Box 2 (Copy)");
            return 0;
        });
    }

    [Fact]
    public void UpdateCanvasGuidanceForTesting_WithoutComponents_ShowsEmptyStateInBothViews()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            viewModel.Components.Clear();
            viewModel.ClearComponentSelection();

            window.UpdateCanvasGuidanceForTesting();

            var planCard = FindRequired<Border>(window, "PlanCanvasGuidanceCard");
            var viewportCard = FindRequired<Border>(window, "ViewportGuidanceCard");
            var planTitle = FindRequired<TextBlock>(window, "PlanCanvasGuidanceTitleTextBlock");
            var viewportTitle = FindRequired<TextBlock>(window, "ViewportGuidanceTitleTextBlock");

            Assert.Equal(Visibility.Visible, planCard.Visibility);
            Assert.Equal(Visibility.Visible, viewportCard.Visibility);
            Assert.Contains("2D", planTitle.Text);
            Assert.Contains("3D", viewportTitle.Text);
            return 0;
        });
    }

    [Fact]
    public void UpdateCanvasGuidanceForTesting_WithReferenceUnderlayAndNoComponents_ShowsTraceGuidance()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            viewModel.Components.Clear();
            viewModel.ClearComponentSelection();
            viewModel.PdfUnderlay = new PdfUnderlay { FilePath = "plan.pdf", PageNumber = 1 };

            window.UpdateCanvasGuidanceForTesting();

            var planTitle = FindRequired<TextBlock>(window, "PlanCanvasGuidanceTitleTextBlock");
            var planSummary = FindRequired<TextBlock>(window, "PlanCanvasGuidanceSummaryTextBlock");
            var viewportSummary = FindRequired<TextBlock>(window, "ViewportGuidanceSummaryTextBlock");

            Assert.Contains("Reference drawing", planTitle.Text);
            Assert.Contains("underlay", planSummary.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("underlay", viewportSummary.Text, StringComparison.OrdinalIgnoreCase);
            return 0;
        });
    }

    [Fact]
    public void UpdateCanvasGuidanceForTesting_WithSketchLineMode_ShowsToolGuidanceAndPrimaryActionFinishesMode()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            viewModel.Components.Clear();
            viewModel.ClearComponentSelection();

            var sketchLineButton = FindRequired<Button>(window, "SketchLineButton");
            sketchLineButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, sketchLineButton));

            var planCard = FindRequired<Border>(window, "PlanCanvasGuidanceCard");
            var planTitle = FindRequired<TextBlock>(window, "PlanCanvasGuidanceTitleTextBlock");
            var primaryActionButton = FindRequired<Button>(window, "PlanCanvasGuidancePrimaryActionButton");
            var secondaryActionButton = FindRequired<Button>(window, "PlanCanvasGuidanceSecondaryActionButton");

            Assert.Equal(Visibility.Visible, planCard.Visibility);
            Assert.Equal("Sketch line mode", planTitle.Text);
            Assert.Equal("Finish Sketch Line", primaryActionButton.Content?.ToString());
            Assert.Equal("Cancel", secondaryActionButton.Content?.ToString());

            primaryActionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, primaryActionButton));

            Assert.Equal("Sketch Line", sketchLineButton.Content?.ToString());
            Assert.Equal("Start the 2D plan", planTitle.Text);
            return 0;
        });
    }

    [Fact]
    public void UpdateMobileTopBarForTesting_InMobileCanvasStartState_ShowsStarterSummaryAndMenu()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            viewModel.ClearComponentSelection();
            window.EnableMobileViewForTesting();
            window.SetMobilePaneForTesting("canvas");
            window.UpdateContextualInspectorForTesting();
            window.UpdateCanvasGuidanceForTesting();

            var state = window.GetMobileTopBarStateForTesting();
            var primaryMenuHeaders = window.GetMobileMenuHeadersForTesting(primaryMenu: true);

            Assert.Equal("Plan", state.SectionTitle);
            Assert.Equal("Start", state.AddLabel);
            Assert.Contains("Import a reference", state.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Import Reference", primaryMenuHeaders);
            Assert.Contains("Add Conduit", primaryMenuHeaders);
            Assert.Contains("Draw Conduit", primaryMenuHeaders);
            Assert.Contains("Review Markups", primaryMenuHeaders);
            return 0;
        });
    }

    [Fact]
    public void UpdateMobileTopBarForTesting_WithComponentSelection_ShowsActionMenuAndDuplicateWorks()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            window.EnableMobileViewForTesting();
            viewModel.SetSelectedComponents(selected, selected[0]);
            window.UpdateContextualInspectorForTesting();

            var state = window.GetMobileTopBarStateForTesting();
            var primaryMenuHeaders = window.GetMobileMenuHeadersForTesting(primaryMenu: true);

            Assert.Equal("Selection", state.SectionTitle);
            Assert.Equal("Act", state.AddLabel);
            Assert.Contains("shared edits", state.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Apply Shared Changes", primaryMenuHeaders);
            Assert.Contains("Zoom Selection", primaryMenuHeaders);
            Assert.Contains("Duplicate", primaryMenuHeaders);

            Assert.True(window.ExecuteMobileMenuItemForTesting(primaryMenu: true, header: "Duplicate"));
            Assert.Equal(4, viewModel.Components.Count);
            Assert.Contains(viewModel.Components, component => component.Name == "Box 1 (Copy)");
            Assert.Contains(viewModel.Components, component => component.Name == "Box 2 (Copy)");
            return 0;
        });
    }

    [Fact]
    public void ClearCustomDimensionsForTesting_WithMultiSelection_RemovesDimensionsForEntireSelection()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            var unselected = CreateComponent("Box 3", 10, 0, 10, Colors.Green);
            viewModel.Components.Add(unselected);

            window.AddCustomDimensionForTesting(selected[0], selected[1]);
            window.AddCustomDimensionForTesting(selected[1], unselected);
            window.AddCustomDimensionForTesting(unselected, null);

            viewModel.SetSelectedComponents(selected, selected[0]);

            var beforeUi = window.GetCustomDimensionUiStateForTesting();
            var removed = window.ClearCustomDimensionsForTesting();
            var afterUi = window.GetCustomDimensionUiStateForTesting();

            Assert.Equal("Custom dimensions on selected: 2", beforeUi.SummaryText);
            Assert.True(beforeUi.CanClear);
            Assert.Equal(2, removed);
            Assert.Equal("Custom dimensions on selected: 0", afterUi.SummaryText);
            Assert.False(afterUi.CanClear);

            var remainingAfterSelectionClear = window.ClearCustomDimensionsForTesting();
            Assert.Equal(0, remainingAfterSelectionClear);

            viewModel.ClearComponentSelection();
            var totalUi = window.GetCustomDimensionUiStateForTesting();
            Assert.Equal("Custom dimensions total: 1", totalUi.SummaryText);
            Assert.True(totalUi.CanClear);

            var removedAll = window.ClearCustomDimensionsForTesting();
            Assert.Equal(1, removedAll);
            Assert.Equal("Custom dimensions total: 0", window.GetCustomDimensionUiStateForTesting().SummaryText);
            return 0;
        });
    }

    [Fact]
    public void TryMeasureDistanceForTesting_WithSelectedComponents_Computes2DAnd3DDistance()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            selected[0].Position = new Point3D(1, 2, 3);
            selected[1].Position = new Point3D(4, 6, 7);
            viewModel.SetSelectedComponents(selected, selected[0]);

            var measured = window.TryMeasureDistanceForTesting(out var measurement);

            Assert.True(measured);
            Assert.Equal("Box 1", measurement.FirstName);
            Assert.Equal("Box 2", measurement.SecondName);
            Assert.Equal(5.0, measurement.Distance2D, 6);
            Assert.Equal(Math.Sqrt(41), measurement.Distance3D, 6);
            Assert.Equal(3.0, measurement.DeltaX, 6);
            Assert.Equal(4.0, measurement.DeltaY, 6);
            Assert.Equal(4.0, measurement.DeltaZ, 6);
            Assert.Equal(53.13010235415598, measurement.AngleDegrees, 6);
            return 0;
        });
    }

    [Fact]
    public void TryMeasureAreaForTesting_WithSelectedComponents_ComputesPlanArea()
    {
        RunWithSelectedComponentsWindow((window, viewModel, selected) =>
        {
            var third = CreateComponent("Box 3", 4, 0, 3, Colors.Green);
            viewModel.Components.Add(third);

            selected[0].Position = new Point3D(0, 0, 0);
            selected[1].Position = new Point3D(4, 0, 0);
            third.Position = new Point3D(4, 0, 3);
            viewModel.SetSelectedComponents(new ElectricalComponent[] { selected[0], selected[1], third }, selected[0]);

            var measured = window.TryMeasureAreaForTesting(out var measurement);

            Assert.True(measured);
            Assert.Equal(3, measurement.Count);
            Assert.Equal(6.0, measurement.Area, 6);
            return 0;
        });
    }

    private static T RunWithSelectedComponentsWindow<T>(Func<MainWindow, MainViewModel, ElectricalComponent[], T> action)
    {
        return RunOnSta(() =>
        {
            var viewModel = new MainViewModel();
            var first = CreateComponent("Box 1", 0, 0, 0, Colors.Red);
            var second = CreateComponent("Box 2", 5, 0, 5, Colors.Blue);
            viewModel.Components.Add(first);
            viewModel.Components.Add(second);

            var window = new MainWindow(viewModel);
            try
            {
                return action(window, viewModel, new ElectricalComponent[] { first, second });
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static T RunOnSta<T>(Func<T> action)
    {
        lock (WpfStaTestSynchronization.MainWindowLock)
        {
            T? result = default;
            Exception? exception = null;

            var thread = new Thread(() =>
            {
                try
                {
                    result = action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (exception != null)
                throw new Xunit.Sdk.XunitException($"STA test failed: {exception}");

            return result!;
        }
    }

    private static ElectricalComponent CreateComponent(ComponentType type, string name, double x, double y, double z, Color color)
    {
        ElectricalComponent component = type switch
        {
            ComponentType.Box => new BoxComponent(),
            ComponentType.Panel => new PanelComponent(),
            ComponentType.Conduit => new ConduitComponent(),
            ComponentType.Support => new SupportComponent(),
            ComponentType.CableTray => new CableTrayComponent(),
            ComponentType.Hanger => new HangerComponent(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        component.Name = name;
        component.Position = new Point3D(x, y, z);
        component.Scale = new Vector3D(1, 1, 1);
        component.Parameters = new ComponentParameters
        {
            Width = 1.0,
            Height = 1.0,
            Depth = 1.0,
            Material = "Steel",
            Elevation = 0.0,
            Color = color.ToString()
        };

        return component;
    }

    private static BoxComponent CreateComponent(string name, double x, double y, double z, Color color)
    {
        return (BoxComponent)CreateComponent(ComponentType.Box, name, x, y, z, color);
    }

    private static T FindRequired<T>(FrameworkElement root, string name) where T : class
    {
        return root.FindName(name) as T
            ?? throw new Xunit.Sdk.XunitException($"Expected control '{name}' of type {typeof(T).Name}.");
    }

    private static bool AreClose(Point3D actual, Point3D expected, double tolerance = 0.001)
    {
        return Math.Abs(actual.X - expected.X) <= tolerance &&
               Math.Abs(actual.Y - expected.Y) <= tolerance &&
               Math.Abs(actual.Z - expected.Z) <= tolerance;
    }
}

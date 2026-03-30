using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.ViewModels;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private void MarkupStatusFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.MarkupTool.StatusFilter = MarkupStatusFilterCombo.SelectedItem?.ToString() ?? "All";
    }

    private void MarkupTypeFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.MarkupTool.TypeFilter = MarkupTypeFilterCombo.SelectedItem?.ToString() ?? "All";
    }

    private void MarkupLayerFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.MarkupTool.LayerFilter = MarkupLayerFilterCombo.SelectedItem?.ToString() ?? "All";
    }

    private void MarkupReviewScope_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (MarkupReviewScopeCombo.SelectedItem is MarkupReviewScope scope)
            _viewModel.MarkupTool.ReviewScope = scope;
    }

    private void MarkupSearch_Changed(object sender, TextChangedEventArgs e)
    {
        _viewModel.MarkupTool.LabelSearch = MarkupSearchBox.Text;
    }

    private void MarkupListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel.MarkupTool.SelectedMarkup == null)
            return;

        if (_viewModel.RevealMarkup(_viewModel.MarkupTool.SelectedMarkup))
            QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
    }

    private void LayerDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LayerDataGrid.SelectedItem is LayerRowViewModel row)
            _viewModel.ActiveLayer = row.Layer;
    }

    private void AddComponentWithUndo(ElectricalComponent component)
    {
        if (_viewModel.ActiveLayer != null)
            component.LayerId = _viewModel.ActiveLayer.Id;
        if (component is ConduitComponent conduit)
            ApplyImperialDefaultsToConduit(conduit);

        var action = new AddComponentAction(_viewModel.Components, component);
        _viewModel.UndoRedo.Execute(action);
        _viewModel.SelectSingleComponent(component);
    }

    private void DeleteComponent_Click(object sender, RoutedEventArgs e)
    {
        if (DeleteSelectedMarkupSelection())
            return;

        _viewModel.DeleteSelectedComponent();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Undo();
        UpdateViewport();
        Update2DCanvas();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Redo();
        UpdateViewport();
        Update2DCanvas();
    }

    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        var name = $"Layer {_viewModel.Layers.Count + 1}";
        _viewModel.AddLayer(name);
    }

    private void RemoveLayer_Click(object sender, RoutedEventArgs e)
    {
        if (LayerListBox.SelectedItem is Layer layer)
        {
            _viewModel.RemoveLayer(layer);
        }
    }

    private void LayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LayerListBox.SelectedItem is Layer layer)
        {
            ActionLogService.Instance.Log(LogCategory.Layer, "Active layer changed",
                $"Name: {layer.Name}, Id: {layer.Id}");
            _viewModel.ActiveLayer = layer;
        }
    }

    private void UnitSystem_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel == null)
            return;

        if (UnitSystemCombo?.SelectedItem is ComboBoxItem item)
        {
            var system = item.Content?.ToString() ?? "Imperial";
            ActionLogService.Instance.Log(LogCategory.View, "Unit system changed", $"System: {system}");
            _viewModel.UnitSystemName = system;
            UpdatePropertiesPanel();
        }
    }

    private void DimensionDisplayFormat_Changed(object sender, SelectionChangedEventArgs e)
    {
        _dimensionDisplayMode = GetSelectedDimensionDisplayMode();
        if (DataContext is not MainViewModel)
            return;

        ActionLogService.Instance.Log(LogCategory.View, "Dimension display format changed",
            $"Mode: {_dimensionDisplayMode}");
        UpdatePropertiesPanel();
    }

    private void DimensionIncrement_Changed(object sender, SelectionChangedEventArgs e)
    {
        _dimensionInchFractionDenominator = GetSelectedDimensionIncrementDenominator();
        if (DataContext is not MainViewModel)
            return;

        ActionLogService.Instance.Log(LogCategory.View, "Dimension display increment changed",
            $"Increment: 1/{_dimensionInchFractionDenominator}\"");
        UpdatePropertiesPanel();
    }

    private void AddCustomDimension_Click(object sender, RoutedEventArgs e)
    {
        _isAddingCustomDimension = !_isAddingCustomDimension;
        _pendingCustomDimensionStartAnchor = null;
        _customDimensionPlacementState.Reset();
        ClearCustomDimensionPreview();

        ActionLogService.Instance.Log(LogCategory.View, "Custom dimension mode toggled",
            $"Enabled: {_isAddingCustomDimension}");

        if (_isAddingCustomDimension && Viewport.IsMouseOver)
            UpdateCustomDimensionPreview(Mouse.GetPosition(Viewport));

        UpdateCustomDimensionUiState();
    }

    private void CustomDimensionSnapMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        _customDimensionSnapMode = GetSelectedCustomDimensionSnapMode();
        _customDimensionPlacementState.LastPreviewSnap = null;
        ActionLogService.Instance.Log(LogCategory.View, "Custom dimension snap mode changed",
            $"Mode: {_customDimensionSnapMode}");
        if (_isAddingCustomDimension && Viewport.IsMouseOver)
            UpdateCustomDimensionPreview(Mouse.GetPosition(Viewport));
        UpdateCustomDimensionUiState();
    }

    private void ClearCustomDimensions_Click(object sender, RoutedEventArgs e)
    {
        var selectedComponents = GetSelectedComponents();
        var removed = RemoveCustomDimensionsForSelection(selectedComponents);

        if (removed > 0)
        {
            var selectionScope = selectedComponents.Count == 0
                ? "all components"
                : $"selected count: {selectedComponents.Count}";
            ActionLogService.Instance.Log(LogCategory.Edit, "Custom dimensions cleared",
                $"Removed: {removed}, Scope: {selectionScope}");
            UpdateViewport();
        }

        _pendingCustomDimensionStartAnchor = null;
        _customDimensionPlacementState.Reset();
        ClearCustomDimensionPreview();
        UpdateCustomDimensionUiState();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    internal static bool IsEditSelectedMarkupGeometryShortcut(Key key, ModifierKeys modifiers)
        => modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.G;

    internal static bool IsEditSelectedMarkupAppearanceShortcut(Key key, ModifierKeys modifiers)
        => modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.A;

    internal static bool IsEditSelectedStructuredMarkupTextShortcut(Key key, ModifierKeys modifiers)
        => modifiers == ModifierKeys.None && key == Key.F2;

    internal bool ExecuteEscapeShortcutForTesting()
        => TryCancelActiveInteraction(this, new RoutedEventArgs());

    internal bool ApplyPropertiesForTesting()
    {
        var selectedComponents = GetSelectedComponents();
        if (selectedComponents.Count == 0)
            return false;

        var component = _viewModel.SelectedComponent ?? selectedComponents[0];
        var catalogDataCleared = selectedComponents.Count == 1
            ? ApplySingleComponentProperties(component)
            : ApplySharedPropertiesToSelection(selectedComponents);

        _viewModel.RefreshComponentParameterTagMarkups();

        UpdateViewport();
        Update2DCanvas();
        UpdatePropertiesPanel();
        return catalogDataCleared;
    }

    private void ApplyProperties_Click(object sender, RoutedEventArgs e)
    {
        var selectedComponents = GetSelectedComponents();
        if (selectedComponents.Count == 0)
            return;

        var component = _viewModel.SelectedComponent ?? selectedComponents[0];

        ActionLogService.Instance.Log(LogCategory.Property, "Applying property changes",
            $"Primary: {component.Name}, Count: {selectedComponents.Count}");

        try
        {
            var catalogDataCleared = ApplyPropertiesForTesting();
            ActionLogService.Instance.Log(LogCategory.Property, "Properties applied",
                $"Primary: {component.Name}, Count: {selectedComponents.Count}");
            var successMessage = selectedComponents.Count == 1
                ? (catalogDataCleared
                    ? "Properties updated. Catalog metadata was cleared because dimensions no longer match the validated catalog size."
                    : "Properties updated successfully!")
                : (catalogDataCleared
                    ? $"Updated {selectedComponents.Count} components. Catalog metadata was cleared where edited dimensions no longer matched validated catalog sizes."
                    : $"Updated {selectedComponents.Count} components successfully!");
            MessageBox.Show(successMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.Property, "Failed to apply properties", ex);
            MessageBox.Show($"Error updating properties: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ApplySingleComponentProperties(ElectricalComponent component)
    {
        component.Name = NameTextBox.Text;

        component.Position = new Point3D(
            ParseLengthInput(PositionXTextBox.Text, "Position X"),
            ParseLengthInput(PositionYTextBox.Text, "Position Y"),
            ParseLengthInput(PositionZTextBox.Text, "Position Z"));

        component.Rotation = new Vector3D(
            double.Parse(RotationXTextBox.Text),
            double.Parse(RotationYTextBox.Text),
            double.Parse(RotationZTextBox.Text));

        var stagedProjectParameterValues = new Dictionary<string, (double Value, string FieldName)>(StringComparer.Ordinal);
        var stagedProjectParameterTextValues = new Dictionary<string, (string Value, string FieldName)>(StringComparer.Ordinal);
        var impactedCatalogParameterIds = new HashSet<string>(StringComparer.Ordinal);
        var projectParametersTouched = false;

        ApplySingleLengthField(component, WidthTextBox, WidthParameterBindingComboBox, ProjectParameterBindingTarget.Width, "Width",
            stagedProjectParameterValues, impactedCatalogParameterIds, ref projectParametersTouched);
        ApplySingleLengthField(component, HeightTextBox, HeightParameterBindingComboBox, ProjectParameterBindingTarget.Height, "Height",
            stagedProjectParameterValues, impactedCatalogParameterIds, ref projectParametersTouched);
        ApplySingleLengthField(component, DepthTextBox, DepthParameterBindingComboBox, ProjectParameterBindingTarget.Depth, "Depth",
            stagedProjectParameterValues, impactedCatalogParameterIds, ref projectParametersTouched);
        ApplySingleLengthField(component, ElevationTextBox, ElevationParameterBindingComboBox, ProjectParameterBindingTarget.Elevation, "Elevation",
            stagedProjectParameterValues, impactedDimensionalParameterIds: null, ref projectParametersTouched);

        ApplySingleTextField(component, MaterialTextBox, MaterialParameterBindingComboBox, ProjectParameterBindingTarget.Material, "Material",
            stagedProjectParameterTextValues, ref projectParametersTouched);
        component.Parameters.Color = ColorTextBox.Text;
        ApplySingleTextField(component, ManufacturerTextBox, ManufacturerParameterBindingComboBox, ProjectParameterBindingTarget.Manufacturer, "Manufacturer",
            stagedProjectParameterTextValues, ref projectParametersTouched);
        ApplySingleTextField(component, PartNumberTextBox, PartNumberParameterBindingComboBox, ProjectParameterBindingTarget.PartNumber, "Part Number",
            stagedProjectParameterTextValues, ref projectParametersTouched);
        ApplySingleTextField(component, ReferenceUrlTextBox, ReferenceUrlParameterBindingComboBox, ProjectParameterBindingTarget.ReferenceUrl, "Reference URL",
            stagedProjectParameterTextValues, ref projectParametersTouched);

        var changedParameterIds = CommitProjectParameterValueChanges(stagedProjectParameterValues);
        changedParameterIds.UnionWith(CommitProjectParameterTextValueChanges(stagedProjectParameterTextValues));
        if (projectParametersTouched || changedParameterIds.Count > 0)
            _viewModel.ApplyProjectParameterBindings();

        if (component is ConduitComponent conduitComponent)
            ApplyImperialDefaultsToConduit(conduitComponent);

        var catalogDataCleared = ClearCatalogMetadataIfDimensionsChanged(component);
        foreach (var impactedComponent in GetComponentsImpactedByProjectParameters(impactedCatalogParameterIds))
        {
            if (ReferenceEquals(impactedComponent, component))
                continue;

            catalogDataCleared |= ClearCatalogMetadataIfDimensionsChanged(impactedComponent);
        }

        if (LayerComboBox.SelectedItem is Layer layer)
            component.LayerId = layer.Id;

        return catalogDataCleared;
    }

    private bool ApplySharedPropertiesToSelection(IReadOnlyList<ElectricalComponent> components)
    {
        var stagedProjectParameterValues = new Dictionary<string, (double Value, string FieldName)>(StringComparer.Ordinal);
        var stagedProjectParameterTextValues = new Dictionary<string, (string Value, string FieldName)>(StringComparer.Ordinal);
        var impactedCatalogParameterIds = new HashSet<string>(StringComparer.Ordinal);
        var projectParametersTouched = false;

        var applyWidth = TryApplySharedLengthField(components, WidthTextBox, WidthParameterBindingComboBox, ProjectParameterBindingTarget.Width, "Width",
            stagedProjectParameterValues, impactedCatalogParameterIds, ref projectParametersTouched);
        var applyHeight = TryApplySharedLengthField(components, HeightTextBox, HeightParameterBindingComboBox, ProjectParameterBindingTarget.Height, "Height",
            stagedProjectParameterValues, impactedCatalogParameterIds, ref projectParametersTouched);
        var applyDepth = TryApplySharedLengthField(components, DepthTextBox, DepthParameterBindingComboBox, ProjectParameterBindingTarget.Depth, "Depth",
            stagedProjectParameterValues, impactedCatalogParameterIds, ref projectParametersTouched);
        var applyElevation = TryApplySharedLengthField(components, ElevationTextBox, ElevationParameterBindingComboBox, ProjectParameterBindingTarget.Elevation, "Elevation",
            stagedProjectParameterValues, impactedDimensionalParameterIds: null, ref projectParametersTouched);
        var applyMaterial = TryApplySharedTextField(components, MaterialTextBox, MaterialParameterBindingComboBox, ProjectParameterBindingTarget.Material, "Material",
            stagedProjectParameterTextValues, ref projectParametersTouched);
        var applyColor = !string.IsNullOrWhiteSpace(ColorTextBox.Text);
        var applyManufacturer = TryApplySharedTextField(components, ManufacturerTextBox, ManufacturerParameterBindingComboBox, ProjectParameterBindingTarget.Manufacturer, "Manufacturer",
            stagedProjectParameterTextValues, ref projectParametersTouched);
        var applyPartNumber = TryApplySharedTextField(components, PartNumberTextBox, PartNumberParameterBindingComboBox, ProjectParameterBindingTarget.PartNumber, "Part Number",
            stagedProjectParameterTextValues, ref projectParametersTouched);
        var applyReferenceUrl = TryApplySharedTextField(components, ReferenceUrlTextBox, ReferenceUrlParameterBindingComboBox, ProjectParameterBindingTarget.ReferenceUrl, "Reference URL",
            stagedProjectParameterTextValues, ref projectParametersTouched);
        var applyLayer = LayerComboBox.SelectedItem is Layer;

        if (!applyWidth && !applyHeight && !applyDepth && !applyMaterial && !applyElevation && !applyColor &&
            !applyManufacturer && !applyPartNumber && !applyReferenceUrl && !applyLayer)
        {
            MessageBox.Show("Enter one or more shared property values or choose a project-parameter binding to apply to the current selection.",
                "Apply Shared Changes", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        var changedParameterIds = CommitProjectParameterValueChanges(stagedProjectParameterValues);
        changedParameterIds.UnionWith(CommitProjectParameterTextValueChanges(stagedProjectParameterTextValues));
        if (projectParametersTouched || changedParameterIds.Count > 0)
            _viewModel.ApplyProjectParameterBindings();

        var layer = LayerComboBox.SelectedItem as Layer;
        var catalogDataCleared = false;

        foreach (var component in components)
        {
            if (applyColor)
                component.Parameters.Color = ColorTextBox.Text;
            if (layer != null)
                component.LayerId = layer.Id;

            if (component is ConduitComponent conduitComponent)
                ApplyImperialDefaultsToConduit(conduitComponent);

            catalogDataCleared |= ClearCatalogMetadataIfDimensionsChanged(component);
        }

        if (projectParametersTouched || changedParameterIds.Count > 0)
        {
            foreach (var impactedComponent in GetComponentsImpactedByProjectParameters(impactedCatalogParameterIds))
            {
                if (components.Contains(impactedComponent))
                    continue;

                catalogDataCleared |= ClearCatalogMetadataIfDimensionsChanged(impactedComponent);
            }
        }

        return catalogDataCleared;
    }

    private void ApplySingleTextField(
        ElectricalComponent component,
        TextBox textBox,
        ComboBox bindingComboBox,
        ProjectParameterBindingTarget target,
        string fieldName,
        Dictionary<string, (string Value, string FieldName)> stagedProjectParameterValues,
        ref bool projectParametersTouched)
    {
        var value = textBox.Text ?? string.Empty;
        var selectedParameterId = TryGetExplicitProjectParameterBindingSelection(bindingComboBox, out var bindingId)
            ? bindingId
            : null;

        component.Parameters.SetBinding(target, selectedParameterId);
        if (!string.IsNullOrWhiteSpace(selectedParameterId))
        {
            StageProjectParameterTextValueChange(selectedParameterId, value, fieldName, stagedProjectParameterValues);
            projectParametersTouched = true;
            return;
        }

        component.Parameters.SetTextValue(target, value);
    }

    private void ApplySingleLengthField(
        ElectricalComponent component,
        TextBox textBox,
        ComboBox bindingComboBox,
        ProjectParameterBindingTarget target,
        string fieldName,
        Dictionary<string, (double Value, string FieldName)> stagedProjectParameterValues,
        ISet<string>? impactedDimensionalParameterIds,
        ref bool projectParametersTouched)
    {
        var value = ParseLengthInput(textBox.Text, fieldName);
        var selectedParameterId = TryGetExplicitProjectParameterBindingSelection(bindingComboBox, out var bindingId)
            ? bindingId
            : null;

        component.Parameters.SetBinding(target, selectedParameterId);
        if (!string.IsNullOrWhiteSpace(selectedParameterId))
        {
            StageProjectParameterValueChange(selectedParameterId, value, fieldName, stagedProjectParameterValues, impactedDimensionalParameterIds);
            projectParametersTouched = true;
            return;
        }

        SetLengthValue(component, target, value);
    }

    private bool TryApplySharedLengthField(
        IReadOnlyList<ElectricalComponent> components,
        TextBox textBox,
        ComboBox bindingComboBox,
        ProjectParameterBindingTarget target,
        string fieldName,
        Dictionary<string, (double Value, string FieldName)> stagedProjectParameterValues,
        ISet<string>? impactedDimensionalParameterIds,
        ref bool projectParametersTouched)
    {
        var hasValueInput = !string.IsNullOrWhiteSpace(textBox.Text);
        var hasExplicitBindingSelection = TryGetExplicitProjectParameterBindingSelection(bindingComboBox, out var selectedParameterId);
        var bindingSelectionChangesAny = hasExplicitBindingSelection && components.Any(component =>
            !string.Equals(component.Parameters.GetBinding(target), selectedParameterId, StringComparison.Ordinal));

        if (!hasValueInput && !bindingSelectionChangesAny)
            return false;

        double? parsedValue = hasValueInput ? ParseLengthInput(textBox.Text, fieldName) : null;
        foreach (var component in components)
        {
            var bindingIdToApply = component.Parameters.GetBinding(target);
            if (bindingSelectionChangesAny)
            {
                bindingIdToApply = selectedParameterId;
                component.Parameters.SetBinding(target, selectedParameterId);
            }

            if (!string.IsNullOrWhiteSpace(bindingIdToApply))
            {
                if (parsedValue.HasValue)
                    StageProjectParameterValueChange(bindingIdToApply, parsedValue.Value, fieldName, stagedProjectParameterValues, impactedDimensionalParameterIds);

                projectParametersTouched = true;
            }
            else if (parsedValue.HasValue)
            {
                SetLengthValue(component, target, parsedValue.Value);
            }
        }

        return hasValueInput || bindingSelectionChangesAny;
    }

    private bool TryApplySharedTextField(
        IReadOnlyList<ElectricalComponent> components,
        TextBox textBox,
        ComboBox bindingComboBox,
        ProjectParameterBindingTarget target,
        string fieldName,
        Dictionary<string, (string Value, string FieldName)> stagedProjectParameterValues,
        ref bool projectParametersTouched)
    {
        var hasValueInput = !string.IsNullOrWhiteSpace(textBox.Text);
        var hasExplicitBindingSelection = TryGetExplicitProjectParameterBindingSelection(bindingComboBox, out var selectedParameterId);
        var bindingSelectionChangesAny = hasExplicitBindingSelection && components.Any(component =>
            !string.Equals(component.Parameters.GetBinding(target), selectedParameterId, StringComparison.Ordinal));

        if (!hasValueInput && !bindingSelectionChangesAny)
            return false;

        var parsedValue = hasValueInput ? textBox.Text ?? string.Empty : null;
        foreach (var component in components)
        {
            var bindingIdToApply = component.Parameters.GetBinding(target);
            if (bindingSelectionChangesAny)
            {
                bindingIdToApply = selectedParameterId;
                component.Parameters.SetBinding(target, selectedParameterId);
            }

            if (!string.IsNullOrWhiteSpace(bindingIdToApply))
            {
                if (parsedValue != null)
                    StageProjectParameterTextValueChange(bindingIdToApply, parsedValue, fieldName, stagedProjectParameterValues);

                projectParametersTouched = true;
            }
            else if (parsedValue != null)
            {
                component.Parameters.SetTextValue(target, parsedValue);
            }
        }

        return hasValueInput || bindingSelectionChangesAny;
    }

    private static bool ClearCatalogMetadataIfDimensionsChanged(ElectricalComponent component)
    {
        var parameters = component.Parameters;
        if (!parameters.CatalogWidth.HasValue || !parameters.CatalogHeight.HasValue || !parameters.CatalogDepth.HasValue)
            return false;

        var matchesCatalog =
            Math.Abs(parameters.Width - parameters.CatalogWidth.Value) <= CatalogDimensionTolerance &&
            Math.Abs(parameters.Height - parameters.CatalogHeight.Value) <= CatalogDimensionTolerance &&
            Math.Abs(parameters.Depth - parameters.CatalogDepth.Value) <= CatalogDimensionTolerance;

        if (matchesCatalog)
            return false;

        if (string.IsNullOrWhiteSpace(parameters.Manufacturer) &&
            string.IsNullOrWhiteSpace(parameters.PartNumber) &&
            string.IsNullOrWhiteSpace(parameters.ReferenceUrl))
            return false;

        parameters.SetBinding(ProjectParameterBindingTarget.Manufacturer, null);
        parameters.SetBinding(ProjectParameterBindingTarget.PartNumber, null);
        parameters.SetBinding(ProjectParameterBindingTarget.ReferenceUrl, null);
        parameters.Manufacturer = string.Empty;
        parameters.PartNumber = string.Empty;
        parameters.ReferenceUrl = string.Empty;
        return true;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;

        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key == Key.S)
            {
                SaveProjectAs_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.E)
            {
                ZoomExtents_Click(sender, e);
                e.Handled = true;
            }
            else if (IsEditSelectedMarkupGeometryShortcut(e.Key, modifiers))
            {
                TryEditSelectedMarkupGeometry(showFeedbackIfUnsupported: true);
                e.Handled = true;
            }
            else if (IsEditSelectedMarkupAppearanceShortcut(e.Key, modifiers))
            {
                TryEditSelectedMarkupAppearance(showFeedbackIfUnsupported: true);
                e.Handled = true;
            }

            return;
        }

        if (modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    NewProject_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.O:
                    OpenProject_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.S:
                    SaveProject_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.E:
                    ExportJson_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.Z:
                    Undo_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.Y:
                    Redo_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.D:
                    DuplicateComponent_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.X:
                    CutComponent_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.C:
                    CopyComponent_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.V:
                    PasteComponent_Click(sender, e);
                    e.Handled = true;
                    break;
            }

            return;
        }

        if (modifiers != ModifierKeys.None)
            return;

        if (IsDeleteSelectedMarkupOrComponentShortcut(e.Key, modifiers))
        {
            if (!DeleteSelectedMarkupVertex())
                DeleteComponent_Click(sender, e);
            e.Handled = true;
        }
        else if (IsEditSelectedStructuredMarkupTextShortcut(e.Key, modifiers))
        {
            TryEditSelectedStructuredMarkupText(showFeedbackIfUnsupported: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Home)
        {
            HomeView_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.F3 || e.Key == Key.F8 || e.Key == Key.F10)
        {
            _canvasInteractionController?.OnKeyDown(e.Key, modifiers);
            SyncViewModelInteractionStateFromCanvasInteraction();
            SyncOsnapToolbarState();
            SkiaBackground.RequestRedraw();
            e.Handled = true;
        }
        else if (IsCancelActiveInteractionShortcut(e.Key, modifiers) && TryCancelActiveInteraction(sender, e))
        {
            e.Handled = true;
        }
    }

    internal static bool IsDeleteSelectedMarkupOrComponentShortcut(Key key, ModifierKeys modifiers)
    {
        return modifiers == ModifierKeys.None && (key == Key.Delete || key == Key.Back);
    }

    private bool TryCancelActiveInteraction(object sender, RoutedEventArgs e)
    {
        if (_isPdfCalibrationMode)
        {
            CancelPdfCalibrationMode();
            return true;
        }

        if (_isFreehandDrawing)
        {
            FinishFreehandConduit();
            return true;
        }

        if (_isSketchLineMode || _isSketchRectangleMode)
        {
            ExitSketchModes();
            Update2DCanvas();
            return true;
        }

        if (_isDrawingConduit)
        {
            FinishDrawingConduit();
            return true;
        }

        if (_isEditingConduitPath)
        {
            ToggleEditConduitPath_Click(sender, e);
            return true;
        }

        if (_pendingPlacementComponent != null)
        {
            CancelPendingPlacement();
            return true;
        }

        if (_isPendingMarkupVertexInsertion)
        {
            CancelPendingMarkupVertexInsertion();
            return true;
        }

        if (TryCancelActiveMarkupDragInteraction())
            return true;

        if (_isAddingCustomDimension)
        {
            CancelCustomDimensionMode();
            UpdateCustomDimensionUiState();
            return true;
        }

        return false;
    }

    internal static bool IsCancelActiveInteractionShortcut(Key key, ModifierKeys modifiers)
    {
        return modifiers == ModifierKeys.None && key == Key.Escape;
    }

    private void OpenReference_Click(object sender, RoutedEventArgs e)
    {
        TryOpenReferenceTarget(ReferenceUrlTextBox.Text, missingReferenceMessage: "No reference URL is set for this component.");
    }
}

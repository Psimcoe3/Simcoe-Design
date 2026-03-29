using System;
using System.Diagnostics;
using System.Windows;
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

    private void ApplyProperties_Click(object sender, RoutedEventArgs e)
    {
        var component = _viewModel.SelectedComponent;
        if (component == null)
            return;

        ActionLogService.Instance.Log(LogCategory.Property, "Applying property changes",
            $"Component: {component.Name}, Type: {component.Type}");

        try
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

            component.Parameters.Width = ParseLengthInput(WidthTextBox.Text, "Width");
            component.Parameters.Height = ParseLengthInput(HeightTextBox.Text, "Height");
            component.Parameters.Depth = ParseLengthInput(DepthTextBox.Text, "Depth");
            component.Parameters.Material = MaterialTextBox.Text;
            component.Parameters.Elevation = ParseLengthInput(ElevationTextBox.Text, "Elevation");
            component.Parameters.Color = ColorTextBox.Text;
            component.Parameters.Manufacturer = ManufacturerTextBox.Text;
            component.Parameters.PartNumber = PartNumberTextBox.Text;
            component.Parameters.ReferenceUrl = ReferenceUrlTextBox.Text;

            if (component is ConduitComponent conduitComponent)
                ApplyImperialDefaultsToConduit(conduitComponent);

            var catalogDataCleared = ClearCatalogMetadataIfDimensionsChanged(component);
            if (LayerComboBox.SelectedItem is Layer layer)
                component.LayerId = layer.Id;

            UpdateViewport();
            Update2DCanvas();
            UpdatePropertiesPanel();
            ActionLogService.Instance.Log(LogCategory.Property, "Properties applied",
                $"Name: {component.Name}, Pos: ({component.Position.X:F2}, {component.Position.Y:F2}, {component.Position.Z:F2}), " +
                $"Material: {component.Parameters.Material}, Color: {component.Parameters.Color}, " +
                $"Mfr: {component.Parameters.Manufacturer}, Part#: {component.Parameters.PartNumber}");
            var successMessage = catalogDataCleared
                ? "Properties updated. Catalog metadata was cleared because dimensions no longer match the validated catalog size."
                : "Properties updated successfully!";
            MessageBox.Show(successMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.Property, "Failed to apply properties", ex);
            MessageBox.Show($"Error updating properties: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        var url = ReferenceUrlTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("No reference URL is set for this component.", "Reference", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open URL: {ex.Message}", "Reference", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.RevitIntrospection;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "New project requested");
        if (MessageBox.Show("Create a new project? Any unsaved changes will be lost.", "New Project", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            CancelPendingPlacement(logCancellation: false);
            CancelCustomDimensionMode();
            _customDimensionAnnotations.Clear();
            ClearMarkupSelection();
            _viewModel.Components.Clear();
            _viewModel.ClearProjectParameters();
            _viewModel.Layers.Clear();
            _viewModel.ResetDrawingSheets();
            _viewModel.UndoRedo.Clear();
            _currentFilePath = null;
            Title = "Electrical Component Sandbox";
            
            // Re-initialize default layer
            var defaultLayer = Layer.CreateDefault();
            _viewModel.Layers.Add(defaultLayer);
            _viewModel.ActiveLayer = defaultLayer;
            SyncSheetBrowserSelection();
            RebuildNamedViewMenuItems();
            ActionLogService.Instance.Log(LogCategory.FileOperation, "New project created");
            
            UpdateViewport();
            Update2DCanvas();
        }
    }
    
    private async void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Open project dialog requested");
        var dialog = new OpenFileDialog
        {
            Filter = Services.ProjectFileService.GetFileFilter(),
            Title = "Open Project File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var project = await _viewModel.ProjectFileService.LoadProjectAsync(dialog.FileName);
                if (project != null)
                {
                    CancelPendingPlacement(logCancellation: false);
                    CancelCustomDimensionMode();
                    _customDimensionAnnotations.Clear();
                    ClearMarkupSelection();
                    _viewModel.LoadFromProject(project);
                    _currentFilePath = dialog.FileName;
                    Title = $"Electrical Component Sandbox - {System.IO.Path.GetFileName(dialog.FileName)}";
                    SyncSheetBrowserSelection();
                    RebuildNamedViewMenuItems();
                    ActionLogService.Instance.Log(LogCategory.FileOperation, "Project opened", $"File: {dialog.FileName}");
                    UpdateViewport();
                    Update2DCanvas();
                }
            }
            catch (Exception ex)
            {
                ActionLogService.Instance.LogError(LogCategory.FileOperation, "Failed to open project", ex);
                MessageBox.Show($"Error opening project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private async void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Save project requested",
            $"Current path: {_currentFilePath ?? "(none)"}");
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveProjectAs_Click(sender, e);
            return;
        }
        
        await SaveProjectAsync(_currentFilePath);
    }
    
    private async void SaveProjectAs_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Save As dialog requested");
        var dialog = new SaveFileDialog
        {
            Filter = Services.ProjectFileService.GetFileFilter(),
            Title = "Save Project File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            await SaveProjectAsync(dialog.FileName);
            _currentFilePath = dialog.FileName;
            Title = $"Electrical Component Sandbox - {System.IO.Path.GetFileName(dialog.FileName)}";
        }
    }
    
    private async Task SaveProjectAsync(string filePath)
    {
        try
        {
            var project = _viewModel.ToProjectModel();
            await _viewModel.ProjectFileService.SaveProjectAsync(project, filePath);
            ActionLogService.Instance.Log(LogCategory.FileOperation, "Project saved", $"File: {filePath}");
            MessageBox.Show("Project saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.FileOperation, "Failed to save project", ex);
            MessageBox.Show($"Error saving project: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Export JSON requested");
        var selectedComponents = GetSelectedComponents();
        if (selectedComponents.Count == 0)
        {
            ActionLogService.Instance.Log(LogCategory.FileOperation, "Export JSON aborted", "No component selected");
            MessageBox.Show("Please select one or more components to export.", "No Component", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Export to JSON"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await ExportSelectedComponentsToJsonForTesting(dialog.FileName);
                ActionLogService.Instance.Log(LogCategory.FileOperation, "JSON exported", $"File: {dialog.FileName}");
                var successMessage = selectedComponents.Count == 1
                    ? "Component exported successfully!"
                    : $"Exported {selectedComponents.Count} components successfully!";
                MessageBox.Show(successMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ActionLogService.Instance.LogError(LogCategory.FileOperation, "Failed to export JSON", ex);
                MessageBox.Show($"Error exporting file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    internal async Task ExportSelectedComponentsToJsonForTesting(string filePath)
    {
        var selectedComponents = GetSelectedComponents();
        if (selectedComponents.Count == 0)
            throw new InvalidOperationException("No components selected for export.");

        if (selectedComponents.Count == 1)
        {
            await _viewModel.FileService.ExportToJsonAsync(selectedComponents[0], filePath);
            return;
        }

        await _viewModel.FileService.ExportToJsonAsync(selectedComponents, filePath);
    }

    internal void ExportSelectedComponentsToJsonSynchronouslyForTesting(string filePath)
        => ExportSelectedComponentsToJsonForTesting(filePath).GetAwaiter().GetResult();
    
    private async void ExportBomCsv_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Export BOM CSV requested");
        if (!_viewModel.Components.Any())
        {
            ActionLogService.Instance.Log(LogCategory.FileOperation, "Export BOM aborted", "No components");
            MessageBox.Show("No components to export.", "No Components", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var dialog = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            Title = "Export Bill of Materials"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _viewModel.BomExport.ExportToCsvAsync(_viewModel.Components, dialog.FileName);
                ActionLogService.Instance.Log(LogCategory.FileOperation, "BOM exported",
                    $"File: {dialog.FileName}, Components: {_viewModel.Components.Count}");
                MessageBox.Show("BOM exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ActionLogService.Instance.LogError(LogCategory.FileOperation, "Failed to export BOM", ex);
                MessageBox.Show($"Error exporting BOM: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void RunRevitIntrospection_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Revit introspection requested");

        if (!_revitIntrospectionOptions.IsEnabled)
        {
            var disabledMessage = $"Revit introspection is disabled. Set {RevitIntrospectionOptions.EnableEnvVar}=true to enable.";
            ActionLogService.Instance.Log(LogCategory.FileOperation, "Revit introspection blocked", disabledMessage);
            MessageBox.Show(disabledMessage, "Revit Introspection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var result = _revitIntrospectionService.RunScan(_revitIntrospectionOptions.InstallPathOverride);
            Mouse.OverrideCursor = null;

            if (!result.Success && result.RequiresInstallPathSelection)
            {
                var promptMessage = "Unable to locate a Revit install path automatically.\n\n" +
                                    "Do you want to browse to a Revit binary (for example RevitDB.dll)?";
                if (MessageBox.Show(promptMessage, "Revit Introspection", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    var selectedPath = PromptForRevitInstallPath();
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        Mouse.OverrideCursor = Cursors.Wait;
                        result = _revitIntrospectionService.RunScan(selectedPath);
                        Mouse.OverrideCursor = null;
                    }
                }
            }

            if (!result.Success)
            {
                ActionLogService.Instance.Log(LogCategory.FileOperation, "Revit introspection failed", result.UserMessage);
                MessageBox.Show(result.UserMessage, "Revit Introspection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var output = result.Output!;
            var scannedPath = result.Report?.ScannedPath ?? "(unknown)";
            ActionLogService.Instance.Log(LogCategory.FileOperation, "Revit introspection completed",
                $"ScannedPath: {scannedPath}, JSON: {output.JsonReportPath}, Summary: {output.SummaryReportPath}");

            MessageBox.Show(
                "Revit introspection completed.\n\n" +
                $"Scanned path:\n{scannedPath}\n\n" +
                $"JSON report:\n{output.JsonReportPath}\n\n" +
                $"Summary report:\n{output.SummaryReportPath}",
                "Revit Introspection",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.FileOperation, "Revit introspection crashed", ex);
            MessageBox.Show($"Revit introspection failed: {ex.Message}", "Revit Introspection", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private static string? PromptForRevitInstallPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a Revit managed binary",
            Filter = "RevitDB.dll|RevitDB.dll|DLL Files (*.dll)|*.dll|All Files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
            return null;

        return System.IO.Path.GetDirectoryName(dialog.FileName);
    }
    
    // ── XFDF Export / Import ──────────────────────────────────────────────

    private void ExportXfdf_Click(object sender, RoutedEventArgs e)
    {
        var reviewMarkups = _viewModel.GetFilteredReviewMarkups();
        if (reviewMarkups.Count == 0)
        {
            MessageBox.Show("No markups to export in the current review scope/filter.", "Export XFDF",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var scopeLabel = _viewModel.MarkupTool.ReviewScope == ViewModels.MarkupReviewScope.AllSheets
            ? "all-sheets-markups"
            : $"{_viewModel.SelectedSheet?.Number?.ToLowerInvariant() ?? "sheet"}-markups";

        var dlg = new SaveFileDialog
        {
            Filter = "XFDF Files (*.xfdf)|*.xfdf",
            FileName = $"{scopeLabel}.xfdf",
            Title = "Export XFDF Markups"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _viewModel.XfdfService.ExportToFile(reviewMarkups, dlg.FileName);
            ActionLogService.Instance.Log(LogCategory.FileOperation, "XFDF exported",
                $"File: {dlg.FileName}, Scope: {_viewModel.MarkupTool.ReviewScope}, Markups: {reviewMarkups.Count}");
            MessageBox.Show($"Exported {reviewMarkups.Count} markup(s) to XFDF from {_viewModel.MarkupTool.ReviewScope}.",
                "Export XFDF", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.FileOperation, "XFDF export failed", ex);
            MessageBox.Show($"XFDF export failed:\n{ex.Message}", "Export XFDF",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportXfdf_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "XFDF Files (*.xfdf)|*.xfdf|All Files (*.*)|*.*",
            Title = "Import XFDF Markups"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var imported = _viewModel.XfdfService.ImportFromFile(dlg.FileName);
            foreach (var markup in imported)
                _viewModel.Markups.Add(markup);

            ActionLogService.Instance.Log(LogCategory.FileOperation, "XFDF imported",
                $"File: {dlg.FileName}, Imported: {imported.Count}");
            MessageBox.Show($"Imported {imported.Count} markup(s) from XFDF.",
                "Import XFDF", MessageBoxButton.OK, MessageBoxImage.Information);

            QueueSceneRefresh(update2D: true, update3D: false, updateProperties: false);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.FileOperation, "XFDF import failed", ex);
            MessageBox.Show($"XFDF import failed:\n{ex.Message}", "Import XFDF",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Plot to Image ────────────────────────────────────────────────────

    private void PlotToImage_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Components.Any())
        {
            MessageBox.Show("No components to plot.", "Plot to Image",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "PNG Files (*.png)|*.png",
            FileName = "plot.png",
            Title = "Plot to Image"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var layout = new Models.PlotLayout();
            var extents = _viewModel.PlotService.ComputeModelExtents(
                _viewModel.Components.ToList(), _viewModel.Layers.ToList());
            var bitmap = _viewModel.PlotService.RenderToBitmap(
                layout, null, _viewModel.Components.ToList(),
                _viewModel.Layers.ToList(), extents);
            _viewModel.PlotService.SaveToPng(bitmap, dlg.FileName);

            ActionLogService.Instance.Log(LogCategory.FileOperation, "Plot to image complete",
                $"File: {dlg.FileName}");
            MessageBox.Show("Plot exported successfully!", "Plot to Image",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.FileOperation, "Plot to image failed", ex);
            MessageBox.Show($"Plot to image failed:\n{ex.Message}", "Plot to Image",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.Application, "Exit requested via menu");
        Close();
    }

    private void ExportLayers_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "XML Files (*.xml)|*.xml",
            FileName = "layers.xml"
        };
        if (dlg.ShowDialog() != true) return;
        System.IO.File.WriteAllText(dlg.FileName, _viewModel.LayerManager.ExportXml());
    }

    private void ExportIfc_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "IFC Files (*.ifc)|*.ifc",
            FileName = "export.ifc"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var svc = new Services.Export.IfcExportService();
            svc.ExportToIfc(_viewModel.Components, dlg.FileName);
            System.Windows.MessageBox.Show("IFC export complete.", "Export IFC",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"IFC export failed:\n{ex.Message}", "Export IFC",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportSchedule_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = "schedule.xlsx"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var exp = new Services.Export.ScheduleExcelExporter();
            exp.ExportSchedule(_viewModel.Components, dlg.FileName);
            System.Windows.MessageBox.Show("Schedule export complete.", "Export Schedule",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Schedule export failed:\n{ex.Message}", "Export Schedule",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

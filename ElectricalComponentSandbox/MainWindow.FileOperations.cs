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
            _viewModel.Components.Clear();
            _viewModel.Layers.Clear();
            _viewModel.PdfUnderlay = null;
            _viewModel.UndoRedo.Clear();
            _currentFilePath = null;
            Title = "Electrical Component Sandbox";
            
            // Re-initialize default layer
            var defaultLayer = Layer.CreateDefault();
            _viewModel.Layers.Add(defaultLayer);
            _viewModel.ActiveLayer = defaultLayer;
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
                    _viewModel.LoadFromProject(project);
                    _currentFilePath = dialog.FileName;
                    Title = $"Electrical Component Sandbox - {System.IO.Path.GetFileName(dialog.FileName)}";
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
        if (_viewModel.SelectedComponent == null)
        {
            ActionLogService.Instance.Log(LogCategory.FileOperation, "Export JSON aborted", "No component selected");
            MessageBox.Show("Please select a component to export.", "No Component", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                await _viewModel.FileService.ExportToJsonAsync(_viewModel.SelectedComponent, dialog.FileName);
                ActionLogService.Instance.Log(LogCategory.FileOperation, "JSON exported", $"File: {dialog.FileName}");
                MessageBox.Show("Component exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ActionLogService.Instance.LogError(LogCategory.FileOperation, "Failed to export JSON", ex);
                MessageBox.Show($"Error exporting file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
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

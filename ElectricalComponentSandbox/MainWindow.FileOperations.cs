using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Export;
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
            _viewModel.ProjectName = "Untitled Project";
            _viewModel.UndoRedo.Clear();
            _currentFilePath = null;
            UpdateWindowTitle();
            
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
                    UpdateWindowTitle();
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
            UpdateWindowTitle();
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

    internal XfdfImportMergeResult ImportXfdfForTesting(string xfdf, XfdfImportMergeMode mode)
    {
        var mergeResult = _viewModel.XfdfService.ImportAndMerge(xfdf, _viewModel.Markups, mode);
        _viewModel.MarkupTool.RefreshReviewContext();
        return mergeResult;
    }

    internal XfdfImportMergeResult PreviewXfdfImportForTesting(string xfdf)
        => _viewModel.XfdfService.PreviewImportMerge(xfdf, _viewModel.Markups);

    internal static string BuildXfdfImportConflictSummaryForTesting(XfdfImportMergeResult preview)
        => BuildXfdfImportConflictSummary(preview);

    internal static string BuildXfdfImportResultSummaryForTesting(XfdfImportMergeResult result, XfdfImportMergeMode mode)
        => BuildXfdfImportResultSummary(result, mode);

    internal static string[] BuildXfdfImportDetailLinesForTesting(XfdfImportMergeResult result)
        => BuildXfdfImportDetailLines(result).ToArray();
    
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
            var preview = _viewModel.XfdfService.PreviewImportMergeFromFile(dlg.FileName, _viewModel.Markups);
            var mergeMode = preview.ConflictCount == 0
                ? XfdfImportMergeMode.PreferImported
                : ShowXfdfImportConflictReviewDialog(preview);

            if (!mergeMode.HasValue)
                return;

            var mergeResult = _viewModel.XfdfService.ImportAndMergeFromFile(
                dlg.FileName,
                _viewModel.Markups,
                mergeMode.Value);

            _viewModel.MarkupTool.RefreshReviewContext();

            ActionLogService.Instance.Log(LogCategory.FileOperation, "XFDF imported",
                $"File: {dlg.FileName}, Mode: {mergeMode.Value}, Imported: {mergeResult.ImportedCount}, Added: {mergeResult.AddedCount}, Merged: {mergeResult.UpdatedCount}, Duplicated: {mergeResult.DuplicatedCount}, Conflicts: {mergeResult.ConflictCount}");
            ShowXfdfImportResultDialog(mergeResult, mergeMode.Value);

            QueueSceneRefresh(update2D: true, update3D: false, updateProperties: true);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.FileOperation, "XFDF import failed", ex);
            MessageBox.Show($"XFDF import failed:\n{ex.Message}", "Import XFDF",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private XfdfImportMergeMode? ShowXfdfImportConflictReviewDialog(XfdfImportMergeResult preview)
    {
        var replaceRadio = new RadioButton
        {
            Content = "Replace conflicting markups with imported versions",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 6)
        };
        var keepExistingRadio = new RadioButton
        {
            Content = "Keep existing markups and only merge new replies/history",
            Margin = new Thickness(0, 0, 0, 6)
        };
        var duplicateRadio = new RadioButton
        {
            Content = "Import conflicting markups as duplicates with fresh IDs"
        };

        var dialog = new Window
        {
            Title = "Import XFDF Conflict Review",
            Width = 760,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.CanResize,
            WindowStyle = WindowStyle.ToolWindow
        };

        var root = new DockPanel { Margin = new Thickness(12) };

        var summary = new TextBlock
        {
            Text = BuildXfdfImportConflictSummary(preview),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(summary, Dock.Top);
        root.Children.Add(summary);

        var strategyPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        strategyPanel.Children.Add(new TextBlock
        {
            Text = "Choose how conflicting markups should be reconciled:",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        });
        strategyPanel.Children.Add(replaceRadio);
        strategyPanel.Children.Add(keepExistingRadio);
        strategyPanel.Children.Add(duplicateRadio);
        DockPanel.SetDock(strategyPanel, Dock.Top);
        root.Children.Add(strategyPanel);

        var conflictList = new ListBox
        {
            ItemsSource = preview.Conflicts.Select(conflict => conflict.Summary).ToList(),
            MinHeight = 220,
            Margin = new Thickness(0, 0, 0, 12)
        };
        root.Children.Add(conflictList);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90,
            IsCancel = true,
            Margin = new Thickness(0, 0, 8, 0)
        };
        var importButton = new Button
        {
            Content = "Import",
            Width = 90,
            IsDefault = true
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(importButton);
        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        root.Children.Add(buttonPanel);

        XfdfImportMergeMode? selectedMode = null;
        importButton.Click += (_, _) =>
        {
            selectedMode = replaceRadio.IsChecked == true
                ? XfdfImportMergeMode.PreferImported
                : keepExistingRadio.IsChecked == true
                    ? XfdfImportMergeMode.PreferExisting
                    : XfdfImportMergeMode.AddAsNew;

            dialog.DialogResult = true;
            dialog.Close();
        };

        dialog.Content = root;
        return dialog.ShowDialog() == true ? selectedMode : null;
    }

    private void ShowXfdfImportResultDialog(XfdfImportMergeResult result, XfdfImportMergeMode mode)
    {
        var dialog = new Window
        {
            Title = "XFDF Import Reconciliation",
            Width = 760,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.CanResize,
            WindowStyle = WindowStyle.ToolWindow
        };

        var root = new DockPanel { Margin = new Thickness(12) };

        var summary = new TextBlock
        {
            Text = BuildXfdfImportResultSummary(result, mode),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(summary, Dock.Top);
        root.Children.Add(summary);

        var detailLines = BuildXfdfImportDetailLines(result).ToList();
        if (detailLines.Count > 0)
        {
            var detailsHeader = new TextBlock
            {
                Text = "Reconciliation details",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            DockPanel.SetDock(detailsHeader, Dock.Top);
            root.Children.Add(detailsHeader);

            var detailsList = new ListBox
            {
                ItemsSource = detailLines,
                MinHeight = 220,
                Margin = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(detailsList);
        }

        var closeButton = new Button
        {
            Content = "Close",
            Width = 90,
            IsDefault = true,
            IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        closeButton.Click += (_, _) => dialog.Close();
        DockPanel.SetDock(closeButton, Dock.Bottom);
        root.Children.Add(closeButton);

        dialog.Content = root;
        dialog.ShowDialog();
    }

    private static string BuildXfdfImportConflictSummary(XfdfImportMergeResult preview)
    {
        var lines = new List<string>
        {
            $"The XFDF import contains {preview.ImportedCount} markup(s) and {preview.ConflictCount} conflicting markup(s).",
            $"Geometry conflicts: {preview.GeometryConflictCount}. Review-state conflicts: {preview.ReviewStateConflictCount}. Type conflicts: {preview.TypeConflictCount}.",
        };

        if (preview.ParticipantNames.Count > 0)
            lines.Add($"Imported reviewers: {string.Join(", ", preview.ParticipantNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))}.");

        lines.Add("Review the conflicting issues below and choose how they should be reconciled before the import is applied.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildXfdfImportResultSummary(XfdfImportMergeResult result, XfdfImportMergeMode mode)
    {
        var lines = new List<string>
        {
            $"Imported {result.ImportedCount} markup(s) using {mode}.",
            $"Added {result.AddedCount}, merged {result.UpdatedCount}, duplicated {result.DuplicatedCount}.",
        };

        if (result.RepliesAddedCount > 0 || result.StatusNotesAppliedCount > 0)
        {
            lines.Add($"Applied {result.RepliesAddedCount} new reply entries ({result.ManualRepliesAddedCount} manual, {result.AuditRepliesAddedCount} audit) and {result.StatusNotesAppliedCount} status note update(s).");
        }

        if (result.ConflictCount > 0)
        {
            lines.Add($"Reviewed {result.ConflictCount} conflict(s): geometry {result.GeometryConflictCount}, review state {result.ReviewStateConflictCount}, type {result.TypeConflictCount}.");
        }

        if (result.ParticipantNames.Count > 0)
            lines.Add($"Review participants: {string.Join(", ", result.ParticipantNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))}.");

        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<string> BuildXfdfImportDetailLines(XfdfImportMergeResult result)
    {
        if (result.AddedMarkupIds.Count > 0)
            yield return $"Added markups: {BuildIdPreview(result.AddedMarkupIds)}";

        if (result.UpdatedMarkupIds.Count > 0)
            yield return $"Merged markups: {BuildIdPreview(result.UpdatedMarkupIds)}";

        if (result.DuplicatedMarkupIds.Count > 0)
            yield return $"Duplicated markups: {BuildIdPreview(result.DuplicatedMarkupIds)}";

        foreach (var conflict in result.Conflicts.Take(6))
            yield return conflict.Summary;

        if (result.ConflictCount > 6)
            yield return $"...and {result.ConflictCount - 6} additional conflict summary item(s).";
    }

    private static string BuildIdPreview(IReadOnlyList<string> ids)
    {
        var visible = ids.Take(5).ToList();
        if (ids.Count <= visible.Count)
            return string.Join(", ", visible);

        return $"{string.Join(", ", visible)}, +{ids.Count - visible.Count} more";
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
            exp.ExportSchedule(_viewModel.Components, dlg.FileName, _viewModel.ProjectParameters);
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

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private readonly List<Models.PlotStyleTable> _plotStyleTables = new() { Models.PlotStyleTable.CreateMonochrome() };
    private bool _sectionCutActive;
    private double _sectionCutY;
    private VisualStyle3D _activeVisualStyle3D = VisualStyle3D.Realistic;
    private bool _showComponentLabels = false;

    private Models.PlotLayout GetOrCreateActivePlotLayout()
    {
        _viewModel.ActivePlotLayout ??= new Models.PlotLayout();
        return _viewModel.ActivePlotLayout;
    }

    private void VisualStyleRealistic_Click(object sender, RoutedEventArgs e)
    {
        SetVisualStyle3D(VisualStyle3D.Realistic);
        UpdateVisualStyleMenuChecks(VisualStyleRealisticMenuItem);
    }

    private void VisualStyleShaded_Click(object sender, RoutedEventArgs e)
    {
        SetVisualStyle3D(VisualStyle3D.Conceptual);
        UpdateVisualStyleMenuChecks(VisualStyleShadedMenuItem);
    }

    private void VisualStyleWireframe_Click(object sender, RoutedEventArgs e)
    {
        SetVisualStyle3D(VisualStyle3D.Wireframe);
        UpdateVisualStyleMenuChecks(VisualStyleWireframeMenuItem);
    }

    private void VisualStyleHiddenLine_Click(object sender, RoutedEventArgs e)
    {
        SetVisualStyle3D(VisualStyle3D.XRay);
        UpdateVisualStyleMenuChecks(VisualStyleHiddenLineMenuItem);
    }

    private void UpdateVisualStyleMenuChecks(MenuItem active)
    {
        VisualStyleRealisticMenuItem.IsChecked = ReferenceEquals(active, VisualStyleRealisticMenuItem);
        VisualStyleShadedMenuItem.IsChecked = ReferenceEquals(active, VisualStyleShadedMenuItem);
        VisualStyleWireframeMenuItem.IsChecked = ReferenceEquals(active, VisualStyleWireframeMenuItem);
        VisualStyleHiddenLineMenuItem.IsChecked = ReferenceEquals(active, VisualStyleHiddenLineMenuItem);
    }

    private void SectionCut_Changed(object sender, RoutedEventArgs e)
    {
        _sectionCutActive = SectionCutEnabled.IsChecked == true;
        _sectionCutY = SectionCutSlider.Value;
        SectionCutValueText.Text = $"Y={_sectionCutY:F2}";
        UpdateViewport();
    }

    private void SectionCutSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_sectionCutActive)
            return;

        _sectionCutY = e.NewValue;
        SectionCutValueText.Text = $"Y={_sectionCutY:F2}";
        UpdateViewport();
    }

    private void BillboardLabels_Click(object sender, RoutedEventArgs e)
    {
        _showComponentLabels = BillboardLabelsMenuItem.IsChecked;
        UpdateViewport();
    }

    private void SaveNamedView_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptInput("Save Named View", "Enter a name for this view:", "View " + (_viewModel.NamedViews.Count + 1));
        if (string.IsNullOrWhiteSpace(name))
            return;

        var layerVisibilityById = BuildLayerVisibilityLookup();

        var nv = new Models.NamedView
        {
            Name = name,
            PanX = PlanScrollViewer.HorizontalOffset,
            PanY = PlanScrollViewer.VerticalOffset,
            Zoom = PlanCanvasScale.ScaleX,
            VisibleLayerIds = _viewModel.Layers
                .Where(layer => IsLayerVisible(layerVisibilityById, layer.Id))
                .Select(layer => layer.Id)
                .ToList()
        };

        // Capture 3D camera state
        if (Viewport.Camera is System.Windows.Media.Media3D.ProjectionCamera cam3D)
        {
            nv.CameraPosition = cam3D.Position;
            nv.CameraLookDirection = cam3D.LookDirection;
            nv.CameraUpDirection = cam3D.UpDirection;
            if (cam3D is System.Windows.Media.Media3D.PerspectiveCamera persp)
                nv.CameraFieldOfView = persp.FieldOfView;
        }
        _viewModel.NamedViews.Add(nv);
        _viewModel.RefreshProjectBrowserItems();
        RebuildNamedViewMenuItems();
        SyncSheetBrowserSelection();
    }

    private void ManageNamedViews_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.NamedViews.Count == 0)
        {
            MessageBox.Show("No named views saved yet.", "Named Views", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var names = string.Join("\n", _viewModel.NamedViews.Select((view, index) => $"{index + 1}. {view.Name}"));
        var input = PromptInput("Manage Named Views", $"Enter the number of the view to delete:\n\n{names}", string.Empty);
        if (int.TryParse(input, out int indexToDelete) && indexToDelete >= 1 && indexToDelete <= _viewModel.NamedViews.Count)
        {
            _viewModel.NamedViews.RemoveAt(indexToDelete - 1);
            _viewModel.RefreshProjectBrowserItems();
            RebuildNamedViewMenuItems();
            SyncSheetBrowserSelection();
        }
    }

    private void RestoreNamedView(Models.NamedView view)
    {
        // Restore 2D viewport
        PlanCanvasScale.ScaleX = view.Zoom;
        PlanCanvasScale.ScaleY = view.Zoom;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            PlanScrollViewer.ScrollToHorizontalOffset(view.PanX);
            PlanScrollViewer.ScrollToVerticalOffset(view.PanY);
        });

        // Restore 3D camera
        if (view.Has3DCamera && Viewport.Camera is System.Windows.Media.Media3D.ProjectionCamera cam3D)
        {
            cam3D.Position = view.CameraPosition!.Value;
            cam3D.LookDirection = view.CameraLookDirection!.Value;
            cam3D.UpDirection = view.CameraUpDirection!.Value;
            if (view.CameraFieldOfView.HasValue && cam3D is System.Windows.Media.Media3D.PerspectiveCamera persp)
                persp.FieldOfView = view.CameraFieldOfView.Value;
        }

        if (view.VisibleLayerIds is { Count: > 0 })
        {
            foreach (var layer in _viewModel.Layers)
                layer.IsVisible = view.VisibleLayerIds.Contains(layer.Id);
        }

        UpdateStatusBar();
    }

    private void RebuildNamedViewMenuItems()
    {
        var namedViewMenu = NamedViewsListMenuItem.Parent as MenuItem;
        if (namedViewMenu is null)
            return;

        while (namedViewMenu.Items.Count > 3)
            namedViewMenu.Items.RemoveAt(3);

        if (_viewModel.NamedViews.Count == 0)
        {
            NamedViewsListMenuItem.Header = "(none saved)";
            NamedViewsListMenuItem.IsEnabled = false;
        }
        else
        {
            NamedViewsListMenuItem.Header = "(select below)";
            NamedViewsListMenuItem.IsEnabled = false;
            foreach (var namedView in _viewModel.NamedViews)
            {
                var menuItem = new MenuItem { Header = namedView.Name, Tag = namedView };
                menuItem.Click += (menuSender, _) =>
                {
                    if (menuSender is MenuItem currentItem && currentItem.Tag is Models.NamedView savedView)
                        RestoreNamedView(savedView);
                };
                namedViewMenu.Items.Add(menuItem);
            }
        }
    }

    private static string? PromptInput(string title, string prompt, string defaultValue)
    {
        var dlg = new Window
        {
            Title = title,
            Width = 380,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow
        };
        var stackPanel = new StackPanel { Margin = new Thickness(12) };
        stackPanel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var textBox = new TextBox { Text = defaultValue };
        stackPanel.Children.Add(textBox);
        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var okButton = new Button { Content = "OK", Width = 75, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancelButton = new Button { Content = "Cancel", Width = 75, IsCancel = true };
        okButton.Click += (_, _) =>
        {
            dlg.DialogResult = true;
            dlg.Close();
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        stackPanel.Children.Add(buttonPanel);
        dlg.Content = stackPanel;
        textBox.SelectAll();
        textBox.Focus();
        return dlg.ShowDialog() == true ? textBox.Text : null;
    }

    private void Show2DView_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.View, "Switched to 2D Plan View");
        ViewTabs.SelectedIndex = 1;
        Update2DCanvas();
        if (_isMobileView)
        {
            SetMobilePane(MobilePane.Canvas);
        }
    }

    private void Show3DView_Click(object sender, RoutedEventArgs e)
    {
        ActionLogService.Instance.Log(LogCategory.View, "Switched to 3D Viewport");
        ViewTabs.SelectedIndex = 0;
        if (_isMobileView)
        {
            SetMobilePane(MobilePane.Canvas);
            MobileSectionTitleText.Text = "Plan (3D)";
        }
    }

    private void PlotStyleManager_Click(object sender, RoutedEventArgs e)
    {
        var tables = _plotStyleTables;
        var names = tables.Count > 0
            ? string.Join("\n", tables.Select((table, index) => $"{index + 1}. {table.Name} — {table.Description}"))
            : "(none)";
        MessageBox.Show($"Plot Style Tables (CTB):\n\n{names}\n\n" +
            "Use 'Page Setup' to assign a CTB to the active layout.",
            "Plot Style Manager", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PageSetup_Click(object sender, RoutedEventArgs e)
    {
        var activePlotLayout = GetOrCreateActivePlotLayout();

        var paperNames = Enum.GetNames(typeof(Models.PaperSize));
        var currentPaper = activePlotLayout.PaperSize.ToString();
        var input = PromptInput(
            "Page Setup",
            $"Current paper: {currentPaper}, Scale: {activePlotLayout.PlotScale}\n" +
            $"Available: {string.Join(", ", paperNames)}\n\nEnter paper name (or Cancel):",
            currentPaper);
        if (input is null)
            return;

        if (Enum.TryParse<Models.PaperSize>(input, true, out var paperSize))
        {
            activePlotLayout.PaperSize = paperSize;
            ActionLogService.Instance.Log(LogCategory.View, "Page setup changed", $"Paper: {paperSize}");
        }
    }

    private void PrintPreview_Click(object sender, RoutedEventArgs e)
    {
        var activePlotLayout = GetOrCreateActivePlotLayout();

        if (!_viewModel.Components.Any())
        {
            MessageBox.Show("No components to preview.", "Print Preview",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var ctb = _plotStyleTables.FirstOrDefault(t =>
                string.Equals(t.Name, activePlotLayout.PlotStyleTableName, StringComparison.OrdinalIgnoreCase));
            var extents = _viewModel.PlotService.ComputeModelExtents(
                _viewModel.Components.ToList(), _viewModel.Layers.ToList());
            var bitmap = _viewModel.PlotService.RenderToBitmap(
                activePlotLayout, ctb, _viewModel.Components.ToList(),
                _viewModel.Layers.ToList(), extents);

            var (paperW, paperH) = activePlotLayout.GetPaperInches();

            // Show preview in a resizable window
            var previewImage = new System.Windows.Controls.Image
            {
                Source = bitmap,
                Stretch = System.Windows.Media.Stretch.Uniform
            };

            var infoText = new TextBlock
            {
                  Text = $"Paper: {activePlotLayout.PaperSize} ({paperW:F1}\" × {paperH:F1}\")  |  " +
                      $"Scale: {activePlotLayout.PlotScale}  |  CTB: {activePlotLayout.PlotStyleTableName}  |  " +
                       $"Components: {_viewModel.Components.Count}  |  DPI: {_viewModel.PlotService.OutputDpi}",
                Margin = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Foreground = System.Windows.Media.Brushes.Gray
            };

            var printButton = new Button
            {
                Content = "Print...",
                Width = 100,
                Height = 28,
                Margin = new Thickness(0, 4, 8, 8),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var exportButton = new Button
            {
                Content = "Export PNG...",
                Width = 100,
                Height = 28,
                Margin = new Thickness(0, 4, 4, 8),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttonPanel.Children.Add(exportButton);
            buttonPanel.Children.Add(printButton);

            var dock = new DockPanel();
            DockPanel.SetDock(infoText, Dock.Top);
            DockPanel.SetDock(buttonPanel, Dock.Bottom);
            dock.Children.Add(infoText);
            dock.Children.Add(buttonPanel);
            dock.Children.Add(previewImage);

            var previewWindow = new Window
            {
                Title = "Print Preview",
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Content = dock
            };

            var capturedBitmap = bitmap;
            exportButton.Click += (_, _) =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG Files (*.png)|*.png",
                    FileName = "print_preview.png"
                };
                if (dlg.ShowDialog() == true)
                {
                    _viewModel.PlotService.SaveToPng(capturedBitmap, dlg.FileName);
                    MessageBox.Show("Exported.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            printButton.Click += (_, _) =>
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var visual = new System.Windows.Media.DrawingVisual();
                    using (var dc = visual.RenderOpen())
                    {
                        dc.DrawImage(capturedBitmap, new Rect(0, 0,
                            printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));
                    }
                    printDialog.PrintVisual(visual, "Electrical Drawing");
                    MessageBox.Show("Print job sent.", "Print", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            previewWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.FileOperation, "Print preview failed", ex);
            MessageBox.Show($"Print preview failed:\n{ex.Message}", "Print Preview",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    internal string[] GetSavedPageSetupNamesForTesting()
        => _viewModel.SavedPageSetups.Select(pageSetup => pageSetup.Name).ToArray();

    internal bool SaveCurrentPageSetupForTesting(string name)
        => _viewModel.SaveCurrentPageSetup(name);

    internal bool ApplySavedPageSetupForTesting(string name)
        => ApplySavedPageSetup(name);

    internal bool DeleteSavedPageSetupForTesting(string name)
        => _viewModel.DeleteSavedPageSetup(name);

    internal static string BuildPlotLayoutSummaryForTesting(Models.PlotLayout layout)
        => BuildPlotLayoutSummary(layout);

    internal static string BuildPrintPreviewInfoTextForTesting(Models.PlotLayout layout, Rect modelExtents, int componentCount, int outputDpi)
        => BuildPrintPreviewInfoText(layout, modelExtents, componentCount, outputDpi);

    internal static FrameworkElement BuildPrintPreviewWorkspaceForTesting(ImageSource previewSource, Models.PlotLayout layout, Rect modelExtents)
        => BuildPrintPreviewWorkspace(previewSource, layout, modelExtents);

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
            if (_viewModel.SelectedSheet != null)
                _viewModel.SelectedSheet.PlotLayout = activePlotLayout;

            UpdateStatusBar();
            ActionLogService.Instance.Log(LogCategory.View, "Page setup changed", $"Paper: {paperSize}");
        }
    }

    private void ManagePageSetups_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "Page Setups",
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
            Text = $"Current layout: {BuildPlotLayoutSummary(GetOrCreateActivePlotLayout())}",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };
        DockPanel.SetDock(summary, Dock.Top);
        root.Children.Add(summary);

        var list = new ListBox { MinHeight = 260, Margin = new Thickness(0, 0, 0, 12) };
        root.Children.Add(list);

        void RefreshList(int selectedIndex = -1)
        {
            list.ItemsSource = _viewModel.SavedPageSetups.Select(BuildPlotLayoutSummary).ToList();
            if (list.Items.Count == 0)
                return;

            list.SelectedIndex = selectedIndex >= 0
                ? Math.Min(selectedIndex, list.Items.Count - 1)
                : 0;
        }

        RefreshList();

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var saveCurrentButton = new Button { Content = "Save Current...", Width = 110, Margin = new Thickness(0, 0, 8, 0) };
        var applyButton = new Button { Content = "Apply", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        var deleteButton = new Button { Content = "Delete", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        var closeButton = new Button { Content = "Close", Width = 90, IsCancel = true, IsDefault = true };

        saveCurrentButton.Click += (_, _) =>
        {
            var defaultName = _viewModel.SavedPageSetups.Count == 0
                ? "Page Setup 1"
                : $"Page Setup {_viewModel.SavedPageSetups.Count + 1}";
            var name = PromptInput("Save Page Setup", "Enter a name for the current page setup:", defaultName);
            if (string.IsNullOrWhiteSpace(name))
                return;

            if (_viewModel.SaveCurrentPageSetup(name))
            {
                summary.Text = $"Current layout: {BuildPlotLayoutSummary(GetOrCreateActivePlotLayout())}";
                RefreshList(_viewModel.SavedPageSetups.Count - 1);
            }
        };

        applyButton.Click += (_, _) =>
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= _viewModel.SavedPageSetups.Count)
            {
                MessageBox.Show("Select a saved page setup to apply.", "Page Setups", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedName = _viewModel.SavedPageSetups[list.SelectedIndex].Name;
            if (ApplySavedPageSetup(selectedName))
            {
                summary.Text = $"Current layout: {BuildPlotLayoutSummary(GetOrCreateActivePlotLayout())}";
                UpdateStatusBar();
            }
        };

        deleteButton.Click += (_, _) =>
        {
            if (list.SelectedIndex < 0 || list.SelectedIndex >= _viewModel.SavedPageSetups.Count)
            {
                MessageBox.Show("Select a saved page setup to delete.", "Page Setups", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedIndex = list.SelectedIndex;
            var selectedName = _viewModel.SavedPageSetups[selectedIndex].Name;
            if (_viewModel.DeleteSavedPageSetup(selectedName))
                RefreshList(Math.Max(0, selectedIndex - 1));
        };

        closeButton.Click += (_, _) => dialog.Close();

        buttonPanel.Children.Add(saveCurrentButton);
        buttonPanel.Children.Add(applyButton);
        buttonPanel.Children.Add(deleteButton);
        buttonPanel.Children.Add(closeButton);
        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        root.Children.Add(buttonPanel);

        dialog.Content = root;
        dialog.ShowDialog();
    }

    private bool ApplySavedPageSetup(string name)
    {
        if (!_viewModel.ApplySavedPageSetup(name))
            return false;

        UpdateStatusBar();
        return true;
    }

    private static string BuildPlotLayoutSummary(Models.PlotLayout layout)
        => layout.GetSummaryText();

    private static string BuildPrintPreviewInfoText(Models.PlotLayout layout, Rect modelExtents, int componentCount, int outputDpi)
    {
        var (paperWidth, paperHeight) = layout.GetPaperInches();
        return $"Page setup: {layout.Name}  |  Paper space: {layout.PaperSize} ({paperWidth:F1}\" × {paperHeight:F1}\")  |  " +
               $"Scale: {layout.PlotScale:g}  |  CTB: {layout.PlotStyleTableName}  |  " +
               $"Model extents: {BuildPlotExtentsSummary(modelExtents)}  |  Components: {componentCount}  |  DPI: {outputDpi}";
    }

    private static string BuildPlotExtentsSummary(Rect modelExtents)
    {
        if (modelExtents.IsEmpty)
            return "n/a";

        return $"{modelExtents.Width:F2} × {modelExtents.Height:F2}";
    }

    private static FrameworkElement BuildPrintPreviewWorkspace(ImageSource previewSource, Models.PlotLayout layout, Rect modelExtents)
    {
        var previewWidth = GetPreviewSurfaceDimension(previewSource, useWidth: true);
        var previewHeight = GetPreviewSurfaceDimension(previewSource, useWidth: false);

        var previewImage = new Image
        {
            Source = previewSource,
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true
        };

        var printableArea = new Border
        {
            Margin = new Thickness(28),
            BorderBrush = new SolidColorBrush(Color.FromRgb(214, 219, 225)),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Child = previewImage
        };

        var paperSurface = new Grid
        {
            Width = previewWidth,
            Height = previewHeight
        };
        paperSurface.Children.Add(new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(90, 96, 106)),
            BorderThickness = new Thickness(2),
            Padding = new Thickness(10),
            Child = printableArea
        });
        paperSurface.Children.Add(new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(18),
            Padding = new Thickness(10, 4, 10, 4),
            Background = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = "PAPER SPACE",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            }
        });
        paperSurface.Children.Add(new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(18),
            Padding = new Thickness(10, 4, 10, 4),
            Background = new SolidColorBrush(Color.FromArgb(228, 255, 255, 255)),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = $"{layout.Name}  |  {layout.PaperSize}  |  Scale {layout.PlotScale:g}",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55))
            }
        });
        paperSurface.Children.Add(new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(18),
            Padding = new Thickness(10, 4, 10, 4),
            Background = new SolidColorBrush(Color.FromArgb(228, 255, 255, 255)),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = $"Model: {BuildPlotExtentsSummary(modelExtents)}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99))
            }
        });

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
            Padding = new Thickness(24),
            Child = new Viewbox
            {
                Stretch = Stretch.Uniform,
                Child = paperSurface
            }
        };
    }

    private static double GetPreviewSurfaceDimension(ImageSource previewSource, bool useWidth)
    {
        if (previewSource is BitmapSource bitmapSource)
        {
            var pixelDimension = useWidth ? bitmapSource.PixelWidth : bitmapSource.PixelHeight;
            if (pixelDimension > 0)
                return pixelDimension;
        }

        var dimension = useWidth ? previewSource.Width : previewSource.Height;
        if (!double.IsNaN(dimension) && dimension > 0)
            return dimension;

        return useWidth ? 1200 : 900;
    }

    private Models.PlotStyleTable? GetPlotStyleTable(Models.PlotLayout layout)
    {
        return _plotStyleTables.FirstOrDefault(table =>
            string.Equals(table.Name, layout.PlotStyleTableName, StringComparison.OrdinalIgnoreCase));
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
            var ctb = GetPlotStyleTable(activePlotLayout);
            var extents = _viewModel.PlotService.ComputeModelExtents(
                _viewModel.Components.ToList(), _viewModel.Layers.ToList());
            var bitmap = _viewModel.PlotService.RenderToBitmap(
                activePlotLayout, ctb, _viewModel.Components.ToList(),
                _viewModel.Layers.ToList(), extents);
            var previewWorkspace = BuildPrintPreviewWorkspace(bitmap, activePlotLayout, extents);

            var infoText = new TextBlock
            {
                  Text = BuildPrintPreviewInfoText(activePlotLayout, extents, _viewModel.Components.Count, _viewModel.PlotService.OutputDpi),
                Margin = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap
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
            dock.Children.Add(previewWorkspace);

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

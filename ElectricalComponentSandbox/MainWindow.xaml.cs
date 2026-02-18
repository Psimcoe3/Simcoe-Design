using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Microsoft.Win32;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.ViewModels;
using ElectricalComponentSandbox.Services;
using HelixToolkit.Wpf;

namespace ElectricalComponentSandbox;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private string? _currentFilePath;
    
    // Constants for smooth conduit rendering
    private const int MaxSegmentResolution = 50;
    private const int MinSegmentResolution = 5;
    private const double ResolutionScaleFactor = 10.0;
    
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }
    
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // TODO: Implement keyboard shortcuts
    }
    
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedComponent))
        {
            UpdatePropertiesPanel();
            UpdateViewport();
            Update2DCanvas();
        }
        else if (e.PropertyName == nameof(MainViewModel.Components))
        {
            UpdateViewport();
            Update2DCanvas();
        }
    }
    
    private void UpdatePropertiesPanel()
    {
        var component = _viewModel.SelectedComponent;
        if (component == null)
        {
            ClearPropertiesPanel();
            return;
        }
        
        NameTextBox.Text = component.Name;
        TypeTextBox.Text = component.Type.ToString();
        
        PositionXTextBox.Text = component.Position.X.ToString("F2");
        PositionYTextBox.Text = component.Position.Y.ToString("F2");
        PositionZTextBox.Text = component.Position.Z.ToString("F2");
        
        RotationXTextBox.Text = component.Rotation.X.ToString("F2");
        RotationYTextBox.Text = component.Rotation.Y.ToString("F2");
        RotationZTextBox.Text = component.Rotation.Z.ToString("F2");
        
        WidthTextBox.Text = component.Parameters.Width.ToString("F2");
        HeightTextBox.Text = component.Parameters.Height.ToString("F2");
        DepthTextBox.Text = component.Parameters.Depth.ToString("F2");
        MaterialTextBox.Text = component.Parameters.Material;
        ElevationTextBox.Text = component.Parameters.Elevation.ToString("F2");
        ColorTextBox.Text = component.Parameters.Color;
        
        // Set layer combo
        var layer = _viewModel.Layers.FirstOrDefault(l => l.Id == component.LayerId);
        if (layer != null)
            LayerComboBox.SelectedItem = layer;
        
        // Update conduit-specific properties
        if (component is ConduitComponent conduit)
        {
            ConduitProperties.Visibility = Visibility.Visible;
            BendPointsTextBlock.Text = conduit.BendPoints.Count.ToString();
        }
        else
        {
            ConduitProperties.Visibility = Visibility.Collapsed;
        }
    }
    
    private void ClearPropertiesPanel()
    {
        NameTextBox.Text = string.Empty;
        TypeTextBox.Text = string.Empty;
        PositionXTextBox.Text = string.Empty;
        PositionYTextBox.Text = string.Empty;
        PositionZTextBox.Text = string.Empty;
        RotationXTextBox.Text = string.Empty;
        RotationYTextBox.Text = string.Empty;
        RotationZTextBox.Text = string.Empty;
        WidthTextBox.Text = string.Empty;
        HeightTextBox.Text = string.Empty;
        DepthTextBox.Text = string.Empty;
        MaterialTextBox.Text = string.Empty;
        ElevationTextBox.Text = string.Empty;
        ColorTextBox.Text = string.Empty;
    }
    
    private void UpdateViewport()
    {
        // Clear existing models and mapping
        var itemsToRemove = Viewport.Children.OfType<ModelVisual3D>()
            .Where(m => m.Content is GeometryModel3D).ToList();
        
        foreach (var item in itemsToRemove)
        {
            Viewport.Children.Remove(item);
        }
        
        // Add components to viewport
        foreach (var component in _viewModel.Components)
        {
            // Check layer visibility
            var layer = _viewModel.Layers.FirstOrDefault(l => l.Id == component.LayerId);
            if (layer != null && !layer.IsVisible) continue;
            
            AddComponentToViewport(component);
        }
    }
    
    private void AddComponentToViewport(ElectricalComponent component)
    {
        var visual = new ModelVisual3D();
        var geometry = CreateComponentGeometry(component);
        
        var color = (Color)ColorConverter.ConvertFromString(component.Parameters.Color);
        var material = new DiffuseMaterial(new SolidColorBrush(color));
        
        Material appliedMaterial;
        if (component == _viewModel.SelectedComponent)
        {
            var materialGroup = new MaterialGroup();
            materialGroup.Children.Add(material);
            materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(80, 255, 165, 0))));
            appliedMaterial = materialGroup;
        }
        else
        {
            appliedMaterial = material;
        }
        
        var model = new GeometryModel3D(geometry, appliedMaterial);
        
        // Apply transformations
        var transformGroup = new Transform3DGroup();
        transformGroup.Children.Add(new TranslateTransform3D(component.Position.X, component.Position.Y, component.Position.Z));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), component.Rotation.X)));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), component.Rotation.Y)));
        transformGroup.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), component.Rotation.Z)));
        transformGroup.Children.Add(new ScaleTransform3D(component.Scale.X, component.Scale.Y, component.Scale.Z));
        
        model.Transform = transformGroup;
        visual.Content = model;
        
        Viewport.Children.Add(visual);
    }
    
    private MeshGeometry3D CreateComponentGeometry(ElectricalComponent component)
    {
        var builder = new MeshBuilder();
        
        switch (component.Type)
        {
            case ComponentType.Conduit:
                if (component is ConduitComponent conduit)
                {
                    CreateConduitGeometry(builder, conduit);
                }
                break;
                
            case ComponentType.Hanger:
                if (component is HangerComponent hanger)
                {
                    // Hanger rendered as a vertical rod
                    builder.AddCylinder(
                        new Point3D(0, 0, 0),
                        new Point3D(0, hanger.RodLength, 0),
                        hanger.RodDiameter, 12);
                }
                break;
                
            case ComponentType.CableTray:
                if (component is CableTrayComponent tray)
                {
                    // Cable tray rendered as an open-top box (U-channel)
                    var pathPts = tray.GetPathPoints();
                    if (pathPts.Count >= 2)
                    {
                        for (int i = 0; i < pathPts.Count - 1; i++)
                        {
                            var p1 = pathPts[i];
                            var p2 = pathPts[i + 1];
                            var dir = p2 - p1;
                            var segLen = dir.Length;
                            builder.AddBox(
                                new Point3D((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, (p1.Z + p2.Z) / 2),
                                tray.TrayWidth, tray.TrayDepth, segLen);
                        }
                    }
                    else
                    {
                        builder.AddBox(new Point3D(0, 0, 0), tray.TrayWidth, tray.TrayDepth, tray.Length);
                    }
                }
                break;
                
            case ComponentType.Box:
            case ComponentType.Panel:
            case ComponentType.Support:
                builder.AddBox(new Point3D(0, 0, 0), 
                    component.Parameters.Width, 
                    component.Parameters.Height, 
                    component.Parameters.Depth);
                break;
        }
        
        return builder.ToMesh();
    }
    
    private void CreateConduitGeometry(MeshBuilder builder, ConduitComponent conduit)
    {
        var pathPoints = conduit.GetPathPoints();
        
        if (pathPoints.Count == 2)
        {
            builder.AddCylinder(pathPoints[0], pathPoints[1], conduit.Diameter, 20);
            return;
        }
        
        var smoothPoints = GenerateSmoothPath(pathPoints, conduit.BendRadius);
        
        if (smoothPoints.Count >= 2)
        {
            for (int i = 0; i < smoothPoints.Count - 1; i++)
            {
                builder.AddCylinder(smoothPoints[i], smoothPoints[i + 1], conduit.Diameter, 20);
            }
        }
    }
    
    private List<Point3D> GenerateSmoothPath(List<Point3D> controlPoints, double bendRadius)
    {
        var smoothPoints = new List<Point3D>();
        
        if (controlPoints.Count < 2)
            return smoothPoints;
        
        int segmentResolution = Math.Min(MaxSegmentResolution, 
            Math.Max(MinSegmentResolution, (int)(bendRadius * ResolutionScaleFactor)));
        
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            Point3D p0 = (i == 0) ? controlPoints[i] : controlPoints[i - 1];
            Point3D p1 = controlPoints[i];
            Point3D p2 = controlPoints[i + 1];
            Point3D p3 = (i + 2 < controlPoints.Count) ? controlPoints[i + 2] : controlPoints[i + 1];
            
            for (int j = 0; j < segmentResolution; j++)
            {
                double t = (double)j / segmentResolution;
                Point3D interpolated = CatmullRomInterpolate(p0, p1, p2, p3, t);
                smoothPoints.Add(interpolated);
            }
        }
        
        smoothPoints.Add(controlPoints[controlPoints.Count - 1]);
        
        return smoothPoints;
    }
    
    private Point3D CatmullRomInterpolate(Point3D p0, Point3D p1, Point3D p2, Point3D p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;
        
        double c0 = -0.5 * t3 + t2 - 0.5 * t;
        double c1 = 1.5 * t3 - 2.5 * t2 + 1.0;
        double c2 = -1.5 * t3 + 2.0 * t2 + 0.5 * t;
        double c3 = 0.5 * t3 - 0.5 * t2;
        
        double x = c0 * p0.X + c1 * p1.X + c2 * p2.X + c3 * p3.X;
        double y = c0 * p0.Y + c1 * p1.Y + c2 * p2.Y + c3 * p3.Y;
        double z = c0 * p0.Z + c1 * p1.Z + c2 * p2.Z + c3 * p3.Z;
        
        return new Point3D(x, y, z);
    }
    
    // ===== 2D Plan Canvas =====
    
    private void Update2DCanvas()
    {
        PlanCanvas.Children.Clear();
        
        // Draw components
        foreach (var component in _viewModel.Components)
        {
            var layer = _viewModel.Layers.FirstOrDefault(l => l.Id == component.LayerId);
            if (layer != null && !layer.IsVisible) continue;
            
            Draw2DComponent(component);
        }
    }
    
    private void Draw2DComponent(ElectricalComponent component)
    {
        var color = (Color)ColorConverter.ConvertFromString(component.Parameters.Color);
        var brush = new SolidColorBrush(color);
        var isSelected = component == _viewModel.SelectedComponent;
        
        FrameworkElement element;
        
        double canvasX = 1000 + component.Position.X * 20; // Center + scale
        double canvasY = 1000 - component.Position.Z * 20; // Flip Y for plan view
        
        switch (component.Type)
        {
            case ComponentType.Conduit:
                if (component is ConduitComponent conduit)
                {
                    var pathPts = conduit.GetPathPoints();
                    if (pathPts.Count >= 2)
                    {
                        var polyline = new Polyline
                        {
                            Stroke = brush,
                            StrokeThickness = Math.Max(2, conduit.Diameter * 10),
                            StrokeLineJoin = PenLineJoin.Round
                        };
                        foreach (var pt in pathPts)
                        {
                            polyline.Points.Add(new Point(
                                canvasX + pt.X * 20,
                                canvasY - pt.Z * 20));
                        }
                        if (isSelected)
                        {
                            polyline.Stroke = Brushes.Orange;
                            polyline.StrokeThickness += 2;
                        }
                        PlanCanvas.Children.Add(polyline);
                        return;
                    }
                }
                element = CreateRectElement(component.Parameters.Width * 20, component.Parameters.Depth * 20, brush, isSelected);
                break;
                
            case ComponentType.CableTray:
                element = CreateRectElement(component.Parameters.Width * 20, component.Parameters.Depth * 20, brush, isSelected);
                break;
                
            case ComponentType.Hanger:
                var ellipse = new Ellipse
                {
                    Width = 10, Height = 10,
                    Fill = brush,
                    Stroke = isSelected ? Brushes.Orange : Brushes.Black,
                    StrokeThickness = isSelected ? 3 : 1
                };
                Canvas.SetLeft(ellipse, canvasX - 5);
                Canvas.SetTop(ellipse, canvasY - 5);
                PlanCanvas.Children.Add(ellipse);
                return;
                
            default:
                element = CreateRectElement(component.Parameters.Width * 20, component.Parameters.Height * 20, brush, isSelected);
                break;
        }
        
        Canvas.SetLeft(element, canvasX - ((element as Rectangle)?.Width ?? 20) / 2);
        Canvas.SetTop(element, canvasY - ((element as Rectangle)?.Height ?? 20) / 2);
        PlanCanvas.Children.Add(element);
    }
    
    private Rectangle CreateRectElement(double width, double height, Brush fill, bool isSelected)
    {
        return new Rectangle
        {
            Width = Math.Max(5, width),
            Height = Math.Max(5, height),
            Fill = fill,
            Stroke = isSelected ? Brushes.Orange : Brushes.Black,
            StrokeThickness = isSelected ? 3 : 1
        };
    }
    
    // ===== Component Add Handlers =====
    
    private void AddConduit_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddComponent(ComponentType.Conduit);
    }
    
    private void AddBox_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddComponent(ComponentType.Box);
    }
    
    private void AddPanel_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddComponent(ComponentType.Panel);
    }
    
    private void AddSupport_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.AddComponent(ComponentType.Support);
    }
    
    private void AddCableTray_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement cable tray component
    }
    
    private void AddHanger_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement hanger component
    }
    
    private void DeleteComponent_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelectedComponent();
    }
    
    private void LibraryItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LibraryListBox.SelectedItem is ElectricalComponent component)
        {
            _viewModel.AddComponent(component.Type);
        }
    }
    
    private void ApplyProperties_Click(object sender, RoutedEventArgs e)
    {
        var component = _viewModel.SelectedComponent;
        if (component == null) return;
        ActionLogService.Instance.Log(LogCategory.Property, "Applying property changes",
            $"Component: {component.Name}, Type: {component.Type}");
        
        try
        {
            component.Name = NameTextBox.Text;
            
            component.Position = new Point3D(
                double.Parse(PositionXTextBox.Text),
                double.Parse(PositionYTextBox.Text),
                double.Parse(PositionZTextBox.Text));
            
            component.Rotation = new Vector3D(
                double.Parse(RotationXTextBox.Text),
                double.Parse(RotationYTextBox.Text),
                double.Parse(RotationZTextBox.Text));
            
            component.Parameters.Width = double.Parse(WidthTextBox.Text);
            component.Parameters.Height = double.Parse(HeightTextBox.Text);
            component.Parameters.Depth = double.Parse(DepthTextBox.Text);
            component.Parameters.Material = MaterialTextBox.Text;
            component.Parameters.Elevation = double.Parse(ElevationTextBox.Text);
            component.Parameters.Color = ColorTextBox.Text;
            
            // Update layer assignment
            if (LayerComboBox.SelectedItem is Layer layer)
            {
                component.LayerId = layer.Id;
            }
            
            UpdateViewport();
            Update2DCanvas();
            ActionLogService.Instance.Log(LogCategory.Property, "Properties applied",
                $"Name: {component.Name}, Pos: ({component.Position.X:F2}, {component.Position.Y:F2}, {component.Position.Z:F2}), " +
                $"Material: {component.Parameters.Material}, Color: {component.Parameters.Color}");
            MessageBox.Show("Properties updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ActionLogService.Instance.LogError(LogCategory.Property, "Failed to apply properties", ex);
            MessageBox.Show($"Error updating properties: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Create a new project? Any unsaved changes will be lost.", "New Project", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _viewModel.Components.Clear();
            _currentFilePath = null;
            Title = "Electrical Component Sandbox";
        }
    }
    
    private async void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = Services.ComponentFileService.GetFileFilter(),
            Title = "Open Project File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var component = await _viewModel.FileService.LoadComponentAsync(dialog.FileName);
                if (component != null)
                {
                    _viewModel.Components.Clear();
                    _viewModel.Components.Add(component);
                    _currentFilePath = dialog.FileName;
                    Title = $"Electrical Component Sandbox - {System.IO.Path.GetFileName(dialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private async void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveProjectAs_Click(sender, e);
            return;
        }
        
        await SaveFileAsync(_currentFilePath);
    }
    
    private async void SaveProjectAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = Services.ComponentFileService.GetFileFilter(),
            Title = "Save Project File"
        };
        
        if (dialog.ShowDialog() == true)
        {
            await SaveFileAsync(dialog.FileName);
            _currentFilePath = dialog.FileName;
            Title = $"Electrical Component Sandbox - {System.IO.Path.GetFileName(dialog.FileName)}";
        }
    }
    
    private async Task SaveFileAsync(string filePath)
    {
        try
        {
            var component = _viewModel.Components.FirstOrDefault();
            if (component == null) throw new InvalidOperationException("No component to save");
            
            await _viewModel.FileService.SaveComponentAsync(component, filePath);
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
    
    private void ExportBomCsv_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement BOM CSV export
    }
    
    private void ImportPdf_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement PDF import
    }
    
    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement undo
    }
    
    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement redo
    }
    
    private void DeleteLastBendPoint_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement delete last bend point
    }
    
    private void Show2DView_Click(object sender, RoutedEventArgs e)
    {
        ViewTabs.SelectedIndex = 1;
    }
    
    private void Show3DView_Click(object sender, RoutedEventArgs e)
    {
        ViewTabs.SelectedIndex = 0;
    }
    
    private void DrawConduit_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement conduit drawing mode
    }
    
    private void ToggleEditConduitPath_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement conduit path editing
    }
    
    private void UnitSystem_Changed(object sender, SelectionChangedEventArgs e)
    {
        // TODO: Implement unit system change
    }
    
    private void LayerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // TODO: Implement layer selection change
    }
    
    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement add layer
    }
    
    private void RemoveLayer_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement remove layer
    }
    
    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // TODO: Implement viewport click handling
    }
    
    private void PdfOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // TODO: Implement PDF opacity change
    }
    
    private void PdfLock_Changed(object sender, RoutedEventArgs e)
    {
        // TODO: Implement PDF lock toggle
    }
    
    private void CalibrateScale_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement scale calibration
    }
    
    private void PlanCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // TODO: Implement 2D canvas click
    }
    
    private void PlanCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        // TODO: Implement 2D canvas mouse move
    }
    
    private void PlanCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // TODO: Implement 2D canvas mouse up
    }
    
    private void PlanCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // TODO: Implement 2D canvas zoom
    }
    
    private void ClearBendPoints_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement clear bend points
    }
    
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.ViewModels;
using HelixToolkit.Wpf;

namespace ElectricalComponentSandbox;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private string? _currentFilePath;
    private readonly Dictionary<ModelVisual3D, ElectricalComponent> _visualToComponentMap = new();
    private bool _isEditingConduitPath = false;
    private readonly List<ModelVisual3D> _bendPointHandles = new();
    private ModelVisual3D? _draggedHandle = null;
    private Point _lastMousePosition;
    
    // Constants for bend point visualization
    private const double BendPointHandleRadius = 0.3;
    private const double ElbowRadiusRatio = 0.6; // Ratio of elbow sphere to conduit diameter
    private static readonly Color EditModeButtonColor = Color.FromRgb(255, 200, 100);
    private static readonly Color BendPointHandleColor = Colors.Orange;
    
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }
    
    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedComponent))
        {
            UpdatePropertiesPanel();
            UpdateViewport();
        }
        else if (e.PropertyName == nameof(MainViewModel.Components))
        {
            UpdateViewport();
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
        
        _visualToComponentMap.Clear();
        
        // Add components to viewport
        foreach (var component in _viewModel.Components)
        {
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
            // Highlight selected component with an emissive glow
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
        _visualToComponentMap[visual] = component;
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
        
        // Create cylinders for each segment
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            var start = pathPoints[i];
            var end = pathPoints[i + 1];
            
            // Add cylinder segment
            builder.AddCylinder(start, end, conduit.Diameter, 20);
            
            // Add elbow/bend at connection points (except at the last point)
            if (i < pathPoints.Count - 2)
            {
                // Add a small sphere at bend points for visual continuity
                builder.AddSphere(end, conduit.Diameter * ElbowRadiusRatio, 12, 12);
            }
        }
    }
    
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
    
    private void DeleteComponent_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.DeleteSelectedComponent();
    }
    
    private void LibraryItem_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (LibraryListBox.SelectedItem is ElectricalComponent component)
        {
            _viewModel.AddComponent(component.Type);
        }
    }
    
    private void Viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(Viewport);
        var hits = Viewport3DHelper.FindHits(Viewport.Viewport, position);
        
        if (_isEditingConduitPath && _viewModel.SelectedComponent is ConduitComponent conduit)
        {
            // Check if clicking on a bend point handle
            var handleHit = hits?
                .Select(hit => hit.Visual)
                .OfType<ModelVisual3D>()
                .FirstOrDefault(v => _bendPointHandles.Contains(v));
            
            if (handleHit != null)
            {
                // Start dragging the handle
                _draggedHandle = handleHit;
                _lastMousePosition = position;
                Viewport.MouseMove += Viewport_MouseMove;
                Viewport.MouseLeftButtonUp += Viewport_MouseLeftButtonUp;
                e.Handled = true;
                return;
            }
            
            // Otherwise, add a new bend point at the clicked location
            var rayHit = hits?.FirstOrDefault();
            if (rayHit != null)
            {
                var hitPoint = rayHit.Position;
                // Convert to local coordinates
                var offset = hitPoint - _viewModel.SelectedComponent.Position;
                var localPoint = new Point3D(offset.X, offset.Y, offset.Z);
                
                conduit.BendPoints.Add(localPoint);
                ShowBendPointHandles();
                UpdateViewport();
                e.Handled = true;
                return;
            }
        }
        
        var matchedComponent = hits?
            .Select(hit => hit.Visual)
            .OfType<ModelVisual3D>()
            .Where(visual => _visualToComponentMap.ContainsKey(visual))
            .Select(visual => _visualToComponentMap[visual])
            .FirstOrDefault();
        
        _viewModel.SelectedComponent = matchedComponent;
        
        // Update handles if in edit mode
        if (_isEditingConduitPath && matchedComponent is ConduitComponent)
        {
            ShowBendPointHandles();
        }
    }
    
    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedHandle == null || _viewModel.SelectedComponent is not ConduitComponent conduit)
            return;
        
        var position = e.GetPosition(Viewport);
        
        // Find which bend point this handle represents
        int handleIndex = _bendPointHandles.IndexOf(_draggedHandle);
        if (handleIndex >= 0)
        {
            // For bend points within the list
            if (handleIndex < conduit.BendPoints.Count)
            {
                var currentPoint = conduit.BendPoints[handleIndex];
                
                // Use ray casting to get a new 3D position
                var hits = Viewport3DHelper.FindHits(Viewport.Viewport, position);
                if (hits != null && hits.Any())
                {
                    var hitPoint = hits.First().Position;
                    var offset = hitPoint - _viewModel.SelectedComponent.Position;
                    var newPoint = new Point3D(offset.X, offset.Y, offset.Z);
                    
                    // Apply snap to grid if enabled
                    if (_viewModel.SnapToGrid)
                    {
                        newPoint.X = Math.Round(newPoint.X / _viewModel.GridSize) * _viewModel.GridSize;
                        newPoint.Y = Math.Round(newPoint.Y / _viewModel.GridSize) * _viewModel.GridSize;
                        newPoint.Z = Math.Round(newPoint.Z / _viewModel.GridSize) * _viewModel.GridSize;
                    }
                    
                    conduit.BendPoints[handleIndex] = newPoint;
                    UpdateViewport();
                    ShowBendPointHandles();
                }
            }
        }
        
        _lastMousePosition = position;
    }
    
    private void Viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _draggedHandle = null;
        Viewport.MouseMove -= Viewport_MouseMove;
        Viewport.MouseLeftButtonUp -= Viewport_MouseLeftButtonUp;
    }
    
    private void ClearBendPoints_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent is ConduitComponent conduit)
        {
            if (MessageBox.Show("Clear all bend points from this conduit?", "Confirm", 
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                conduit.BendPoints.Clear();
                UpdateViewport();
                UpdatePropertiesPanel();
                
                if (_isEditingConduitPath)
                {
                    ShowBendPointHandles();
                }
            }
        }
    }
    
    private void DeleteLastBendPoint_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent is ConduitComponent conduit && conduit.BendPoints.Count > 0)
        {
            conduit.BendPoints.RemoveAt(conduit.BendPoints.Count - 1);
            UpdateViewport();
            UpdatePropertiesPanel();
            
            if (_isEditingConduitPath)
            {
                ShowBendPointHandles();
            }
        }
        else
        {
            MessageBox.Show("No bend points to delete.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private void ApplyProperties_Click(object sender, RoutedEventArgs e)
    {
        var component = _viewModel.SelectedComponent;
        if (component == null) return;
        
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
            
            UpdateViewport();
            MessageBox.Show("Properties updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error updating properties: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void NewFile_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Create a new file? Any unsaved changes will be lost.", "New File", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _viewModel.Components.Clear();
            _currentFilePath = null;
            Title = "Electrical Component Sandbox";
        }
    }
    
    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = Services.ComponentFileService.GetFileFilter(),
            Title = "Open Component File"
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
    
    private async void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveFileAs_Click(sender, e);
            return;
        }
        
        await SaveFileAsync(_currentFilePath);
    }
    
    private async void SaveFileAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = Services.ComponentFileService.GetFileFilter(),
            Title = "Save Component File"
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
        if (_viewModel.SelectedComponent == null)
        {
            MessageBox.Show("Please select a component to save.", "No Component", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        try
        {
            await _viewModel.FileService.SaveComponentAsync(_viewModel.SelectedComponent, filePath);
            MessageBox.Show("File saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedComponent == null)
        {
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
                MessageBox.Show("Component exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        
        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key == Key.S)
            {
                SaveFileAs_Click(sender, e);
                e.Handled = true;
            }
        }
        else if (modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N:
                    NewFile_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.O:
                    OpenFile_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.S:
                    SaveFile_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.E:
                    ExportJson_Click(sender, e);
                    e.Handled = true;
                    break;
            }
        }
        else if (modifiers == ModifierKeys.None)
        {
            if (e.Key == Key.Delete)
            {
                DeleteComponent_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _isEditingConduitPath)
            {
                ToggleEditConduitPath_Click(sender, e);
                e.Handled = true;
            }
        }
    }
    
    private void ToggleEditConduitPath_Click(object sender, RoutedEventArgs e)
    {
        _isEditingConduitPath = !_isEditingConduitPath;
        
        // Update button appearance
        if (_isEditingConduitPath)
        {
            EditConduitPathButton.Background = new SolidColorBrush(EditModeButtonColor);
            EditConduitPathButton.Content = "Exit Edit Mode";
            
            if (_viewModel.SelectedComponent is ConduitComponent)
            {
                ShowBendPointHandles();
                MessageBox.Show("Edit Mode Active:\n" +
                    "• Click on conduit to add bend points\n" +
                    "• Drag orange handles to move bend points\n" +
                    "• Use 'Clear All Bend Points' to reset\n" +
                    "• Click 'Exit Edit Mode' when done", 
                    "Edit Conduit Path", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _isEditingConduitPath = false;
                EditConduitPathButton.Background = System.Windows.SystemColors.ControlBrush;
                EditConduitPathButton.Content = "Edit Conduit Path";
                MessageBox.Show("Please select a conduit component first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
            EditConduitPathButton.Background = System.Windows.SystemColors.ControlBrush;
            EditConduitPathButton.Content = "Edit Conduit Path";
            HideBendPointHandles();
        }
    }
    
    private void ShowBendPointHandles()
    {
        HideBendPointHandles();
        
        if (_viewModel.SelectedComponent is not ConduitComponent conduit)
            return;
        
        var pathPoints = conduit.GetPathPoints();
        
        // Skip first point (origin), create handles for bend points and end point
        for (int i = 1; i < pathPoints.Count; i++)
        {
            var point = pathPoints[i];
            var handle = CreateBendPointHandle(point);
            _bendPointHandles.Add(handle);
            Viewport.Children.Add(handle);
        }
    }
    
    private void HideBendPointHandles()
    {
        foreach (var handle in _bendPointHandles)
        {
            Viewport.Children.Remove(handle);
        }
        _bendPointHandles.Clear();
    }
    
    private ModelVisual3D CreateBendPointHandle(Point3D position)
    {
        var visual = new ModelVisual3D();
        var builder = new MeshBuilder();
        builder.AddSphere(new Point3D(0, 0, 0), BendPointHandleRadius, 12, 12);
        
        var material = new DiffuseMaterial(new SolidColorBrush(BendPointHandleColor));
        var emissive = new EmissiveMaterial(new SolidColorBrush(Color.FromArgb(100, 255, 165, 0)));
        var materialGroup = new MaterialGroup();
        materialGroup.Children.Add(material);
        materialGroup.Children.Add(emissive);
        
        var model = new GeometryModel3D(builder.ToMesh(), materialGroup);
        
        // Apply position from selected component's transform
        if (_viewModel.SelectedComponent != null)
        {
            var transformGroup = new Transform3DGroup();
            var globalPos = _viewModel.SelectedComponent.Position + new Vector3D(position.X, position.Y, position.Z);
            transformGroup.Children.Add(new TranslateTransform3D(globalPos.X, globalPos.Y, globalPos.Z));
            model.Transform = transformGroup;
        }
        
        visual.Content = model;
        return visual;
    }
}
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
                    builder.AddCylinder(new Point3D(0, 0, 0), new Point3D(0, 0, conduit.Length), 
                        conduit.Diameter, 20);
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
        
        var matchedComponent = hits?
            .Select(hit => hit.Visual)
            .OfType<ModelVisual3D>()
            .Where(visual => _visualToComponentMap.ContainsKey(visual))
            .Select(visual => _visualToComponentMap[visual])
            .FirstOrDefault();
        
        _viewModel.SelectedComponent = matchedComponent;
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
}
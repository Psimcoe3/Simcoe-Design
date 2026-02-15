using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private ElectricalComponent? _selectedComponent;
    private bool _showGrid = true;
    private bool _snapToGrid = true;
    private double _gridSize = 1.0;
    
    public ObservableCollection<ElectricalComponent> Components { get; } = new();
    public ObservableCollection<ElectricalComponent> LibraryComponents { get; } = new();
    
    public ComponentFileService FileService { get; } = new();
    
    public ElectricalComponent? SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            _selectedComponent = value;
            OnPropertyChanged();
        }
    }
    
    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            _showGrid = value;
            OnPropertyChanged();
        }
    }
    
    public bool SnapToGrid
    {
        get => _snapToGrid;
        set
        {
            _snapToGrid = value;
            OnPropertyChanged();
        }
    }
    
    public double GridSize
    {
        get => _gridSize;
        set
        {
            _gridSize = value;
            OnPropertyChanged();
        }
    }
    
    public MainViewModel()
    {
        InitializeLibrary();
    }
    
    private void InitializeLibrary()
    {
        LibraryComponents.Add(new ConduitComponent());
        LibraryComponents.Add(new BoxComponent());
        LibraryComponents.Add(new PanelComponent());
        LibraryComponents.Add(new SupportComponent());
    }
    
    public void AddComponent(ComponentType type)
    {
        ElectricalComponent component = type switch
        {
            ComponentType.Conduit => new ConduitComponent(),
            ComponentType.Box => new BoxComponent(),
            ComponentType.Panel => new PanelComponent(),
            ComponentType.Support => new SupportComponent(),
            _ => throw new ArgumentException("Invalid component type")
        };
        
        Components.Add(component);
        SelectedComponent = component;
    }
    
    public void DeleteSelectedComponent()
    {
        if (SelectedComponent != null)
        {
            Components.Remove(SelectedComponent);
            SelectedComponent = null;
        }
    }
    
    public void MoveComponent(Vector3D delta)
    {
        if (SelectedComponent == null) return;
        
        var newPosition = SelectedComponent.Position + delta;
        
        if (SnapToGrid)
        {
            newPosition.X = Math.Round(newPosition.X / GridSize) * GridSize;
            newPosition.Y = Math.Round(newPosition.Y / GridSize) * GridSize;
            newPosition.Z = Math.Round(newPosition.Z / GridSize) * GridSize;
        }
        
        SelectedComponent.Position = newPosition;
        OnPropertyChanged(nameof(SelectedComponent));
    }
    
    public void RotateComponent(Vector3D rotation)
    {
        if (SelectedComponent == null) return;
        
        SelectedComponent.Rotation += rotation;
        OnPropertyChanged(nameof(SelectedComponent));
    }
    
    public void ScaleComponent(Vector3D scale)
    {
        if (SelectedComponent == null) return;
        
        SelectedComponent.Scale = new Vector3D(
            SelectedComponent.Scale.X * scale.X,
            SelectedComponent.Scale.Y * scale.Y,
            SelectedComponent.Scale.Z * scale.Z
        );
        OnPropertyChanged(nameof(SelectedComponent));
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

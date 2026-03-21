using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Dimensioning;

namespace ElectricalComponentSandbox.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private ElectricalComponent? _selectedComponent;
    private bool _showGrid = true;
    private bool _snapToGrid = true;
    private double _gridSize = 1.0;
    private Layer? _activeLayer;
    private string _unitSystem = "Imperial";
    private PdfUnderlay? _pdfUnderlay;
    private bool _isOrthoActive = false;
    private bool _isPolarActive = false;
    private double _polarIncrementDeg = 45.0;

    public ObservableCollection<ElectricalComponent> Components { get; } = new();
    public ObservableCollection<ElectricalComponent> LibraryComponents { get; } = new();
    public ObservableCollection<Layer> Layers { get; } = new();

    // ── Markup / 2D annotation ────────────────────────────────────────────────

    /// <summary>All markup annotations for the active drawing sheet</summary>
    public ObservableCollection<MarkupRecord> Markups { get; } = new();

    /// <summary>IDs of all currently selected components (multi-select)</summary>
    public HashSet<string> SelectedComponentIds { get; } = new();

    // ── Services ──────────────────────────────────────────────────────────────

    public ComponentFileService FileService { get; } = new();
    public ProjectFileService ProjectFileService { get; } = new();
    public UndoRedoService UndoRedo { get; } = new();
    public UnitConversionService UnitConverter { get; } = new();
    public BomExportService BomExport { get; } = new();
    public SnapService SnapService { get; } = new();
    public PdfCalibrationService CalibrationService { get; } = new();

    /// <summary>Dispatch-based markup renderer; used by the canvas paint loop</summary>
    public MarkupRenderService MarkupRenderer { get; } = new();

    /// <summary>Shadow hit-test tree for SkiaSharp canvas geometry</summary>
    public ShadowGeometryTree ShadowTree { get; } = new();

    /// <summary>Factory for 2D dimension markup records</summary>
    public Dimension2DService DimensionService { get; } = new();
    
    public ElectricalComponent? SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            _selectedComponent = value;
            ActionLogService.Instance.Log(LogCategory.Selection, "Component selected",
                value != null ? $"Name: {value.Name}, Type: {value.Type}, Id: {value.Id}" : "Deselected");
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
            _gridSize = value > 0 ? value : 0.1;
            OnPropertyChanged();
        }
    }
    
    public Layer? ActiveLayer
    {
        get => _activeLayer;
        set
        {
            _activeLayer = value;
            OnPropertyChanged();
        }
    }
    
    public string UnitSystemName
    {
        get => _unitSystem;
        set
        {
            _unitSystem = value;
            UnitConverter.CurrentSystem = value == "Metric" ? UnitSystem.Metric : UnitSystem.Imperial;
            OnPropertyChanged();
        }
    }
    
    public PdfUnderlay? PdfUnderlay
    {
        get => _pdfUnderlay;
        set
        {
            _pdfUnderlay = value;
            OnPropertyChanged();
        }
    }

    // ── Drawing mode props ────────────────────────────────────────────────────

    /// <summary>
    /// Ortho mode (F8): constrains cursor movement to horizontal or vertical.
    /// </summary>
    public bool IsOrthoActive
    {
        get => _isOrthoActive;
        set
        {
            _isOrthoActive = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Polar tracking mode (F10): snaps movement to multiples of <see cref="PolarIncrementDeg"/>.
    /// Mutually exclusive with IsOrthoActive — enabling one disables the other.
    /// </summary>
    public bool IsPolarActive
    {
        get => _isPolarActive;
        set
        {
            _isPolarActive = value;
            if (value) _isOrthoActive = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOrthoActive));
        }
    }

    /// <summary>Polar tracking increment in degrees (default 45°)</summary>
    public double PolarIncrementDeg
    {
        get => _polarIncrementDeg;
        set
        {
            _polarIncrementDeg = value > 0 ? value : 45.0;
            OnPropertyChanged();
        }
    }

    // ── Markup helpers ────────────────────────────────────────────────────────

    /// <summary>Adds a markup and syncs the shadow tree</summary>
    public void AddMarkup(MarkupRecord markup)
    {
        markup.UpdateBoundingRect();
        Markups.Add(markup);
        ShadowTree.AddOrUpdate(markup);
    }

    /// <summary>Removes a markup by id and syncs the shadow tree</summary>
    public bool RemoveMarkup(string markupId)
    {
        var rec = Markups.FirstOrDefault(m => m.Id == markupId);
        if (rec is null) return false;
        Markups.Remove(rec);
        ShadowTree.Remove(markupId);
        return true;
    }

    /// <summary>Replaces (updates) an existing markup record and resyncs shadows</summary>
    public void UpdateMarkup(MarkupRecord markup)
    {
        var idx = Markups.IndexOf(Markups.FirstOrDefault(m => m.Id == markup.Id)!);
        if (idx >= 0)
        {
            markup.UpdateBoundingRect();
            Markups[idx] = markup;
        }
        ShadowTree.AddOrUpdate(markup);
    }

    public MainViewModel()
    {
        InitializeLibrary();
        InitializeLayers();
    }
    
    private void InitializeLibrary()
    {
        LibraryComponents.Clear();
        foreach (var template in ElectricalComponentCatalog.CreateLibraryTemplates())
        {
            LibraryComponents.Add(template);
        }
    }
    
    private void InitializeLayers()
    {
        var defaultLayer = Layer.CreateDefault();
        Layers.Add(defaultLayer);
        ActiveLayer = defaultLayer;
    }
    
    public void AddComponent(ComponentType type)
    {
        ActionLogService.Instance.Log(LogCategory.Component, "Adding component", $"Type: {type}");
        var component = ElectricalComponentCatalog.CreateDefaultComponent(type);
        AddComponentInstance(component);
    }

    public void AddComponentFromTemplate(ElectricalComponent template)
    {
        var component = ElectricalComponentCatalog.CloneTemplate(template);
        AddComponentInstance(component);
    }

    private void AddComponentInstance(ElectricalComponent component)
    {
        if (ActiveLayer != null)
            component.LayerId = ActiveLayer.Id;

        var action = new AddComponentAction(Components, component);
        UndoRedo.Execute(action);
        SelectedComponent = component;
        ActionLogService.Instance.Log(LogCategory.Component, "Component added",
            $"Name: {component.Name}, Id: {component.Id}, Layer: {component.LayerId}, Total: {Components.Count}");
    }
    
    public void DeleteSelectedComponent()
    {
        if (SelectedComponent != null)
        {
            ActionLogService.Instance.Log(LogCategory.Component, "Deleting component",
                $"Name: {SelectedComponent.Name}, Type: {SelectedComponent.Type}, Id: {SelectedComponent.Id}");
            var action = new RemoveComponentAction(Components, SelectedComponent);
            UndoRedo.Execute(action);
            SelectedComponent = null;
        }
    }
    
    public void MoveComponent(Vector3D delta)
    {
        if (SelectedComponent == null) return;
        ActionLogService.Instance.Log(LogCategory.Transform, "Moving component",
            $"Name: {SelectedComponent.Name}, Delta: ({delta.X:F2}, {delta.Y:F2}, {delta.Z:F2})");
        
        var oldPosition = SelectedComponent.Position;
        var newPosition = SelectedComponent.Position + delta;
        
        if (SnapToGrid)
        {
            newPosition.X = Math.Round(newPosition.X / GridSize) * GridSize;
            newPosition.Y = Math.Round(newPosition.Y / GridSize) * GridSize;
            newPosition.Z = Math.Round(newPosition.Z / GridSize) * GridSize;
        }
        
        var action = new MoveComponentAction(SelectedComponent, oldPosition, newPosition);
        UndoRedo.Execute(action);
        OnPropertyChanged(nameof(SelectedComponent));
    }
    
    public void RotateComponent(Vector3D rotation)
    {
        if (SelectedComponent == null) return;
        ActionLogService.Instance.Log(LogCategory.Transform, "Rotating component",
            $"Name: {SelectedComponent.Name}, Rotation: ({rotation.X:F2}, {rotation.Y:F2}, {rotation.Z:F2})");
        
        SelectedComponent.Rotation += rotation;
        OnPropertyChanged(nameof(SelectedComponent));
    }
    
    public void ScaleComponent(Vector3D scale)
    {
        if (SelectedComponent == null) return;
        ActionLogService.Instance.Log(LogCategory.Transform, "Scaling component",
            $"Name: {SelectedComponent.Name}, Scale: ({scale.X:F2}, {scale.Y:F2}, {scale.Z:F2})");
        
        SelectedComponent.Scale = new Vector3D(
            SelectedComponent.Scale.X * scale.X,
            SelectedComponent.Scale.Y * scale.Y,
            SelectedComponent.Scale.Z * scale.Z
        );
        OnPropertyChanged(nameof(SelectedComponent));
    }
    
    public void Undo()
    {
        ActionLogService.Instance.Log(LogCategory.Edit, "Undo", $"CanUndo: {UndoRedo.CanUndo}");
        UndoRedo.Undo();
        OnPropertyChanged(nameof(SelectedComponent));
    }
    
    public void Redo()
    {
        ActionLogService.Instance.Log(LogCategory.Edit, "Redo", $"CanRedo: {UndoRedo.CanRedo}");
        UndoRedo.Redo();
        OnPropertyChanged(nameof(SelectedComponent));
    }
    
    /// <summary>
    /// Adds a new layer to the project
    /// </summary>
    public Layer AddLayer(string name)
    {
        var layer = new Layer { Name = name };
        Layers.Add(layer);
        ActionLogService.Instance.Log(LogCategory.Layer, "Layer added",
            $"Name: {name}, Id: {layer.Id}, Total: {Layers.Count}");
        return layer;
    }
    
    /// <summary>
    /// Removes a layer (moves components to default layer)
    /// </summary>
    public void RemoveLayer(Layer layer)
    {
        if (layer.Id == "default")
        {
            ActionLogService.Instance.Log(LogCategory.Layer, "Remove layer blocked", "Cannot remove default layer");
            return;
        }
        ActionLogService.Instance.Log(LogCategory.Layer, "Removing layer",
            $"Name: {layer.Name}, Id: {layer.Id}");
        
        foreach (var comp in Components.Where(c => c.LayerId == layer.Id))
        {
            comp.LayerId = "default";
        }
        Layers.Remove(layer);
        
        if (ActiveLayer == layer)
        {
            ActiveLayer = Layers.FirstOrDefault(l => l.Id == "default");
        }
    }
    
    /// <summary>
    /// Creates a ProjectModel from the current state for saving
    /// </summary>
    public ProjectModel ToProjectModel()
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Creating project model",
            $"Components: {Components.Count}, Layers: {Layers.Count}, Markups: {Markups.Count}");
        return new ProjectModel
        {
            Components = Components.ToList(),
            Layers = Layers.ToList(),
            Markups = Markups.ToList(),
            PdfUnderlay = PdfUnderlay,
            UnitSystem = UnitSystemName,
            GridSize = GridSize,
            ShowGrid = ShowGrid,
            SnapToGrid = SnapToGrid
        };
    }

    /// <summary>
    /// Loads state from a ProjectModel
    /// </summary>
    public void LoadFromProject(ProjectModel project)
    {
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Loading project",
            $"Name: {project.Name}, Components: {project.Components.Count}, Layers: {project.Layers.Count}, Units: {project.UnitSystem}");
        Components.Clear();
        foreach (var comp in project.Components)
            Components.Add(comp);

        Layers.Clear();
        foreach (var layer in project.Layers)
            Layers.Add(layer);

        if (Layers.Count == 0)
            InitializeLayers();

        Markups.Clear();
        ShadowTree.Clear();
        foreach (var markup in project.Markups)
        {
            Markups.Add(markup);
            ShadowTree.AddOrUpdate(markup);
        }

        ActiveLayer = Layers.FirstOrDefault(l => l.Id == "default") ?? Layers.FirstOrDefault();
        PdfUnderlay = project.PdfUnderlay;
        UnitSystemName = project.UnitSystem;
        GridSize = project.GridSize;
        ShowGrid = project.ShowGrid;
        SnapToGrid = project.SnapToGrid;

        UndoRedo.Clear();
        SelectedComponent = null;
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

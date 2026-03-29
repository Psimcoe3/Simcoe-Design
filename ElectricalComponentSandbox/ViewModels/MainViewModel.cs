using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Dimensioning;
using ElectricalComponentSandbox.Services.Export;

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
    private DimensionStyleDefinition? _activeDimensionStyle;

    public ObservableCollection<ElectricalComponent> Components { get; } = new();
    public ObservableCollection<ElectricalComponent> LibraryComponents { get; } = new();
    public ObservableCollection<Layer> Layers { get; } = new();

    // ── Markup / 2D annotation ────────────────────────────────────────────────

    /// <summary>All markup annotations for the active drawing sheet</summary>
    public ObservableCollection<MarkupRecord> Markups { get; } = new();

    /// <summary>Saved named views (camera bookmarks)</summary>
    public ObservableCollection<NamedView> NamedViews { get; } = new();

    /// <summary>IDs of all currently selected components (multi-select)</summary>
    public HashSet<string> SelectedComponentIds { get; } = new();

    // ── Services ──────────────────────────────────────────────────────────────

    public ComponentFileService FileService { get; }
    public ProjectFileService ProjectFileService { get; }
    public UndoRedoService UndoRedo { get; }
    public UnitConversionService UnitConverter { get; }
    public BomExportService BomExport { get; }
    public SnapService SnapService { get; }
    public PdfCalibrationService CalibrationService { get; }

    /// <summary>Dispatch-based markup renderer; used by the canvas paint loop</summary>
    public MarkupRenderService MarkupRenderer { get; }

    /// <summary>Drives the markup annotation toolbar and punch-list filtering</summary>
    public MarkupToolViewModel MarkupTool { get; }

    /// <summary>Drives the layer manager DataGrid</summary>
    public LayerManagerViewModel LayerManager { get; }

    /// <summary>Shadow hit-test tree for SkiaSharp canvas geometry</summary>
    public ShadowGeometryTree ShadowTree { get; }

    /// <summary>Factory for 2D dimension markup records</summary>
    public Dimension2DService DimensionService { get; }

    /// <summary>NEC-based voltage drop, wire sizing, and load calculations</summary>
    public ElectricalCalculationService ElectricalCalc { get; }

    /// <summary>Renders drawing to bitmap for plot/print</summary>
    public PlotToPdfService PlotService { get; }

    /// <summary>XFDF markup exchange for Bluebeam/Acrobat interop</summary>
    public XfdfExportService XfdfService { get; }

    /// <summary>Selection filtering and bulk property editing</summary>
    public SelectionFilterService SelectionFilter { get; }

    /// <summary>All circuits in the active project</summary>
    public ObservableCollection<Circuit> Circuits { get; } = new();

    /// <summary>Panel schedules built from circuits + panels</summary>
    public ObservableCollection<PanelSchedule> PanelSchedules { get; } = new();

    /// <summary>Dimension style definitions (DIMSTYLE table)</summary>
    public ObservableCollection<DimensionStyleDefinition> DimensionStyles { get; } = new();

    /// <summary>Text style definitions (STYLE table)</summary>
    public ObservableCollection<TextStyle> TextStyles { get; } = new();

    /// <summary>Active dimension style applied to new dimensions</summary>
    public DimensionStyleDefinition? ActiveDimensionStyle
    {
        get => _activeDimensionStyle;
        set
        {
            _activeDimensionStyle = value;
            if (value != null)
                DimensionService.ApplyStyle(value);
            OnPropertyChanged();
        }
    }
    
    public ElectricalComponent? SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            _selectedComponent = value;
            if (value != null && SelectedComponentIds.Count == 0)
                SelectedComponentIds.Add(value.Id);
            ActionLogService.Instance.Log(LogCategory.Selection, "Component selected",
                value != null ? $"Name: {value.Name}, Type: {value.Type}, Id: {value.Id}" : "Deselected");
            OnPropertyChanged();
        }
    }

    public bool IsComponentSelected(ElectricalComponent? component)
        => component != null && SelectedComponentIds.Contains(component.Id);

    public void ClearComponentSelection()
    {
        SelectedComponentIds.Clear();
        OnPropertyChanged(nameof(SelectedComponentIds));
        SelectedComponent = null;
    }

    public void SelectSingleComponent(ElectricalComponent? component)
    {
        SelectedComponentIds.Clear();
        if (component != null)
            SelectedComponentIds.Add(component.Id);

        OnPropertyChanged(nameof(SelectedComponentIds));
        SelectedComponent = component;
    }

    public void SetSelectedComponents(IEnumerable<ElectricalComponent> components, ElectricalComponent? primaryComponent = null)
    {
        var selection = components
            .Where(component => component != null)
            .Distinct()
            .ToList();

        SelectedComponentIds.Clear();
        foreach (var component in selection)
            SelectedComponentIds.Add(component.Id);

        var nextPrimary = primaryComponent != null && SelectedComponentIds.Contains(primaryComponent.Id)
            ? primaryComponent
            : selection.FirstOrDefault();

        OnPropertyChanged(nameof(SelectedComponentIds));
        SelectedComponent = nextPrimary;
    }

    public bool ToggleComponentSelection(ElectricalComponent component)
    {
        if (SelectedComponentIds.Remove(component.Id))
        {
            OnPropertyChanged(nameof(SelectedComponentIds));
            if (ReferenceEquals(SelectedComponent, component) || SelectedComponent?.Id == component.Id)
                SelectedComponent = Components.FirstOrDefault(candidate => SelectedComponentIds.Contains(candidate.Id));
            return false;
        }

        SelectedComponentIds.Add(component.Id);
        OnPropertyChanged(nameof(SelectedComponentIds));
        SelectedComponent = component;
        return true;
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
        : this(
            new ComponentFileService(),
            new ProjectFileService(),
            new UndoRedoService(),
            new UnitConversionService(),
            new BomExportService(),
            new SnapService(),
            new PdfCalibrationService(),
            new MarkupRenderService(),
            new Dimension2DService(),
            new ShadowGeometryTree(),
            new ElectricalCalculationService(),
            new PlotToPdfService(),
            new XfdfExportService(),
            new SelectionFilterService())
    {
    }

    public MainViewModel(
        ComponentFileService fileService,
        ProjectFileService projectFileService,
        UndoRedoService undoRedo,
        UnitConversionService unitConverter,
        BomExportService bomExport,
        SnapService snapService,
        PdfCalibrationService calibrationService,
        MarkupRenderService markupRenderer,
        Dimension2DService dimensionService,
        ShadowGeometryTree shadowTree,
        ElectricalCalculationService electricalCalc,
        PlotToPdfService plotService,
        XfdfExportService xfdfService,
        SelectionFilterService selectionFilter)
    {
        FileService = fileService;
        ProjectFileService = projectFileService;
        UndoRedo = undoRedo;
        UnitConverter = unitConverter;
        BomExport = bomExport;
        SnapService = snapService;
        CalibrationService = calibrationService;
        MarkupRenderer = markupRenderer;
        DimensionService = dimensionService;
        ShadowTree = shadowTree;
        ElectricalCalc = electricalCalc;
        PlotService = plotService;
        XfdfService = xfdfService;
        SelectionFilter = selectionFilter;

        InitializeLibrary();
        InitializeLayers();
        InitializeStyles();
        MarkupTool = new MarkupToolViewModel(Markups);
        LayerManager = new LayerManagerViewModel(Layers);
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

    private void InitializeStyles()
    {
        DimensionStyles.Add(new DimensionStyleDefinition { Name = "Standard" });
        DimensionStyles.Add(new DimensionStyleDefinition
        {
            Name = "Architectural",
            UnitFormat = DimensionUnitFormat.Architectural,
            TextHeight = 0.09375,
            ArrowSize = 0.09375,
            ArrowType = ArrowType.Tick
        });
        TextStyles.Add(new TextStyle { Name = "Standard" });
        TextStyles.Add(new TextStyle { Name = "Notes", Height = 0.1, FontFamily = "Consolas" });

        // Apply the first dimension style as active
        ActiveDimensionStyle = DimensionStyles.FirstOrDefault();
    }

    /// <summary>
    /// Builds or refreshes all panel schedules from the current circuits and panels.
    /// </summary>
    public void RebuildPanelSchedules()
    {
        PanelSchedules.Clear();
        var panels = Components.OfType<PanelComponent>().ToList();
        foreach (var panel in panels)
        {
            var schedule = new PanelSchedule
            {
                PanelId = panel.Id,
                PanelName = panel.Name,
                MainBreakerAmps = (int)panel.Amperage,
                BusAmps = (int)panel.Amperage,
                Circuits = Circuits.Where(c =>
                    string.Equals(c.PanelId, panel.Id, StringComparison.Ordinal)).ToList()
            };
            PanelSchedules.Add(schedule);
        }
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
        SelectSingleComponent(component);
        ActionLogService.Instance.Log(LogCategory.Component, "Component added",
            $"Name: {component.Name}, Id: {component.Id}, Layer: {component.LayerId}, Total: {Components.Count}");
    }
    
    public void DeleteSelectedComponent()
    {
        var selected = Components
            .Where(component => SelectedComponentIds.Contains(component.Id))
            .ToList();

        if (selected.Count > 1)
        {
            ActionLogService.Instance.Log(LogCategory.Component, "Deleting selected components",
                $"Count: {selected.Count}, Primary: {SelectedComponent?.Name ?? "(none)"}");

            var actions = selected
                .Select(component => (IUndoableAction)new RemoveComponentAction(Components, component))
                .ToList();
            UndoRedo.Execute(new CompositeAction($"Delete {selected.Count} components", actions));
            ClearComponentSelection();
            return;
        }

        if (SelectedComponent != null)
        {
            ActionLogService.Instance.Log(LogCategory.Component, "Deleting component",
                $"Name: {SelectedComponent.Name}, Type: {SelectedComponent.Type}, Id: {SelectedComponent.Id}");
            var action = new RemoveComponentAction(Components, SelectedComponent);
            UndoRedo.Execute(action);
            ClearComponentSelection();
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

        var oldRotation = SelectedComponent.Rotation;
        var newRotation = SelectedComponent.Rotation + rotation;
        var action = new RotateComponentAction(SelectedComponent, oldRotation, newRotation);
        UndoRedo.Execute(action);
        OnPropertyChanged(nameof(SelectedComponent));
    }

    public void ScaleComponent(Vector3D scale)
    {
        if (SelectedComponent == null) return;
        ActionLogService.Instance.Log(LogCategory.Transform, "Scaling component",
            $"Name: {SelectedComponent.Name}, Scale: ({scale.X:F2}, {scale.Y:F2}, {scale.Z:F2})");

        var oldScale = SelectedComponent.Scale;
        var newScale = new Vector3D(
            SelectedComponent.Scale.X * scale.X,
            SelectedComponent.Scale.Y * scale.Y,
            SelectedComponent.Scale.Z * scale.Z
        );
        var action = new ScaleComponentAction(SelectedComponent, oldScale, newScale);
        UndoRedo.Execute(action);
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
            NamedViews = NamedViews.ToList(),
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

        NamedViews.Clear();
        foreach (var nv in project.NamedViews)
            NamedViews.Add(nv);

        UnitSystemName = project.UnitSystem;
        GridSize = project.GridSize;
        ShowGrid = project.ShowGrid;
        SnapToGrid = project.SnapToGrid;

        UndoRedo.Clear();
        ClearComponentSelection();
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

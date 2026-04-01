using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Rendering;
using ElectricalComponentSandbox.Services;
using ElectricalComponentSandbox.Services.Dimensioning;
using ElectricalComponentSandbox.Services.Export;

namespace ElectricalComponentSandbox.ViewModels;

public sealed record ProjectParameterDraftPreview(
    string Name,
    string Formula,
    ProjectParameterValueKind ValueKind,
    double Value,
    string TextValue,
    string? ErrorMessage)
{
    public bool HasFormula => ValueKind == ProjectParameterValueKind.Length && !string.IsNullOrWhiteSpace(Formula);
    public bool CanSave => string.IsNullOrWhiteSpace(ErrorMessage);
}

public class MainViewModel : INotifyPropertyChanged
{
    private ElectricalComponent? _selectedComponent;
    private DrawingSheet? _selectedSheet;
    private bool _showGrid = true;
    private bool _snapToGrid = true;
    private double _gridSize = 1.0;
    private Layer? _activeLayer;
    private string _projectName = "Untitled Project";
    private string _unitSystem = "Imperial";
    private PlotLayout? _activePlotLayout;
    private PdfUnderlay? _pdfUnderlay;
    private bool _isOrthoActive = false;
    private bool _isPolarActive = false;
    private double _polarIncrementDeg = 45.0;
    private DimensionStyleDefinition? _activeDimensionStyle;
    private readonly ScheduleTableService _scheduleTableService = new();
    private readonly TitleBlockService _titleBlockService = new();
    private readonly RevisionHistoryService _revisionHistoryService = new();
    private readonly DrawingAnnotationMarkupService _drawingAnnotationMarkupService = new();

    public ObservableCollection<ElectricalComponent> Components { get; } = new();
    public ObservableCollection<ElectricalComponent> LibraryComponents { get; } = new();
    public ObservableCollection<ProjectParameterDefinition> ProjectParameters { get; } = new();
    public ObservableCollection<Layer> Layers { get; } = new();
    public ObservableCollection<DrawingSheet> Sheets { get; } = new();
    public ObservableCollection<ProjectBrowserSheetItemViewModel> ProjectBrowserItems { get; } = new();

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

    public ProjectParameterDefinition? GetProjectParameter(string? parameterId)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
            return null;

        return ProjectParameters.FirstOrDefault(parameter => string.Equals(parameter.Id, parameterId, StringComparison.Ordinal));
    }

    public ProjectParameterDefinition UpsertProjectParameter(
        string name,
        double value,
        string? parameterId = null,
        string? formula = null,
        ProjectParameterValueKind valueKind = ProjectParameterValueKind.Length,
        string? textValue = null)
    {
        var preview = BuildProjectParameterPreview(name, value, parameterId, formula, valueKind, textValue, throwOnError: true);
        var trimmedName = preview.Name;
        var trimmedFormula = preview.Formula;

        var parameter = GetProjectParameter(parameterId);
        if (parameter == null)
        {
            parameter = new ProjectParameterDefinition();
            if (!string.IsNullOrWhiteSpace(parameterId))
                parameter.Id = parameterId;
            ProjectParameters.Add(parameter);
        }

        var previousName = parameter.Name;
        parameter.Name = trimmedName;
        parameter.ValueKind = preview.ValueKind;
        parameter.Formula = trimmedFormula;
        parameter.Value = preview.Value;
        parameter.TextValue = preview.TextValue;

        if (!string.IsNullOrWhiteSpace(previousName) &&
            !string.Equals(previousName, trimmedName, StringComparison.OrdinalIgnoreCase))
        {
            PropagateProjectParameterRename(ProjectParameters, previousName, trimmedName, parameter.Id);
        }

        RecalculateProjectParameterValues(throwOnError: true);
        ApplyProjectParameterBindings(BuildProjectParameterLookup());
        OnPropertyChanged(nameof(ProjectParameters));
        return parameter;
    }

    public ProjectParameterDraftPreview PreviewProjectParameter(
        string name,
        double value,
        string? parameterId = null,
        string? formula = null,
        ProjectParameterValueKind valueKind = ProjectParameterValueKind.Length,
        string? textValue = null)
        => BuildProjectParameterPreview(name, value, parameterId, formula, valueKind, textValue, throwOnError: false);

    public bool RemoveProjectParameter(string parameterId)
    {
        var parameter = GetProjectParameter(parameterId);
        if (parameter == null)
            return false;

        ProjectParameters.Remove(parameter);
        foreach (var component in Components)
            component.Parameters.ClearBindingReference(parameterId);

        RecalculateProjectParameterValues(throwOnError: false);
        ApplyProjectParameterBindings(BuildProjectParameterLookup());

        OnPropertyChanged(nameof(ProjectParameters));
        return true;
    }

    public void ClearProjectParameters()
    {
        ProjectParameters.Clear();
        foreach (var component in Components)
        {
            component.Parameters.Bindings = new ComponentParameterBindings();
        }

        OnPropertyChanged(nameof(ProjectParameters));
    }

    public bool TryGetProjectParameterValue(string? parameterId, out double value)
    {
        var parameter = GetProjectParameter(parameterId);
        if (parameter is { ValueKind: ProjectParameterValueKind.Length })
        {
            value = parameter.Value;
            return true;
        }

        value = 0.0;
        return false;
    }

    public bool TryGetProjectParameterTextValue(string? parameterId, out string value)
    {
        var parameter = GetProjectParameter(parameterId);
        if (parameter is { ValueKind: ProjectParameterValueKind.Text })
        {
            value = parameter.TextValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public void ApplyProjectParameterBindings()
    {
        RecalculateProjectParameterValues(throwOnError: false);
        ApplyProjectParameterBindings(BuildProjectParameterLookup());
    }

    public void ApplyProjectParameterBindings(ElectricalComponent component)
    {
        RecalculateProjectParameterValues(throwOnError: false);
        var parameterLookup = BuildProjectParameterLookup();
        ApplyProjectParameterBindings(component, parameterLookup);
        RefreshComponentParameterTagMarkups(parameterLookup);
        RefreshLiveScheduleMarkups();
    }

    private void ApplyProjectParameterBindings(IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup)
    {
        foreach (var component in Components)
            ApplyProjectParameterBindings(component, parameterLookup);

        RefreshComponentParameterTagMarkups(parameterLookup);
        RefreshLiveScheduleMarkups();
    }

    private static void ApplyProjectParameterBindings(ElectricalComponent component, IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup)
    {
        foreach (var target in ProjectParameterBindingTargetExtensions.OrderedTargets)
        {
            var parameterId = component.Parameters.GetBinding(target);
            if (string.IsNullOrWhiteSpace(parameterId) ||
                !parameterLookup.TryGetValue(parameterId, out var parameter) ||
                parameter.ValueKind != target.GetValueKind())
            {
                continue;
            }

            switch (target.GetValueKind())
            {
                case ProjectParameterValueKind.Length:
                    component.Parameters.SetLengthValue(target, parameter.Value);
                    break;
                case ProjectParameterValueKind.Text:
                    component.Parameters.SetTextValue(target, parameter.TextValue);
                    break;
            }
        }
    }

    public void RecalculateProjectParameterValues(bool throwOnError)
        => ProjectParameterFormulaEvaluator.EvaluateAll(ProjectParameters, throwOnError);

    private List<ProjectParameterDefinition> CloneProjectParameters()
    {
        return ProjectParameters
            .Select(parameter => new ProjectParameterDefinition
            {
                Id = parameter.Id,
                Name = parameter.Name,
                ValueKind = parameter.ValueKind,
                Value = parameter.Value,
                TextValue = parameter.TextValue,
                Formula = parameter.Formula
            })
            .ToList();
    }

    private Dictionary<string, ProjectParameterDefinition> BuildProjectParameterLookup()
    {
        return ProjectParameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Id))
            .GroupBy(parameter => parameter.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
    }

    private ProjectParameterDraftPreview BuildProjectParameterPreview(
        string name,
        double value,
        string? parameterId,
        string? formula,
        ProjectParameterValueKind valueKind,
        string? textValue,
        bool throwOnError)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var trimmedFormula = formula?.Trim() ?? string.Empty;
        var normalizedTextValue = textValue ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            const string message = "Project parameter name cannot be empty.";
            if (throwOnError)
                throw new ArgumentException(message, nameof(name));

            return new ProjectParameterDraftPreview(trimmedName, trimmedFormula, valueKind, value, normalizedTextValue, message);
        }

        var duplicate = ProjectParameters.FirstOrDefault(parameter =>
            !string.Equals(parameter.Id, parameterId, StringComparison.Ordinal) &&
            string.Equals(parameter.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
        if (duplicate != null)
        {
            var message = $"A project parameter named '{trimmedName}' already exists.";
            if (throwOnError)
                throw new InvalidOperationException(message);

            return new ProjectParameterDraftPreview(trimmedName, trimmedFormula, valueKind, value, normalizedTextValue, message);
        }

        if (valueKind != ProjectParameterValueKind.Length && !string.IsNullOrWhiteSpace(trimmedFormula))
        {
            const string message = "Text parameters do not support formulas.";
            if (throwOnError)
                throw new InvalidOperationException(message);

            return new ProjectParameterDraftPreview(trimmedName, trimmedFormula, valueKind, value, normalizedTextValue, message);
        }

        var incompatibleBindingMessage = BuildProjectParameterTypeCompatibilityMessage(parameterId, valueKind);
        if (!string.IsNullOrWhiteSpace(incompatibleBindingMessage))
        {
            if (throwOnError)
                throw new InvalidOperationException(incompatibleBindingMessage);

            return new ProjectParameterDraftPreview(trimmedName, trimmedFormula, valueKind, value, normalizedTextValue, incompatibleBindingMessage);
        }

        var previewParameters = CloneProjectParameters();
        var previewParameter = previewParameters.FirstOrDefault(parameter => string.Equals(parameter.Id, parameterId, StringComparison.Ordinal));
        var previousName = previewParameter?.Name;
        if (previewParameter == null)
        {
            previewParameter = new ProjectParameterDefinition();
            if (!string.IsNullOrWhiteSpace(parameterId))
                previewParameter.Id = parameterId;
            previewParameters.Add(previewParameter);
        }

        previewParameter.Name = trimmedName;
        previewParameter.ValueKind = valueKind;
        previewParameter.Formula = valueKind == ProjectParameterValueKind.Length ? trimmedFormula : string.Empty;
        previewParameter.Value = value;
        previewParameter.TextValue = normalizedTextValue;

        if (!string.IsNullOrWhiteSpace(previousName) &&
            !string.Equals(previousName, trimmedName, StringComparison.OrdinalIgnoreCase))
        {
            PropagateProjectParameterRename(previewParameters, previousName, trimmedName, previewParameter.Id);
        }

        ProjectParameterFormulaEvaluator.EvaluateAll(previewParameters, throwOnError);

        var firstInvalid = previewParameters.FirstOrDefault(parameter => !string.IsNullOrWhiteSpace(parameter.FormulaError));
        var errorMessage = firstInvalid == null
            ? null
            : $"Formula for '{firstInvalid.Name}' is invalid: {firstInvalid.FormulaError}";

        return new ProjectParameterDraftPreview(
            trimmedName,
            previewParameter.Formula,
            previewParameter.ValueKind,
            previewParameter.Value,
            previewParameter.TextValue,
            errorMessage);
    }

    private string? BuildProjectParameterTypeCompatibilityMessage(string? parameterId, ProjectParameterValueKind valueKind)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
            return null;

        var incompatibleTargets = Components
            .SelectMany(component => ProjectParameterBindingTargetExtensions.OrderedTargets
                .Where(target => string.Equals(component.Parameters.GetBinding(target), parameterId, StringComparison.Ordinal)))
            .Where(target => target.GetValueKind() != valueKind)
            .Distinct()
            .ToList();

        if (incompatibleTargets.Count == 0)
            return null;

        var targetSummary = string.Join(", ", incompatibleTargets.Select(target => target.GetDisplayName()));
        return $"This parameter is still bound to incompatible component fields ({targetSummary}). Remove those bindings before changing its type.";
    }

    private static void PropagateProjectParameterRename(IEnumerable<ProjectParameterDefinition> parameters, string oldName, string newName, string renamedParameterId)
    {
        foreach (var parameter in parameters)
        {
            if (string.Equals(parameter.Id, renamedParameterId, StringComparison.Ordinal))
                continue;

            parameter.Formula = ReplaceFormulaReference(parameter.Formula, oldName, newName);
        }
    }

    private static string ReplaceFormulaReference(string formula, string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(formula) || string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            return formula;

        return Regex.Replace(
            formula,
            @"\[(?<name>[^\]]+)\]",
            match => string.Equals(match.Groups["name"].Value.Trim(), oldName, StringComparison.OrdinalIgnoreCase)
                ? $"[{newName}]"
                : match.Value);
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

    public DrawingSheet? SelectedSheet
    {
        get => _selectedSheet;
        private set
        {
            _selectedSheet = value;
            OnPropertyChanged();
        }
    }

    public string ProjectName
    {
        get => _projectName;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Untitled Project" : value.Trim();
            if (string.Equals(_projectName, normalized, StringComparison.Ordinal))
                return;

            _projectName = normalized;
            RefreshLiveTitleBlockMarkups();
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

    public PlotLayout? ActivePlotLayout
    {
        get => _activePlotLayout;
        set
        {
            _activePlotLayout = value;
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
        PersistActiveSheetState();
    }

    /// <summary>Removes a markup by id and syncs the shadow tree</summary>
    public bool RemoveMarkup(string markupId)
    {
        var rec = Markups.FirstOrDefault(m => m.Id == markupId);
        if (rec is null) return false;
        Markups.Remove(rec);
        ShadowTree.Remove(markupId);
        PersistActiveSheetState();
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
        PersistActiveSheetState();
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
        InitializeSheets();
        MarkupTool = new MarkupToolViewModel(
            Markups,
            GetProjectReviewMarkups,
            status => TryApplySelectedMarkupStatus(status, Environment.UserName),
            status => ApplyFilteredMarkupStatus(status, Environment.UserName));
        LayerManager = new LayerManagerViewModel(Layers);
        UndoRedo.Changed += (_, _) => HandleUndoRedoChanged();
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

    private void InitializeSheets()
    {
        Sheets.Clear();
        var defaultSheet = DrawingSheet.CreateDefault(1);
        Sheets.Add(defaultSheet);
        LoadSheetState(defaultSheet);
        SelectedSheet = defaultSheet;
        RefreshProjectBrowserItems();
        RefreshMarkupReviewContext();
    }

    private void PersistActiveSheetState()
    {
        if (SelectedSheet == null)
            return;

        SynchronizeLiveScheduleInstancesFromMarkups(
            SelectedSheet,
            Markups,
            removeMissingInstances: SelectedSheet.Markups.Any(IsLiveScheduleMarkup));
        SynchronizeLiveTitleBlockInstancesFromMarkups(
            SelectedSheet,
            Markups,
            removeMissingInstances: SelectedSheet.Markups.Any(IsLiveTitleBlockMarkup));
        SelectedSheet.Markups = Markups.ToList();
        SelectedSheet.NamedViews = NamedViews.ToList();
        SelectedSheet.PdfUnderlay = PdfUnderlay;
        SelectedSheet.PlotLayout = ActivePlotLayout;
        UpdateSheetMarkupReviewContext(SelectedSheet);
    }

    private void LoadSheetState(DrawingSheet sheet)
    {
        UpdateSheetMarkupReviewContext(sheet);

        Markups.Clear();
        ShadowTree.Clear();
        foreach (var markup in sheet.Markups)
        {
            Markups.Add(markup);
            ShadowTree.AddOrUpdate(markup);
        }

        NamedViews.Clear();
        foreach (var namedView in sheet.NamedViews)
            NamedViews.Add(namedView);

        PdfUnderlay = sheet.PdfUnderlay;
        ActivePlotLayout = sheet.PlotLayout;
        if (MarkupTool != null)
            MarkupTool.SelectedMarkup = null;
    }

    private static DrawingSheet CreateSheetFromLegacyProject(ProjectModel project)
    {
        var sheet = DrawingSheet.CreateDefault(1);
        sheet.Name = string.IsNullOrWhiteSpace(project.Name) ? sheet.Name : project.Name;
        sheet.Markups = project.Markups.ToList();
        sheet.NamedViews = project.NamedViews.ToList();
        sheet.PdfUnderlay = project.PdfUnderlay;
        sheet.PlotLayout = project.PlotLayout;
        foreach (var markup in sheet.Markups)
            markup.ReviewSheetDisplayText = sheet.DisplayName;
        return sheet;
    }

    private static void TouchSheet(DrawingSheet sheet, string? actor, bool initializeCreatedMetadata = false)
    {
        var resolvedActor = string.IsNullOrWhiteSpace(actor) ? Environment.UserName : actor.Trim();
        var utcNow = DateTime.UtcNow;

        if (initializeCreatedMetadata)
        {
            if (sheet.CreatedUtc == default)
                sheet.CreatedUtc = utcNow;
            if (string.IsNullOrWhiteSpace(sheet.CreatedBy))
                sheet.CreatedBy = resolvedActor;
        }

        sheet.ModifiedUtc = utcNow;
        sheet.ModifiedBy = resolvedActor;
    }

    public DrawingSheet AddSheet(string? name = null)
    {
        PersistActiveSheetState();

        var sheet = DrawingSheet.CreateDefault(Sheets.Count + 1);
        if (!string.IsNullOrWhiteSpace(name))
            sheet.Name = name.Trim();

        TouchSheet(sheet, Environment.UserName, initializeCreatedMetadata: true);

        Sheets.Add(sheet);
        SelectSheet(sheet);
        RefreshLiveTitleBlockMarkups();
        RefreshProjectBrowserItems();
        RefreshMarkupReviewContext();

        ActionLogService.Instance.Log(LogCategory.View, "Sheet added",
            $"Sheet: {sheet.DisplayName}, Total: {Sheets.Count}");
        return sheet;
    }

    public bool RenameSheet(DrawingSheet? sheet, string? number, string? name)
    {
        if (sheet == null)
            return false;

        var normalizedNumber = string.IsNullOrWhiteSpace(number) ? sheet.Number : number.Trim();
        var normalizedName = string.IsNullOrWhiteSpace(name) ? sheet.Name : name.Trim();
        if (string.Equals(sheet.Number, normalizedNumber, StringComparison.Ordinal) &&
            string.Equals(sheet.Name, normalizedName, StringComparison.Ordinal))
        {
            return false;
        }

        sheet.Number = normalizedNumber;
        sheet.Name = normalizedName;
        TouchSheet(sheet, Environment.UserName);
        RefreshProjectBrowserItems();
        UpdateSheetMarkupReviewContext(sheet);
        MarkupTool?.RefreshReviewContext();
        OnPropertyChanged(nameof(Sheets));
        if (ReferenceEquals(SelectedSheet, sheet))
            OnPropertyChanged(nameof(SelectedSheet));

        RefreshLiveTitleBlockMarkups();

        ActionLogService.Instance.Log(LogCategory.View, "Sheet renamed",
            $"Sheet: {sheet.DisplayName}");
        return true;
    }

    public bool DeleteSheet(DrawingSheet? sheet)
    {
        if (sheet == null || Sheets.Count <= 1)
            return false;

        PersistActiveSheetState();
        var index = Sheets.IndexOf(sheet);
        if (index < 0)
            return false;

        var deletedWasActive = ReferenceEquals(SelectedSheet, sheet);
        var currentActiveSheet = SelectedSheet;
        Sheets.RemoveAt(index);

        var replacement = deletedWasActive
            ? Sheets[Math.Max(0, Math.Min(index, Sheets.Count - 1))]
            : currentActiveSheet ?? Sheets.First();

        if (deletedWasActive)
        {
            LoadSheetState(replacement);
            SelectedSheet = replacement;
            ClearComponentSelection();
        }
        else
        {
            SelectedSheet = replacement;
        }

        RefreshLiveTitleBlockMarkups();
        RefreshMarkupReviewContext();
    RefreshProjectBrowserItems();
        OnPropertyChanged(nameof(Sheets));

        ActionLogService.Instance.Log(LogCategory.View, "Sheet deleted",
            $"Deleted: {sheet.DisplayName}, Active: {replacement.DisplayName}, Total: {Sheets.Count}");
        return true;
    }

    public bool MoveSheet(DrawingSheet? sheet, int direction)
    {
        if (sheet == null || direction == 0)
            return false;

        var index = Sheets.IndexOf(sheet);
        if (index < 0)
            return false;

        var targetIndex = index + direction;
        if (targetIndex < 0 || targetIndex >= Sheets.Count)
            return false;

        Sheets.Move(index, targetIndex);
        RefreshLiveTitleBlockMarkups();
        RefreshProjectBrowserItems();
        RefreshMarkupReviewContext();
        OnPropertyChanged(nameof(Sheets));

        ActionLogService.Instance.Log(LogCategory.View, "Sheet reordered",
            $"Sheet: {sheet.DisplayName}, NewIndex: {targetIndex + 1}");
        return true;
    }

    public bool SelectSheet(DrawingSheet? sheet)
    {
        if (sheet == null || ReferenceEquals(SelectedSheet, sheet))
            return false;

        PersistActiveSheetState();
        LoadSheetState(sheet);
        SelectedSheet = sheet;
        ClearComponentSelection();
        RefreshProjectBrowserItems();
        RefreshMarkupReviewContext();

        ActionLogService.Instance.Log(LogCategory.View, "Sheet selected",
            $"Sheet: {sheet.DisplayName}, Markups: {Markups.Count}, Views: {NamedViews.Count}");
        return true;
    }

    public void ResetDrawingSheets()
    {
        InitializeSheets();
        OnPropertyChanged(nameof(Sheets));
    }

    public void RefreshProjectBrowserItems()
    {
        ProjectBrowserItems.Clear();

        foreach (var sheet in Sheets)
        {
            var sheetItem = new ProjectBrowserSheetItemViewModel(sheet)
            {
                IsExpanded = true,
                IsSelected = ReferenceEquals(sheet, SelectedSheet)
            };

            foreach (var namedView in GetNamedViewsForSheet(sheet))
            {
                sheetItem.Children.Add(new ProjectBrowserNamedViewItemViewModel(sheet, namedView));
            }

            ProjectBrowserItems.Add(sheetItem);
        }

        OnPropertyChanged(nameof(ProjectBrowserItems));
    }

    public IReadOnlyList<MarkupRecord> GetReviewMarkups()
    {
        return MarkupTool.ReviewScope == MarkupReviewScope.AllSheets
            ? GetProjectReviewMarkups()
            : Markups.ToList();
    }

    public IReadOnlyList<MarkupRecord> GetFilteredReviewMarkups()
        => MarkupTool.GetFilteredReviewMarkups();

    public IReadOnlyList<MarkupRecord> GetSelectedIssueGroupReviewMarkups()
        => MarkupTool.GetSelectedIssueGroupMarkups();

    public bool TryApplySelectedMarkupStatus(MarkupStatus newStatus, string actor)
    {
        var markup = MarkupTool.SelectedMarkup;
        if (markup == null)
            return false;

        return TryApplyMarkupStatus(markup, newStatus, actor);
    }

    public int ApplyFilteredMarkupStatus(MarkupStatus newStatus, string actor)
    {
        var markups = GetFilteredReviewMarkups()
            .Where(markup => markup.Status != newStatus)
            .ToList();

        return ApplyMarkupStatus(markups, newStatus, actor);
    }

    public int ApplySelectedIssueGroupStatus(MarkupStatus newStatus, string actor)
    {
        var markups = GetSelectedIssueGroupReviewMarkups()
            .Where(markup => markup.Status != newStatus)
            .ToList();

        return ApplyMarkupStatus(markups, newStatus, actor);
    }

    private int ApplyMarkupStatus(IReadOnlyList<MarkupRecord> markups, MarkupStatus newStatus, string actor)
    {

        if (markups.Count == 0)
            return 0;

        var actions = markups
            .Select(markup => (IUndoableAction)CreateMarkupStatusAction(markup, newStatus, actor))
            .ToList();

        UndoRedo.Execute(actions.Count == 1
            ? actions[0]
            : new CompositeAction($"Set {markups.Count} markup statuses to {MarkupRecord.GetStatusDisplayText(newStatus)}", actions));

        MarkupTool.RefreshReviewContext();
        return markups.Count;
    }

    public bool TryAssignSelectedMarkup(string? assignee, string actor)
    {
        var markup = MarkupTool.SelectedMarkup;
        if (markup == null)
            return false;

        return TryApplyMarkupAssignment(markup, assignee, actor);
    }

    public int ApplyFilteredMarkupAssignment(string? assignee, string actor)
    {
        var normalizedAssignee = NormalizeAssignee(assignee);
        var markups = GetFilteredReviewMarkups()
            .Where(markup => !string.Equals(NormalizeAssignee(markup.AssignedTo), normalizedAssignee, StringComparison.Ordinal))
            .ToList();

        return ApplyMarkupAssignment(markups, normalizedAssignee, actor);
    }

    public int ApplySelectedIssueGroupAssignment(string? assignee, string actor)
    {
        var normalizedAssignee = NormalizeAssignee(assignee);
        var markups = GetSelectedIssueGroupReviewMarkups()
            .Where(markup => !string.Equals(NormalizeAssignee(markup.AssignedTo), normalizedAssignee, StringComparison.Ordinal))
            .ToList();

        return ApplyMarkupAssignment(markups, normalizedAssignee, actor);
    }

    private int ApplyMarkupAssignment(IReadOnlyList<MarkupRecord> markups, string? normalizedAssignee, string actor)
    {

        if (markups.Count == 0)
            return 0;

        var actions = markups
            .Select(markup => (IUndoableAction)CreateMarkupAssignmentAction(markup, normalizedAssignee, actor))
            .ToList();

        UndoRedo.Execute(actions.Count == 1
            ? actions[0]
            : new CompositeAction($"Assign {markups.Count} markups", actions));

        MarkupTool.RefreshReviewContext();
        return markups.Count;
    }

    public string GetMarkupSheetDisplayName(MarkupRecord markup)
    {
        if (!string.IsNullOrWhiteSpace(markup.ReviewSheetDisplayText))
            return markup.ReviewSheetDisplayText;

        foreach (var sheet in Sheets)
        {
            if (sheet.Markups.Any(candidate => string.Equals(candidate.Id, markup.Id, StringComparison.Ordinal)))
                return sheet.DisplayName;
        }

        return SelectedSheet?.DisplayName ?? string.Empty;
    }

    public bool RevealMarkup(MarkupRecord? markup)
    {
        if (markup == null)
            return false;

        PersistActiveSheetState();

        var ownerSheet = Sheets.FirstOrDefault(sheet =>
            sheet.Markups.Any(candidate => string.Equals(candidate.Id, markup.Id, StringComparison.Ordinal)));

        if (ownerSheet == null)
            return false;

        if (!ReferenceEquals(SelectedSheet, ownerSheet))
            SelectSheet(ownerSheet);

        var activeMarkup = Markups.FirstOrDefault(candidate => string.Equals(candidate.Id, markup.Id, StringComparison.Ordinal));
        if (activeMarkup == null)
            return false;

        MarkupTool.SelectedMarkup = activeMarkup;
        return true;
    }

    private bool TryApplyMarkupStatus(MarkupRecord markup, MarkupStatus newStatus, string actor)
    {
        if (markup.Status == newStatus)
            return false;

        UndoRedo.Execute(CreateMarkupStatusAction(markup, newStatus, actor));
        MarkupTool.RefreshReviewContext();
        return true;
    }

    private bool TryApplyMarkupAssignment(MarkupRecord markup, string? assignee, string actor)
    {
        var normalizedAssignee = NormalizeAssignee(assignee);
        if (string.Equals(NormalizeAssignee(markup.AssignedTo), normalizedAssignee, StringComparison.Ordinal))
            return false;

        UndoRedo.Execute(CreateMarkupAssignmentAction(markup, normalizedAssignee, actor));
        MarkupTool.RefreshReviewContext();
        return true;
    }

    private static MarkupStatusAction CreateMarkupStatusAction(MarkupRecord markup, MarkupStatus newStatus, string actor)
    {
        var utcNow = DateTime.UtcNow;
        var oldStatus = markup.Status;
        var author = string.IsNullOrWhiteSpace(actor) ? Environment.UserName : actor;
        var auditText = $"Status changed: {MarkupRecord.GetStatusDisplayText(oldStatus)} -> {MarkupRecord.GetStatusDisplayText(newStatus)}";
        var auditReply = new MarkupReply
        {
            Author = author,
            Text = auditText,
            Kind = MarkupReplyKind.StatusAudit,
            CreatedUtc = utcNow,
            ModifiedUtc = utcNow
        };

        return new MarkupStatusAction(markup, newStatus, auditText, auditReply, utcNow);
    }

    private static MarkupAssignmentAction CreateMarkupAssignmentAction(MarkupRecord markup, string? assignee, string actor)
    {
        var utcNow = DateTime.UtcNow;
        var author = string.IsNullOrWhiteSpace(actor) ? Environment.UserName : actor;
        var oldAssignedTo = NormalizeAssignee(markup.AssignedTo);
        var newAssignedTo = NormalizeAssignee(assignee);
        var oldDisplay = string.IsNullOrWhiteSpace(oldAssignedTo) ? "(unassigned)" : oldAssignedTo;
        var newDisplay = string.IsNullOrWhiteSpace(newAssignedTo) ? "(unassigned)" : newAssignedTo;
        var auditReply = new MarkupReply
        {
            Author = author,
            Text = $"Assignment changed: {oldDisplay} -> {newDisplay}",
            Kind = MarkupReplyKind.AssignmentAudit,
            CreatedUtc = utcNow,
            ModifiedUtc = utcNow
        };

        return new MarkupAssignmentAction(markup, newAssignedTo, auditReply, utcNow);
    }

    private static string? NormalizeAssignee(string? assignee)
    {
        var normalized = assignee?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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

    public ScheduleTable GenerateLiveScheduleTable(LiveScheduleKind kind)
    {
        return kind switch
        {
            LiveScheduleKind.Equipment => _scheduleTableService.GenerateEquipmentSchedule(Components.ToList(), ProjectParameters.ToList()),
            LiveScheduleKind.Conduit => _scheduleTableService.GenerateConduitSchedule(Components.ToList(), ProjectParameters.ToList()),
            LiveScheduleKind.CircuitSummary => _scheduleTableService.GenerateCircuitSummary(Circuits.ToList()),
            LiveScheduleKind.ProjectParameter => _scheduleTableService.GenerateProjectParameterSchedule(ProjectParameters.ToList(), Components.ToList()),
            _ => throw new InvalidOperationException($"Unsupported live schedule kind '{kind}'.")
        };
    }

    public TitleBlockBorderGeometry GenerateLiveTitleBlockGeometry(LiveTitleBlockInstance instance, DrawingSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(sheet);

        NormalizeLiveTitleBlockInstance(instance);
        var sheetIndex = Math.Max(1, Sheets.IndexOf(sheet) + 1);
        var resolvedTemplate = _titleBlockService.BuildResolvedTemplate(
            instance.Template,
            ProjectName,
            sheet,
            sheetIndex,
            Math.Max(1, Sheets.Count));
        return _titleBlockService.GenerateBorderGeometry(resolvedTemplate, sheet.RevisionEntries);
    }

    public RevisionEntry AddSheetRevision(
        DrawingSheet sheet,
        string description,
        string? author = null,
        string? revisionNumber = null,
        string? revisionDate = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var entry = _revisionHistoryService.CreateRevisionEntry(sheet, description, author, revisionNumber, revisionDate);
        _revisionHistoryService.AddRevision(sheet, entry);
        TouchSheet(sheet, entry.Author);
        RenderLiveTitleBlockMarkupsForSheet(sheet, updateActiveCollections: ReferenceEquals(sheet, SelectedSheet), synchronizeFromExistingMarkups: false);
        PersistActiveSheetState();
        RefreshMarkupReviewContext();
        return entry;
    }

    public string GetNextSheetRevisionNumber(DrawingSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return _revisionHistoryService.GetNextRevisionNumber(sheet.RevisionEntries);
    }

    public bool RemoveSheetRevision(DrawingSheet sheet, string revisionId)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (!_revisionHistoryService.RemoveRevision(sheet, revisionId))
            return false;

        TouchSheet(sheet, Environment.UserName);
        RenderLiveTitleBlockMarkupsForSheet(sheet, updateActiveCollections: ReferenceEquals(sheet, SelectedSheet), synchronizeFromExistingMarkups: false);
        PersistActiveSheetState();
        RefreshMarkupReviewContext();
        return true;
    }

    public bool UpdateSheetRevision(
        DrawingSheet sheet,
        string revisionId,
        string revisionNumber,
        string revisionDate,
        string description,
        string? author = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (!_revisionHistoryService.UpdateRevision(sheet, revisionId, revisionNumber, revisionDate, description, author))
            return false;

        var actor = string.IsNullOrWhiteSpace(author) ? Environment.UserName : author.Trim();
        TouchSheet(sheet, actor);
        RenderLiveTitleBlockMarkupsForSheet(sheet, updateActiveCollections: ReferenceEquals(sheet, SelectedSheet), synchronizeFromExistingMarkups: false);
        PersistActiveSheetState();
        RefreshMarkupReviewContext();
        return true;
    }

    public bool SetSheetStatus(DrawingSheet sheet, DrawingSheetStatus status, string? actor = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (sheet.Status == status)
            return false;

        var resolvedActor = string.IsNullOrWhiteSpace(actor) ? Environment.UserName : actor.Trim();
        sheet.Status = status;
        TouchSheet(sheet, resolvedActor);
        if (status == DrawingSheetStatus.Approved)
        {
            sheet.ApprovedBy = resolvedActor;
            sheet.ApprovedUtc = DateTime.UtcNow;
        }
        else
        {
            sheet.ApprovedBy = null;
            sheet.ApprovedUtc = null;
        }

        RefreshProjectBrowserItems();
        OnPropertyChanged(nameof(Sheets));
        if (ReferenceEquals(SelectedSheet, sheet))
            OnPropertyChanged(nameof(SelectedSheet));
        return true;
    }

    public void AddLiveScheduleInstance(DrawingSheet sheet, LiveScheduleInstance instance)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(instance);

        NormalizeLiveScheduleInstance(instance);
        if (sheet.LiveSchedules.Any(existing => string.Equals(existing.Id, instance.Id, StringComparison.Ordinal)))
            return;

        sheet.LiveSchedules.Add(instance);
        RenderLiveScheduleMarkupsForSheet(sheet, updateActiveCollections: ReferenceEquals(sheet, SelectedSheet), synchronizeFromExistingMarkups: false);
        PersistActiveSheetState();
        RefreshMarkupReviewContext();
    }

    public bool RemoveLiveScheduleInstance(DrawingSheet sheet, string instanceId)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var instance = sheet.LiveSchedules.FirstOrDefault(existing => string.Equals(existing.Id, instanceId, StringComparison.Ordinal));
        if (instance == null)
            return false;

        sheet.LiveSchedules.Remove(instance);
        RenderLiveScheduleMarkupsForSheet(sheet, updateActiveCollections: ReferenceEquals(sheet, SelectedSheet), synchronizeFromExistingMarkups: false);
        PersistActiveSheetState();
        RefreshMarkupReviewContext();
        return true;
    }

    public void AddLiveTitleBlockInstance(DrawingSheet sheet, LiveTitleBlockInstance instance)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(instance);

        NormalizeLiveTitleBlockInstance(instance);
        if (sheet.LiveTitleBlocks.Any(existing => string.Equals(existing.Id, instance.Id, StringComparison.Ordinal)))
            return;

        sheet.LiveTitleBlocks.Add(instance);
        RenderLiveTitleBlockMarkupsForSheet(sheet, updateActiveCollections: ReferenceEquals(sheet, SelectedSheet), synchronizeFromExistingMarkups: false);
        PersistActiveSheetState();
        RefreshMarkupReviewContext();
    }

    public bool RemoveLiveTitleBlockInstance(DrawingSheet sheet, string instanceId)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var instance = sheet.LiveTitleBlocks.FirstOrDefault(existing => string.Equals(existing.Id, instanceId, StringComparison.Ordinal));
        if (instance == null)
            return false;

        sheet.LiveTitleBlocks.Remove(instance);
        RenderLiveTitleBlockMarkupsForSheet(sheet, updateActiveCollections: ReferenceEquals(sheet, SelectedSheet), synchronizeFromExistingMarkups: false);
        PersistActiveSheetState();
        RefreshMarkupReviewContext();
        return true;
    }

    public bool UpdateLiveTitleBlockFieldValue(string instanceId, string fieldLabel, string? value)
    {
        if (string.IsNullOrWhiteSpace(instanceId) ||
            string.IsNullOrWhiteSpace(fieldLabel) ||
            TitleBlockService.IsLiveBoundFieldLabel(fieldLabel))
        {
            return false;
        }

        foreach (var sheet in Sheets)
        {
            var instance = sheet.LiveTitleBlocks.FirstOrDefault(existing => string.Equals(existing.Id, instanceId, StringComparison.Ordinal));
            if (instance == null)
                continue;

            NormalizeLiveTitleBlockInstance(instance);
            if (!TitleBlockService.TrySetFieldValue(instance.Template, fieldLabel, value))
                return false;

            RenderLiveTitleBlockMarkupsForSheet(sheet, updateActiveCollections: ReferenceEquals(sheet, SelectedSheet), synchronizeFromExistingMarkups: false);
            PersistActiveSheetState();
            RefreshMarkupReviewContext();
            return true;
        }

        return false;
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
        RefreshComponentParameterTagMarkups(BuildProjectParameterLookup());
        RefreshMarkupReviewContext();
        OnPropertyChanged(nameof(SelectedComponent));
    }
    
    public void Redo()
    {
        ActionLogService.Instance.Log(LogCategory.Edit, "Redo", $"CanRedo: {UndoRedo.CanRedo}");
        UndoRedo.Redo();
        RefreshComponentParameterTagMarkups(BuildProjectParameterLookup());
        RefreshMarkupReviewContext();
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
        PersistActiveSheetState();
        ApplyProjectParameterBindings();

        var activeSheet = SelectedSheet ?? Sheets.FirstOrDefault();
        ActionLogService.Instance.Log(LogCategory.FileOperation, "Creating project model",
            $"Components: {Components.Count}, Layers: {Layers.Count}, Sheets: {Sheets.Count}, ActiveSheet: {activeSheet?.DisplayName ?? "(none)"}");
        return new ProjectModel
        {
            Name = ProjectName,
            Components = Components.ToList(),
            Circuits = Circuits.ToList(),
            ProjectParameters = ProjectParameters.ToList(),
            Layers = Layers.ToList(),
            Sheets = Sheets.ToList(),
            ActiveSheetId = activeSheet?.Id,
            Markups = activeSheet?.Markups.ToList() ?? Markups.ToList(),
            NamedViews = activeSheet?.NamedViews.ToList() ?? NamedViews.ToList(),
            PdfUnderlay = activeSheet?.PdfUnderlay ?? PdfUnderlay,
            PlotLayout = activeSheet?.PlotLayout ?? ActivePlotLayout,
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
            $"Name: {project.Name}, Components: {project.Components.Count}, Layers: {project.Layers.Count}, Sheets: {project.Sheets.Count}, Units: {project.UnitSystem}");
        Components.Clear();
        foreach (var comp in project.Components)
            Components.Add(comp);

        Circuits.Clear();
        foreach (var circuit in project.Circuits)
            Circuits.Add(circuit);

        ProjectParameters.Clear();
        foreach (var parameter in project.ProjectParameters)
            ProjectParameters.Add(parameter);

        Layers.Clear();
        foreach (var layer in project.Layers)
            Layers.Add(layer);

        if (Layers.Count == 0)
            InitializeLayers();

        ActiveLayer = Layers.FirstOrDefault(l => l.Id == "default") ?? Layers.FirstOrDefault();

        Sheets.Clear();
        var sheets = project.Sheets.Count > 0
            ? project.Sheets
            : new List<DrawingSheet> { CreateSheetFromLegacyProject(project) };

        foreach (var sheet in sheets)
        {
            UpdateSheetMarkupReviewContext(sheet);
            Sheets.Add(sheet);
        }

        var firstSheet = !string.IsNullOrWhiteSpace(project.ActiveSheetId)
            ? Sheets.FirstOrDefault(sheet => string.Equals(sheet.Id, project.ActiveSheetId, StringComparison.Ordinal))
            : null;
        firstSheet ??= Sheets.FirstOrDefault();
        if (firstSheet == null)
        {
            firstSheet = DrawingSheet.CreateDefault(1);
            Sheets.Add(firstSheet);
        }

        LoadSheetState(firstSheet);
        SelectedSheet = firstSheet;
        RefreshProjectBrowserItems();
        RefreshMarkupReviewContext();

        ProjectName = string.IsNullOrWhiteSpace(project.Name)
            ? "Untitled Project"
            : project.Name;

        UnitSystemName = project.UnitSystem;
        GridSize = project.GridSize;
        ShowGrid = project.ShowGrid;
        SnapToGrid = project.SnapToGrid;

        RebuildPanelSchedules();
        ApplyProjectParameterBindings();
        RefreshLiveTitleBlockMarkups();

        UndoRedo.Clear();
        ClearComponentSelection();
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void RefreshComponentParameterTagMarkups()
        => RefreshComponentParameterTagMarkups(BuildProjectParameterLookup());

    public void RefreshLiveScheduleMarkups()
    {
        var refreshedAny = false;
        foreach (var sheet in Sheets)
        {
            var currentMarkups = ReferenceEquals(sheet, SelectedSheet)
                ? Markups.ToList()
                : sheet.Markups.ToList();
            if (sheet.LiveSchedules.Count == 0 && !currentMarkups.Any(IsLiveScheduleMarkup))
                continue;

            if (ReferenceEquals(sheet, SelectedSheet))
                SynchronizeLiveScheduleInstancesFromMarkups(
                    sheet,
                    currentMarkups,
                    removeMissingInstances: sheet.Markups.Any(IsLiveScheduleMarkup));

            RenderLiveScheduleMarkupsForSheet(
                sheet,
                updateActiveCollections: ReferenceEquals(sheet, SelectedSheet),
                synchronizeFromExistingMarkups: false);
            refreshedAny = true;
        }

        if (!refreshedAny)
            return;

        PersistActiveSheetState();
        RefreshMarkupReviewContext();
    }

    public void RefreshLiveTitleBlockMarkups()
    {
        var refreshedAny = false;
        foreach (var sheet in Sheets)
        {
            var currentMarkups = ReferenceEquals(sheet, SelectedSheet)
                ? Markups.ToList()
                : sheet.Markups.ToList();
            if (sheet.LiveTitleBlocks.Count == 0 && !currentMarkups.Any(IsLiveTitleBlockMarkup))
                continue;

            if (ReferenceEquals(sheet, SelectedSheet))
                SynchronizeLiveTitleBlockInstancesFromMarkups(
                    sheet,
                    currentMarkups,
                    removeMissingInstances: sheet.Markups.Any(IsLiveTitleBlockMarkup));

            RenderLiveTitleBlockMarkupsForSheet(
                sheet,
                updateActiveCollections: ReferenceEquals(sheet, SelectedSheet),
                synchronizeFromExistingMarkups: false);
            refreshedAny = true;
        }

        if (!refreshedAny)
            return;

        PersistActiveSheetState();
        RefreshMarkupReviewContext();
    }

    private void RefreshComponentParameterTagMarkups(IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup)
    {
        var componentsById = Components
            .Where(component => !string.IsNullOrWhiteSpace(component.Id))
            .GroupBy(component => component.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        var anyChanged = RefreshComponentParameterTagMarkups(Markups, componentsById, parameterLookup, updateShadowTree: true);
        foreach (var sheet in Sheets)
        {
            if (ReferenceEquals(sheet, SelectedSheet))
                continue;

            anyChanged |= RefreshComponentParameterTagMarkups(sheet.Markups, componentsById, parameterLookup, updateShadowTree: false);
        }

        if (!anyChanged)
            return;

        PersistActiveSheetState();
        RefreshMarkupReviewContext();
    }

    private bool RefreshComponentParameterTagMarkups(
        IEnumerable<MarkupRecord> markups,
        IReadOnlyDictionary<string, ElectricalComponent> componentsById,
        IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup,
        bool updateShadowTree)
    {
        var anyChanged = false;
        foreach (var markup in markups)
        {
            if (!TryRefreshComponentParameterTagMarkup(markup, componentsById, parameterLookup))
                continue;

            anyChanged = true;
            if (updateShadowTree)
                ShadowTree.AddOrUpdate(markup);
        }

        return anyChanged;
    }

    private bool TryRefreshComponentParameterTagMarkup(
        MarkupRecord markup,
        IReadOnlyDictionary<string, ElectricalComponent> componentsById,
        IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup)
    {
        if (!TryGetComponentParameterTagBinding(markup, out var componentId, out var target))
            return false;

        var label = markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationTextKeyField, out var textKey) &&
                    !string.IsNullOrWhiteSpace(textKey)
            ? textKey
            : target.GetDisplayName();

        var nextText = componentsById.TryGetValue(componentId, out var component)
            ? ProjectParameterScheduleSupport.BuildComponentTagText(component, target, parameterLookup, useFriendlyLengthFormatting: UnitSystemName == "Imperial")
            : $"{label}: (missing component)";

        if (string.Equals(markup.TextContent, nextText, StringComparison.Ordinal))
            return false;

        DrawingAnnotationMarkupService.UpdateTextMarkupText(markup, nextText);
        return true;
    }

    private static bool TryGetComponentParameterTagBinding(
        MarkupRecord markup,
        out string componentId,
        out ProjectParameterBindingTarget target)
    {
        componentId = string.Empty;
        target = default;

        if (markup.Type != MarkupType.Text)
            return false;

        if (!markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationKindField, out var annotationKind) ||
            !string.Equals(annotationKind, DrawingAnnotationMarkupService.ComponentParameterTagAnnotationKind, StringComparison.Ordinal))
        {
            return false;
        }

        if (!markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.ComponentParameterTagComponentIdField, out var componentIdValue) ||
            string.IsNullOrWhiteSpace(componentIdValue) ||
            !markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.ComponentParameterTagTargetField, out var targetText) ||
            !Enum.TryParse(targetText, out target))
        {
            componentId = string.Empty;
            return false;
        }

        componentId = componentIdValue;

        return true;
    }

    private IReadOnlyList<MarkupRecord> GetProjectReviewMarkups()
    {
        return Sheets
            .SelectMany(sheet => ReferenceEquals(sheet, SelectedSheet)
                ? (IEnumerable<MarkupRecord>)Markups
                : sheet.Markups)
            .ToList();
    }

    private IReadOnlyList<NamedView> GetNamedViewsForSheet(DrawingSheet sheet)
    {
        return ReferenceEquals(sheet, SelectedSheet)
            ? NamedViews.ToList()
            : sheet.NamedViews;
    }

    private void HandleUndoRedoChanged()
    {
        RebuildPanelSchedules();
        RefreshComponentParameterTagMarkups(BuildProjectParameterLookup());
        RefreshLiveScheduleMarkups();
        RefreshLiveTitleBlockMarkups();
    }

    private void RenderLiveTitleBlockMarkupsForSheet(
        DrawingSheet sheet,
        bool updateActiveCollections,
        bool synchronizeFromExistingMarkups)
    {
        var currentMarkups = updateActiveCollections
            ? Markups.ToList()
            : sheet.Markups.ToList();

        if (synchronizeFromExistingMarkups)
            SynchronizeLiveTitleBlockInstancesFromMarkups(
                sheet,
                currentMarkups,
                removeMissingInstances: sheet.Markups.Any(IsLiveTitleBlockMarkup));

        if (sheet.LiveTitleBlocks.Count == 0 && !currentMarkups.Any(IsLiveTitleBlockMarkup))
            return;

        var renderedMarkups = BuildRenderedMarkupsWithLiveTitleBlocks(sheet, currentMarkups);
        ApplyRenderedMarkupsToSheet(sheet, renderedMarkups, updateActiveCollections);
    }

    private IReadOnlyList<MarkupRecord> BuildRenderedMarkupsWithLiveTitleBlocks(
        DrawingSheet sheet,
        IReadOnlyList<MarkupRecord> currentMarkups)
    {
        var renderedByInstanceId = sheet.LiveTitleBlocks
            .ToDictionary(instance => instance.Id, instance => CreateLiveTitleBlockMarkups(sheet, instance), StringComparer.Ordinal);
        var emittedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        var finalMarkups = new List<MarkupRecord>(currentMarkups.Count);

        foreach (var markup in currentMarkups)
        {
            if (!TryGetLiveTitleBlockInstanceId(markup, out var instanceId))
            {
                finalMarkups.Add(markup);
                continue;
            }

            if (!renderedByInstanceId.TryGetValue(instanceId, out var renderedMarkups))
                continue;

            if (!emittedInstanceIds.Add(instanceId))
                continue;

            finalMarkups.AddRange(renderedMarkups);
        }

        foreach (var instance in sheet.LiveTitleBlocks)
        {
            if (emittedInstanceIds.Add(instance.Id) &&
                renderedByInstanceId.TryGetValue(instance.Id, out var renderedMarkups))
            {
                finalMarkups.AddRange(renderedMarkups);
            }
        }

        return finalMarkups;
    }

    private IReadOnlyList<MarkupRecord> CreateLiveTitleBlockMarkups(DrawingSheet sheet, LiveTitleBlockInstance instance)
    {
        NormalizeLiveTitleBlockInstance(instance);
        var geometry = GenerateLiveTitleBlockGeometry(instance, sheet);
        return _drawingAnnotationMarkupService.CreateTitleBlockMarkups(
            geometry,
            instance.Origin,
            instance.LayerId,
            groupId: instance.GroupId,
            liveTitleBlockInstanceId: instance.Id);
    }

    private void RenderLiveScheduleMarkupsForSheet(
        DrawingSheet sheet,
        bool updateActiveCollections,
        bool synchronizeFromExistingMarkups)
    {
        var currentMarkups = updateActiveCollections
            ? Markups.ToList()
            : sheet.Markups.ToList();

        if (synchronizeFromExistingMarkups)
            SynchronizeLiveScheduleInstancesFromMarkups(
                sheet,
                currentMarkups,
                removeMissingInstances: sheet.Markups.Any(IsLiveScheduleMarkup));

        if (sheet.LiveSchedules.Count == 0 && !currentMarkups.Any(IsLiveScheduleMarkup))
            return;

        var renderedMarkups = BuildRenderedMarkupsWithLiveSchedules(sheet, currentMarkups);
        ApplyRenderedMarkupsToSheet(sheet, renderedMarkups, updateActiveCollections);
    }

    private IReadOnlyList<MarkupRecord> BuildRenderedMarkupsWithLiveSchedules(
        DrawingSheet sheet,
        IReadOnlyList<MarkupRecord> currentMarkups)
    {
        var renderedByInstanceId = sheet.LiveSchedules
            .ToDictionary(instance => instance.Id, CreateLiveScheduleMarkups, StringComparer.Ordinal);
        var emittedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        var finalMarkups = new List<MarkupRecord>(currentMarkups.Count);

        foreach (var markup in currentMarkups)
        {
            if (!TryGetLiveScheduleInstanceId(markup, out var instanceId))
            {
                finalMarkups.Add(markup);
                continue;
            }

            if (!renderedByInstanceId.TryGetValue(instanceId, out var renderedMarkups))
                continue;

            if (!emittedInstanceIds.Add(instanceId))
                continue;

            finalMarkups.AddRange(renderedMarkups);
        }

        foreach (var instance in sheet.LiveSchedules)
        {
            if (emittedInstanceIds.Add(instance.Id) &&
                renderedByInstanceId.TryGetValue(instance.Id, out var renderedMarkups))
            {
                finalMarkups.AddRange(renderedMarkups);
            }
        }

        return finalMarkups;
    }

    private IReadOnlyList<MarkupRecord> CreateLiveScheduleMarkups(LiveScheduleInstance instance)
    {
        NormalizeLiveScheduleInstance(instance);
        var table = GenerateLiveScheduleTable(instance.Kind);
        return _drawingAnnotationMarkupService.CreateScheduleTableMarkups(
            table,
            instance.Origin,
            instance.LayerId,
            instance.GroupId,
            instance.Id);
    }

    private void ApplyRenderedMarkupsToSheet(
        DrawingSheet sheet,
        IReadOnlyList<MarkupRecord> renderedMarkups,
        bool updateActiveCollections)
    {
        if (!updateActiveCollections)
        {
            sheet.Markups = renderedMarkups.ToList();
            UpdateSheetMarkupReviewContext(sheet);
            return;
        }

        var markupTool = MarkupTool;
        if (markupTool is null)
            return;

        var managedSelection = CaptureManagedStructuredSelection(markupTool.SelectedMarkup);

        Markups.Clear();
        ShadowTree.Clear();
        foreach (var markup in renderedMarkups)
        {
            Markups.Add(markup);
            ShadowTree.AddOrUpdate(markup);
        }

        if (managedSelection is not { } selection)
            return;

        var nextSelection = FindMatchingManagedStructuredMarkup(renderedMarkups, selection);
        markupTool.SelectedMarkup = nextSelection;
    }

    private static void SynchronizeLiveScheduleInstancesFromMarkups(
        DrawingSheet sheet,
        IEnumerable<MarkupRecord> markups,
        bool removeMissingInstances)
    {
        var markupsByInstanceId = markups
            .Select(markup => new { Markup = markup, HasInstance = TryGetLiveScheduleInstanceId(markup, out var instanceId), InstanceId = instanceId })
            .Where(entry => entry.HasInstance)
            .GroupBy(entry => entry.InstanceId, entry => entry.Markup, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        for (int index = sheet.LiveSchedules.Count - 1; index >= 0; index--)
        {
            var instance = sheet.LiveSchedules[index];
            if (!markupsByInstanceId.TryGetValue(instance.Id, out var instanceMarkups) || instanceMarkups.Count == 0)
            {
                if (removeMissingInstances)
                    sheet.LiveSchedules.RemoveAt(index);

                continue;
            }

            var groupId = instanceMarkups
                .Select(GetAnnotationGroupId)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(groupId))
                instance.GroupId = groupId;

            if (TryGetLiveScheduleOrigin(instanceMarkups, out var origin))
                instance.Origin = origin;
        }
    }

    private static void SynchronizeLiveTitleBlockInstancesFromMarkups(
        DrawingSheet sheet,
        IEnumerable<MarkupRecord> markups,
        bool removeMissingInstances)
    {
        var markupsByInstanceId = markups
            .Select(markup => new { Markup = markup, HasInstance = TryGetLiveTitleBlockInstanceId(markup, out var instanceId), InstanceId = instanceId })
            .Where(entry => entry.HasInstance)
            .GroupBy(entry => entry.InstanceId, entry => entry.Markup, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        for (int index = sheet.LiveTitleBlocks.Count - 1; index >= 0; index--)
        {
            var instance = sheet.LiveTitleBlocks[index];
            if (!markupsByInstanceId.TryGetValue(instance.Id, out var instanceMarkups) || instanceMarkups.Count == 0)
            {
                if (removeMissingInstances)
                    sheet.LiveTitleBlocks.RemoveAt(index);

                continue;
            }

            var groupId = instanceMarkups
                .Select(GetAnnotationGroupId)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(groupId))
                instance.GroupId = groupId;

            if (TryGetLiveTitleBlockOrigin(instanceMarkups, out var origin))
                instance.Origin = origin;
        }
    }

    private static bool TryGetLiveScheduleOrigin(
        IEnumerable<MarkupRecord> markups,
        out Point origin)
    {
        var border = markups.FirstOrDefault(markup =>
            markup.Type == MarkupType.Rectangle &&
            string.Equals(markup.Metadata.Subject, "Table Border", StringComparison.Ordinal));
        if (border != null)
        {
            origin = new Point(border.BoundingRect.X, border.BoundingRect.Y);
            return true;
        }

        var bounds = Rect.Empty;
        foreach (var markup in markups)
        {
            var markupBounds = markup.BoundingRect;
            if (markupBounds == Rect.Empty)
            {
                markup.UpdateBoundingRect();
                markupBounds = markup.BoundingRect;
            }

            bounds = bounds == Rect.Empty ? markupBounds : Rect.Union(bounds, markupBounds);
        }

        if (bounds == Rect.Empty)
        {
            origin = default;
            return false;
        }

        origin = new Point(bounds.X, bounds.Y);
        return true;
    }

    private static string? GetAnnotationGroupId(MarkupRecord markup)
    {
        return markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.AnnotationGroupIdField, out var groupId)
            ? groupId
            : null;
    }

    private static bool TryGetLiveTitleBlockOrigin(
        IEnumerable<MarkupRecord> markups,
        out Point origin)
    {
        var outerBorder = markups.FirstOrDefault(markup =>
            markup.Type == MarkupType.Rectangle &&
            string.Equals(markup.Metadata.Subject, "Sheet Border", StringComparison.Ordinal) &&
            string.Equals(markup.Metadata.Label, "Outer Border", StringComparison.Ordinal));
        if (outerBorder != null)
        {
            origin = new Point(outerBorder.BoundingRect.X, outerBorder.BoundingRect.Y);
            return true;
        }

        origin = default;
        return false;
    }

    private static bool IsLiveScheduleMarkup(MarkupRecord markup)
        => TryGetLiveScheduleInstanceId(markup, out _);

    private static bool IsLiveTitleBlockMarkup(MarkupRecord markup)
        => TryGetLiveTitleBlockInstanceId(markup, out _);

    private static bool TryGetLiveScheduleInstanceId(MarkupRecord markup, out string instanceId)
    {
        if (markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.LiveScheduleInstanceIdField, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            instanceId = value;
            return true;
        }

        instanceId = string.Empty;
        return false;
    }

    private static bool TryGetLiveTitleBlockInstanceId(MarkupRecord markup, out string instanceId)
    {
        if (markup.Metadata.CustomFields.TryGetValue(DrawingAnnotationMarkupService.LiveTitleBlockInstanceIdField, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            instanceId = value;
            return true;
        }

        instanceId = string.Empty;
        return false;
    }

    private static void NormalizeLiveScheduleInstance(LiveScheduleInstance instance)
    {
        if (string.IsNullOrWhiteSpace(instance.LayerId))
            instance.LayerId = DrawingAnnotationMarkupService.DefaultLayerId;

        if (string.IsNullOrWhiteSpace(instance.GroupId))
            instance.GroupId = Guid.NewGuid().ToString("N");
    }

    private static void NormalizeLiveTitleBlockInstance(LiveTitleBlockInstance instance)
    {
        if (string.IsNullOrWhiteSpace(instance.LayerId))
            instance.LayerId = DrawingAnnotationMarkupService.DefaultLayerId;

        if (string.IsNullOrWhiteSpace(instance.GroupId))
            instance.GroupId = Guid.NewGuid().ToString("N");

        instance.Template ??= new TitleBlockTemplate();
    }

    private static ManagedStructuredSelectionKey? CaptureManagedStructuredSelection(MarkupRecord? markup)
    {
        if (markup == null || !TryGetManagedStructuredInstanceId(markup, out var instanceId))
            return null;

        return new ManagedStructuredSelectionKey(
            instanceId,
            markup.Type,
            markup.Metadata.Subject ?? string.Empty,
            GetMarkupCustomField(markup, DrawingAnnotationMarkupService.AnnotationTextRoleField) ?? string.Empty,
            GetMarkupCustomField(markup, DrawingAnnotationMarkupService.AnnotationTextKeyField) ?? string.Empty,
            GetMarkupCustomFieldInt(markup, DrawingAnnotationMarkupService.AnnotationRowIndexField),
            GetMarkupCustomFieldInt(markup, DrawingAnnotationMarkupService.AnnotationColumnIndexField));
    }

    private static MarkupRecord? FindMatchingManagedStructuredMarkup(
        IEnumerable<MarkupRecord> markups,
        ManagedStructuredSelectionKey selection)
    {
        return markups.FirstOrDefault(markup =>
            TryGetManagedStructuredInstanceId(markup, out var instanceId) &&
            string.Equals(instanceId, selection.InstanceId, StringComparison.Ordinal) &&
            markup.Type == selection.Type &&
            string.Equals(markup.Metadata.Subject ?? string.Empty, selection.Subject, StringComparison.Ordinal) &&
            string.Equals(GetMarkupCustomField(markup, DrawingAnnotationMarkupService.AnnotationTextRoleField) ?? string.Empty, selection.TextRole, StringComparison.Ordinal) &&
            string.Equals(GetMarkupCustomField(markup, DrawingAnnotationMarkupService.AnnotationTextKeyField) ?? string.Empty, selection.TextKey, StringComparison.Ordinal) &&
            GetMarkupCustomFieldInt(markup, DrawingAnnotationMarkupService.AnnotationRowIndexField) == selection.RowIndex &&
            GetMarkupCustomFieldInt(markup, DrawingAnnotationMarkupService.AnnotationColumnIndexField) == selection.ColumnIndex)
            ?? markups.FirstOrDefault(markup =>
                TryGetManagedStructuredInstanceId(markup, out var instanceId) &&
                string.Equals(instanceId, selection.InstanceId, StringComparison.Ordinal));
    }

    private static bool TryGetManagedStructuredInstanceId(MarkupRecord markup, out string instanceId)
        => TryGetLiveScheduleInstanceId(markup, out instanceId) || TryGetLiveTitleBlockInstanceId(markup, out instanceId);

    private static string? GetMarkupCustomField(MarkupRecord markup, string key)
    {
        return markup.Metadata.CustomFields.TryGetValue(key, out var value)
            ? value
            : null;
    }

    private static int? GetMarkupCustomFieldInt(MarkupRecord markup, string key)
    {
        return markup.Metadata.CustomFields.TryGetValue(key, out var value) &&
               int.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    private readonly record struct ManagedStructuredSelectionKey(
        string InstanceId,
        MarkupType Type,
        string Subject,
        string TextRole,
        string TextKey,
        int? RowIndex,
        int? ColumnIndex);

    private void RefreshMarkupReviewContext()
    {
        foreach (var sheet in Sheets)
            UpdateSheetMarkupReviewContext(sheet);

        MarkupTool?.RefreshReviewContext();
    }

    private static void UpdateSheetMarkupReviewContext(DrawingSheet sheet)
    {
        foreach (var markup in sheet.Markups)
            markup.ReviewSheetDisplayText = sheet.DisplayName;
    }
}

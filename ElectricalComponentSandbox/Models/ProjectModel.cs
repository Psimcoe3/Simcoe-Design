namespace ElectricalComponentSandbox.Models;

using ElectricalComponentSandbox.Markup.Models;

/// <summary>
/// Represents the full project, containing all components, layers, markup annotations, and settings.
/// This is the top-level model serialized for project save/load.
/// </summary>
public class ProjectModel
{
    public string Name { get; set; } = "Untitled Project";
    public string Version { get; set; } = "1.0";

    public List<ElectricalComponent> Components { get; set; } = new();
    public List<Circuit> Circuits { get; set; } = new();
    public List<HomeRunWire> HomeRunWires { get; set; } = new();
    public List<DistributionSystemType> DistributionSystems { get; set; } = new();
    public List<DemandSchedule> DemandSchedules { get; set; } = new();
    public List<CircuitNamingScheme> CircuitNamingSchemes { get; set; } = new();
    public string? ActiveCircuitNamingSchemeId { get; set; }
    public List<ComponentFamily> ComponentFamilies { get; set; } = new();
    public List<Conduit.Core.Model.ConduitRun> ConduitRuns { get; set; } = new();
    public List<ElectricalCircuit> ElectricalCircuits { get; set; } = new();
    public ConduitFittingAngleSettings FittingAngleSettings { get; set; } = new();
    public List<LoadZone> LoadZones { get; set; } = new();
    public List<ProjectParameterDefinition> ProjectParameters { get; set; } = new();
    public List<Layer> Layers { get; set; } = new();

    /// <summary>All persisted sheets for this project.</summary>
    public List<DrawingSheet> Sheets { get; set; } = new();

    /// <summary>Selected sheet when the project was last saved.</summary>
    public string? ActiveSheetId { get; set; }

    /// <summary>Legacy single-sheet markup records retained for backward compatibility.</summary>
    public List<MarkupRecord> Markups { get; set; } = new();

    /// <summary>Legacy single-sheet PDF underlay retained for backward compatibility.</summary>
    public PdfUnderlay? PdfUnderlay { get; set; }

    /// <summary>Legacy single-sheet saved views retained for backward compatibility.</summary>
    public List<NamedView> NamedViews { get; set; } = new();

    /// <summary>Plot style tables (CTB) associated with this project</summary>
    public List<PlotStyleTable> PlotStyleTables { get; set; } = new();

    /// <summary>Legacy single-sheet layout retained for backward compatibility.</summary>
    public PlotLayout? PlotLayout { get; set; }

    /// <summary>Saved page setup presets for reusable AutoCAD-style layouts.</summary>
    public List<PlotLayout> SavedPageSetups { get; set; } = new();

    /// <summary>Published markup review sets captured from the current filtered review scope.</summary>
    public List<MarkupReviewSnapshot> MarkupReviewSnapshots { get; set; } = new();

    /// <summary>Unit system setting: "Imperial" (ft/in) or "Metric" (m/mm)</summary>
    public string UnitSystem { get; set; } = "Imperial";

    public double GridSize { get; set; } = 1.0;
    public bool ShowGrid { get; set; } = true;
    public bool SnapToGrid { get; set; } = true;
}

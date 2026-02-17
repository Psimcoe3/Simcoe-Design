namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Represents the full project, containing all components, layers, and settings.
/// This is the top-level model serialized for project save/load.
/// </summary>
public class ProjectModel
{
    public string Name { get; set; } = "Untitled Project";
    public string Version { get; set; } = "1.0";
    
    public List<ElectricalComponent> Components { get; set; } = new();
    public List<Layer> Layers { get; set; } = new();
    
    public PdfUnderlay? PdfUnderlay { get; set; }
    
    /// <summary>
    /// Unit system setting: "Imperial" (ft/in) or "Metric" (m/mm)
    /// </summary>
    public string UnitSystem { get; set; } = "Imperial";
    
    public double GridSize { get; set; } = 1.0;
    public bool ShowGrid { get; set; } = true;
    public bool SnapToGrid { get; set; } = true;
}

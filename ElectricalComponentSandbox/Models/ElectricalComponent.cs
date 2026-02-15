using System.Windows.Media.Media3D;
using Newtonsoft.Json;

namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Base class for all electrical components
/// </summary>
public abstract class ElectricalComponent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public ComponentType Type { get; set; }
    
    // Transformation properties
    public Point3D Position { get; set; }
    public Vector3D Rotation { get; set; }
    public Vector3D Scale { get; set; } = new Vector3D(1, 1, 1);
    
    // Parameters
    public ComponentParameters Parameters { get; set; } = new();
    
    // Constraints
    public List<string> Constraints { get; set; } = new();
}

public enum ComponentType
{
    Conduit,
    Box,
    Panel,
    Support
}

public class ComponentParameters
{
    public double Width { get; set; } = 1.0;
    public double Height { get; set; } = 1.0;
    public double Depth { get; set; } = 1.0;
    public string Material { get; set; } = "Steel";
    public double Elevation { get; set; } = 0.0;
    public string Color { get; set; } = "#808080";
}

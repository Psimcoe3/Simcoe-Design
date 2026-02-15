using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Models;
using Newtonsoft.Json;

namespace ElectricalComponentSandbox.Examples;

/// <summary>
/// Example component configurations for testing and demonstration
/// </summary>
public static class ComponentExamples
{
    /// <summary>
    /// Create a sample 2-inch EMT conduit run
    /// </summary>
    public static ConduitComponent CreateSampleConduit()
    {
        return new ConduitComponent
        {
            Name = "2-inch EMT Conduit",
            Diameter = 2.0,
            Length = 20.0,
            ConduitType = "EMT",
            Position = new Point3D(0, 0, 0),
            Parameters = new ComponentParameters
            {
                Material = "Steel",
                Color = "#A9A9A9",
                Elevation = 10.0
            }
        };
    }
    
    /// <summary>
    /// Create a sample 4x4 junction box
    /// </summary>
    public static BoxComponent CreateSampleBox()
    {
        return new BoxComponent
        {
            Name = "4x4 Junction Box",
            BoxType = "Junction Box",
            KnockoutCount = 8,
            Position = new Point3D(10, 0, 0),
            Parameters = new ComponentParameters
            {
                Width = 4.0,
                Height = 4.0,
                Depth = 2.0,
                Material = "Steel",
                Color = "#808080",
                Elevation = 10.0
            }
        };
    }
    
    /// <summary>
    /// Create a sample 200A electrical panel
    /// </summary>
    public static PanelComponent CreateSamplePanel()
    {
        return new PanelComponent
        {
            Name = "Main Distribution Panel",
            PanelType = "Distribution Panel",
            CircuitCount = 42,
            Amperage = 200.0,
            Position = new Point3D(0, 0, 10),
            Parameters = new ComponentParameters
            {
                Width = 20.0,
                Height = 30.0,
                Depth = 6.0,
                Material = "Steel",
                Color = "#696969",
                Elevation = 60.0
            }
        };
    }
    
    /// <summary>
    /// Create a sample support bracket
    /// </summary>
    public static SupportComponent CreateSampleSupport()
    {
        return new SupportComponent
        {
            Name = "Heavy Duty Support Bracket",
            SupportType = "Wall Bracket",
            LoadCapacity = 150.0,
            Position = new Point3D(5, 5, 5),
            Parameters = new ComponentParameters
            {
                Width = 3.0,
                Height = 3.0,
                Depth = 1.5,
                Material = "Galvanized Steel",
                Color = "#C0C0C0",
                Elevation = 10.0
            }
        };
    }
    
    /// <summary>
    /// Export a component to JSON string for demonstration
    /// </summary>
    public static string ExportToJson(ElectricalComponent component)
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Auto
        };
        return JsonConvert.SerializeObject(component, settings);
    }
}

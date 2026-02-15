using System.Windows.Media.Media3D;

namespace ElectricalComponentSandbox.Models;

public class ConduitComponent : ElectricalComponent
{
    public double Diameter { get; set; } = 0.5;
    public double Length { get; set; } = 10.0;
    public string ConduitType { get; set; } = "EMT";
    
    // Bend points for multi-segment conduits (relative to component position)
    public List<Point3D> BendPoints { get; set; } = new();
    
    // Bend radius for smooth transitions
    public double BendRadius { get; set; } = 1.0;
    
    // Bend type (90 or 45 degrees)
    public BendType BendType { get; set; } = BendType.Degree90;
    
    public ConduitComponent()
    {
        Type = ComponentType.Conduit;
        Name = "Conduit";
    }
    
    /// <summary>
    /// Gets all points in the conduit path, including start, bend points, and end
    /// </summary>
    public List<Point3D> GetPathPoints()
    {
        var points = new List<Point3D> { new Point3D(0, 0, 0) };
        points.AddRange(BendPoints);
        
        // If no bend points, add end point based on length
        if (BendPoints.Count == 0)
        {
            points.Add(new Point3D(0, 0, Length));
        }
        
        return points;
    }
}

public enum BendType
{
    Degree90,
    Degree45
}

using System.Windows.Media.Media3D;

namespace ElectricalComponentSandbox.Models;

public class CableTrayComponent : ElectricalComponent
{
    public double TrayWidth { get; set; } = 12.0;
    public double TrayDepth { get; set; } = 4.0;
    public double Length { get; set; } = 10.0;
    public string TrayType { get; set; } = "Ladder";
    
    /// <summary>
    /// Path points for multi-segment cable tray runs (relative to component position)
    /// </summary>
    public List<Point3D> PathPoints { get; set; } = new();
    
    public CableTrayComponent()
    {
        Type = ComponentType.CableTray;
        Name = "Cable Tray";
        Parameters.Width = 12.0;
        Parameters.Height = 4.0;
        Parameters.Depth = 10.0;
        Parameters.Color = "#C0C0C0";
    }
    
    /// <summary>
    /// Gets all points in the cable tray path, including start and end
    /// </summary>
    public List<Point3D> GetPathPoints()
    {
        var points = new List<Point3D> { new Point3D(0, 0, 0) };
        points.AddRange(PathPoints);
        
        if (PathPoints.Count == 0)
        {
            points.Add(new Point3D(Length, 0, 0));
        }
        
        return points;
    }
}

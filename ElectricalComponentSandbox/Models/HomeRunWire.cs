namespace ElectricalComponentSandbox.Models;

using System.Windows;

/// <summary>
/// Wiring style for a home-run wire annotation.
/// Maps to Revit's WiringType enum:
///   Arc     = WiringType.Arc     (concealed — curved representation)
///   Chamfer = WiringType.Chamfer (exposed — polyline)
///   Straight = direct 2-point line (no intermediate vertices)
/// </summary>
public enum WiringStyle
{
    Arc,
    Chamfer,
    Straight
}

/// <summary>
/// A 2D home-run wire annotation connecting a panel to a load device on the drawing canvas.
/// Analogous to Autodesk.Revit.DB.Electrical.Wire.
/// First vertex = panel end (tick marks placed here); last vertex = device end.
/// Stored as part of <see cref="ProjectModel.HomeRunWires"/>.
/// </summary>
public class HomeRunWire
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Circuit this wire annotates. Empty when not yet assigned to a circuit.</summary>
    public string CircuitId { get; set; } = string.Empty;

    /// <summary>Panel this wire originates from.</summary>
    public string PanelId { get; set; } = string.Empty;

    /// <summary>Shape style used by the renderer.</summary>
    public WiringStyle WiringStyle { get; set; } = WiringStyle.Chamfer;

    /// <summary>
    /// Ordered list of canvas-space vertices defining the wire path.
    /// Minimum 2 points (start and end). No two adjacent points may be coincident.
    /// </summary>
    public List<Point> Vertices { get; set; } = new();

    /// <summary>Number of hot (phase) conductors. Determines tick-mark count at the panel end.</summary>
    public int HotConductors { get; set; } = 2;

    /// <summary>Number of neutral conductors.</summary>
    public int NeutralConductors { get; set; } = 1;

    /// <summary>Number of ground conductors.</summary>
    public int GroundConductors { get; set; } = 1;
}

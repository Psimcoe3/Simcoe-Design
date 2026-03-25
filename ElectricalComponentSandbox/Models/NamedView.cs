using System.Windows.Media.Media3D;

namespace ElectricalComponentSandbox.Models;

/// <summary>
/// A saved camera / viewport state that the user can recall by name.
/// Stores both 2D pan/zoom transform and 3D camera state, plus layer-visibility overrides.
/// </summary>
public class NamedView
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "View";

    // 2D viewport transform
    public double PanX { get; set; }
    public double PanY { get; set; }
    public double Zoom { get; set; } = 1.0;

    // 3D camera state
    /// <summary>3D camera position (null = not captured)</summary>
    public Point3D? CameraPosition { get; set; }

    /// <summary>3D camera look direction</summary>
    public Vector3D? CameraLookDirection { get; set; }

    /// <summary>3D camera up direction</summary>
    public Vector3D? CameraUpDirection { get; set; }

    /// <summary>3D camera field of view (degrees)</summary>
    public double? CameraFieldOfView { get; set; }

    /// <summary>Whether this view was saved from the 3D viewport</summary>
    public bool Has3DCamera => CameraPosition.HasValue;

    /// <summary>Layer IDs that should be visible when this view is restored (null = use current)</summary>
    public List<string>? VisibleLayerIds { get; set; }
}

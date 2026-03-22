namespace ElectricalComponentSandbox.Models;

/// <summary>
/// A saved camera / viewport state that the user can recall by name.
/// Stores the 2D pan/zoom transform plus layer-visibility overrides.
/// </summary>
public class NamedView
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "View";

    // 2D viewport transform
    public double PanX { get; set; }
    public double PanY { get; set; }
    public double Zoom { get; set; } = 1.0;

    /// <summary>Layer IDs that should be visible when this view is restored (null = use current)</summary>
    public List<string>? VisibleLayerIds { get; set; }
}

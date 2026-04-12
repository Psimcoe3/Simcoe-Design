using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Models;

/// <summary>
/// An area-based preliminary load zone, analogous to Revit's
/// <c>ElectricalLoadArea</c> / <c>AreaBasedLoadData</c>.
/// The zone defines a closed polygon boundary with per-classification
/// load densities in W/ft².
/// </summary>
public class LoadZone
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Closed polygon boundary defined as ordered vertices (XYZ with Z = 0).
    /// The polygon is implicitly closed — the last point connects back to the first.
    /// </summary>
    public List<XYZ> BoundaryPoints { get; set; } = new();

    /// <summary>Building level / floor identifier.</summary>
    public string? Level { get; set; }

    /// <summary>Electrical phase identifier (e.g. "A" or "1").</summary>
    public string? Phase { get; set; }

    /// <summary>
    /// Load densities keyed by <see cref="LoadClassification"/> in W/ft².
    /// Multiple classifications can be present (e.g. Lighting + Power).
    /// </summary>
    public Dictionary<LoadClassification, double> LoadDensities { get; set; } = new();
}

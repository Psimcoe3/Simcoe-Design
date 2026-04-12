namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Project-level permitted conduit fitting bend angles, analogous to Revit's
/// <c>ElectricalSetting.GetSpecificFittingAngles()</c>.  The default set
/// covers NEC/UL standard bends: 22.5°, 30°, 45°, 60°, 90°.
/// </summary>
public class ConduitFittingAngleSettings
{
    /// <summary>
    /// Permitted bend angles in degrees, sorted ascending.
    /// </summary>
    public List<double> PermittedAngles { get; set; } = new() { 22.5, 30, 45, 60, 90 };

    /// <summary>
    /// Whether the angle enforcement is active.  When <c>false</c> any angle is accepted.
    /// </summary>
    public bool EnforceAngles { get; set; } = true;

    /// <summary>
    /// Returns the nearest permitted angle for a given input angle.
    /// If <see cref="EnforceAngles"/> is <c>false</c> or the list is empty,
    /// returns the input angle unchanged.
    /// </summary>
    public double SnapToNearest(double angleDegrees)
    {
        if (!EnforceAngles || PermittedAngles.Count == 0)
            return angleDegrees;

        double best = PermittedAngles[0];
        double bestDelta = Math.Abs(angleDegrees - best);

        for (int i = 1; i < PermittedAngles.Count; i++)
        {
            double delta = Math.Abs(angleDegrees - PermittedAngles[i]);
            if (delta < bestDelta)
            {
                best = PermittedAngles[i];
                bestDelta = delta;
            }
        }

        return best;
    }

    /// <summary>
    /// Returns <c>true</c> when the given angle exactly matches one of the
    /// <see cref="PermittedAngles"/> (within 0.001° tolerance).
    /// </summary>
    public bool IsPermitted(double angleDegrees)
    {
        if (!EnforceAngles || PermittedAngles.Count == 0)
            return true;

        foreach (var a in PermittedAngles)
        {
            if (Math.Abs(a - angleDegrees) < 0.001)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a default instance with the NEC/UL standard angle set.
    /// </summary>
    public static ConduitFittingAngleSettings CreateDefault() => new();

    /// <summary>
    /// Creates a custom angle set with enforcement enabled.
    /// </summary>
    public static ConduitFittingAngleSettings CreateCustom(IEnumerable<double> angles)
    {
        var settings = new ConduitFittingAngleSettings
        {
            PermittedAngles = angles.OrderBy(a => a).ToList()
        };
        return settings;
    }
}

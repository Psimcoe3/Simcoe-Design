namespace ElectricalComponentSandbox.Conduit.Core.Model;

/// <summary>
/// Fitting type used in routing preferences.
/// </summary>
public enum FittingType
{
    Elbow90,
    Elbow45,
    Coupling,
    Offset,
    Transition,
    Tee,
    Cross,
    Cap,
    Connector
}

/// <summary>
/// A routing preference rule that maps an angle range to a fitting type.
/// </summary>
public class RoutingPreferenceRule
{
    public double MinAngleDegrees { get; set; }
    public double MaxAngleDegrees { get; set; }
    public FittingType FittingType { get; set; }

    public bool Matches(double angleDegrees) =>
        angleDegrees >= MinAngleDegrees && angleDegrees <= MaxAngleDegrees;
}

/// <summary>
/// Defines a conduit type with name, standard, and routing preferences.
/// Analogous to Autodesk.Revit.DB.Electrical.ConduitType.
/// </summary>
public class ConduitType
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "EMT Conduit";
    public ConduitMaterialType Standard { get; set; } = ConduitMaterialType.EMT;

    /// <summary>Whether this type uses fittings at connections.</summary>
    public bool IsWithFitting { get; set; } = true;

    /// <summary>Allowed trade sizes for this type.</summary>
    public ConduitSizeSettings SizeSettings { get; set; } = ConduitSizeSettings.CreateDefaultEMT();

    /// <summary>Routing preference rules mapping angles to fitting types.</summary>
    public List<RoutingPreferenceRule> RoutingPreferences { get; set; } = new()
    {
        new() { MinAngleDegrees = 80, MaxAngleDegrees = 100, FittingType = FittingType.Elbow90 },
        new() { MinAngleDegrees = 35, MaxAngleDegrees = 55, FittingType = FittingType.Elbow45 },
        new() { MinAngleDegrees = 0, MaxAngleDegrees = 5, FittingType = FittingType.Coupling },
        new() { MinAngleDegrees = 170, MaxAngleDegrees = 180, FittingType = FittingType.Coupling }
    };

    /// <summary>
    /// Selects the appropriate fitting for a given connection angle.
    /// </summary>
    public FittingType? SelectFitting(double angleDegrees)
    {
        return RoutingPreferences
            .FirstOrDefault(r => r.Matches(angleDegrees))?.FittingType;
    }
}

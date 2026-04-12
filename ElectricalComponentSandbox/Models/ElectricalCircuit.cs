namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Determines how the circuit traces its path from panel to devices.
/// Mirrors Revit's <c>ElectricalCircuitPathMode</c>.
/// </summary>
public enum CircuitPathMode
{
    /// <summary>User-defined custom path with explicit connector ordering.</summary>
    Custom,
    /// <summary>Path runs to the farthest device first, then branches.</summary>
    FarthestDevice,
    /// <summary>Path includes all branch-connected device connectors.</summary>
    AllDevices
}

/// <summary>
/// A connector-based electrical circuit linking a panel connector to one or more
/// device connectors. Mirrors Revit's <c>ElectricalSystem</c> created via
/// <c>ElectricalSystem.Create(doc, connector, ElectricalSystemType)</c>.
/// </summary>
public class ElectricalCircuit
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>System type this circuit services (power, data, fire alarm, etc.).</summary>
    public ElectricalSystemType SystemType { get; set; } = ElectricalSystemType.PowerCircuit;

    /// <summary>
    /// Ordered list of connector IDs that form this circuit.
    /// The first entry is the panel (source) connector; subsequent entries are device connectors.
    /// </summary>
    public List<string> ConnectorIds { get; set; } = new();

    /// <summary>Path mode controlling how the circuit traces its route.</summary>
    public CircuitPathMode PathMode { get; set; } = CircuitPathMode.AllDevices;

    /// <summary>
    /// Optional reference to the legacy <see cref="Circuit"/> schedule entry
    /// (for schedule column data like breaker, wire, load).
    /// </summary>
    public string? ScheduleCircuitId { get; set; }

    /// <summary>The panel (source) connector ID, or null if the circuit has no connectors.</summary>
    public string? PanelConnectorId => ConnectorIds.Count > 0 ? ConnectorIds[0] : null;

    /// <summary>All device connector IDs (everything except the panel connector).</summary>
    public IReadOnlyList<string> DeviceConnectorIds =>
        ConnectorIds.Count > 1 ? ConnectorIds.Skip(1).ToList() : [];
}

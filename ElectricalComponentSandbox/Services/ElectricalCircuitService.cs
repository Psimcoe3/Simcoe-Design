using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Service for creating and managing connector-based electrical circuits.
/// Mirrors Revit's <c>ElectricalSystem.Create</c> API and circuit editing operations.
/// </summary>
public static class ElectricalCircuitService
{
    /// <summary>
    /// Creates a new <see cref="ElectricalCircuit"/> from a panel connector and a device connector.
    /// Both connectors are stamped with the new circuit's ID.
    /// </summary>
    /// <param name="panelConnector">Source (panel-side) connector.</param>
    /// <param name="deviceConnector">First device connector to wire.</param>
    /// <param name="systemType">Electrical system type for the circuit.</param>
    /// <returns>The newly created circuit.</returns>
    public static ElectricalCircuit Create(
        ElectricalConnector panelConnector,
        ElectricalConnector deviceConnector,
        ElectricalSystemType systemType)
    {
        ArgumentNullException.ThrowIfNull(panelConnector);
        ArgumentNullException.ThrowIfNull(deviceConnector);

        if (panelConnector.IsConnected)
            throw new InvalidOperationException(
                $"Panel connector '{panelConnector.Id}' is already wired to circuit '{panelConnector.CircuitId}'.");

        if (deviceConnector.IsConnected)
            throw new InvalidOperationException(
                $"Device connector '{deviceConnector.Id}' is already wired to circuit '{deviceConnector.CircuitId}'.");

        var circuit = new ElectricalCircuit
        {
            SystemType = systemType,
            ConnectorIds = { panelConnector.Id, deviceConnector.Id }
        };

        panelConnector.CircuitId = circuit.Id;
        deviceConnector.CircuitId = circuit.Id;

        return circuit;
    }

    /// <summary>
    /// Creates a circuit from a panel connector and multiple device connectors.
    /// </summary>
    public static ElectricalCircuit Create(
        ElectricalConnector panelConnector,
        IReadOnlyList<ElectricalConnector> deviceConnectors,
        ElectricalSystemType systemType)
    {
        ArgumentNullException.ThrowIfNull(panelConnector);
        ArgumentNullException.ThrowIfNull(deviceConnectors);

        if (deviceConnectors.Count == 0)
            throw new ArgumentException("At least one device connector is required.", nameof(deviceConnectors));

        if (panelConnector.IsConnected)
            throw new InvalidOperationException(
                $"Panel connector '{panelConnector.Id}' is already wired to circuit '{panelConnector.CircuitId}'.");

        var circuit = new ElectricalCircuit
        {
            SystemType = systemType,
            ConnectorIds = { panelConnector.Id }
        };

        panelConnector.CircuitId = circuit.Id;

        foreach (var dc in deviceConnectors)
        {
            if (dc.IsConnected)
                throw new InvalidOperationException(
                    $"Device connector '{dc.Id}' is already wired to circuit '{dc.CircuitId}'.");

            circuit.ConnectorIds.Add(dc.Id);
            dc.CircuitId = circuit.Id;
        }

        return circuit;
    }

    /// <summary>
    /// Adds a device connector to an existing circuit.
    /// </summary>
    public static void AddDevice(
        ElectricalCircuit circuit,
        ElectricalConnector deviceConnector)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(deviceConnector);

        if (deviceConnector.IsConnected)
            throw new InvalidOperationException(
                $"Device connector '{deviceConnector.Id}' is already wired to circuit '{deviceConnector.CircuitId}'.");

        circuit.ConnectorIds.Add(deviceConnector.Id);
        deviceConnector.CircuitId = circuit.Id;
    }

    /// <summary>
    /// Removes a device connector from a circuit. The panel connector (index 0) cannot be removed.
    /// </summary>
    public static void RemoveDevice(
        ElectricalCircuit circuit,
        ElectricalConnector deviceConnector)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(deviceConnector);

        if (circuit.ConnectorIds.Count > 0 && circuit.ConnectorIds[0] == deviceConnector.Id)
            throw new InvalidOperationException(
                "Cannot remove the panel (source) connector. Delete the circuit instead.");

        if (!circuit.ConnectorIds.Remove(deviceConnector.Id))
            throw new ArgumentException(
                $"Connector '{deviceConnector.Id}' is not part of circuit '{circuit.Id}'.",
                nameof(deviceConnector));

        deviceConnector.CircuitId = null;
    }

    /// <summary>
    /// Finds all connectors across all components that are not wired to any circuit (orphans).
    /// </summary>
    public static IReadOnlyList<ElectricalConnector> GetOrphanConnectors(
        IEnumerable<ElectricalComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        return components
            .Where(c => c.ElectricalConnectors != null)
            .SelectMany(c => c.ElectricalConnectors!.GetUnconnected())
            .ToList();
    }

    /// <summary>
    /// Resolves the ordered list of connectors for a circuit from the full component set.
    /// Returns connectors in circuit wire-order (panel first, then devices).
    /// </summary>
    public static IReadOnlyList<ElectricalConnector> ResolveConnectors(
        ElectricalCircuit circuit,
        IEnumerable<ElectricalComponent> components)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(components);

        var allConnectors = components
            .Where(c => c.ElectricalConnectors != null)
            .SelectMany(c => c.ElectricalConnectors!.Connectors)
            .ToDictionary(c => c.Id);

        var result = new List<ElectricalConnector>();
        foreach (var id in circuit.ConnectorIds)
        {
            if (allConnectors.TryGetValue(id, out var connector))
                result.Add(connector);
        }
        return result;
    }
}

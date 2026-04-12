namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Type of electrical connector port — mirrors Revit's <c>ConnectorType</c>.
/// </summary>
public enum ElectricalConnectorType
{
    /// <summary>Endpoint connector (receptacle, switch, device terminal).</summary>
    End,
    /// <summary>Curve/pass-through connector (wire run, bus tap).</summary>
    Curve
}

/// <summary>
/// A live electrical connector on a placed component instance.
/// Created from a <see cref="ConnectorDefinition"/> template when a component is placed,
/// and participates in <see cref="ElectricalCircuit"/> wiring.
/// Mirrors Revit's <c>Connector</c> (MEP domain).
/// </summary>
public class ElectricalConnector
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Owning component instance.</summary>
    public string ComponentId { get; set; } = string.Empty;

    /// <summary>Port name matching the <see cref="ConnectorDefinition.Name"/> from the family type.</summary>
    public string PortName { get; set; } = string.Empty;

    /// <summary>Electrical system type this connector supports.</summary>
    public ElectricalSystemType SystemType { get; set; } = ElectricalSystemType.PowerCircuit;

    /// <summary>Nominal voltage at this connector.</summary>
    public double Voltage { get; set; }

    /// <summary>Phase assignment ("A", "B", "C", or "ABC").</summary>
    public string Phase { get; set; } = "A";

    /// <summary>Whether this is an endpoint or pass-through connector.</summary>
    public ElectricalConnectorType ConnectorType { get; set; } = ElectricalConnectorType.End;

    /// <summary>Connector domain (electrical, conduit, cable tray).</summary>
    public ConnectorDomain Domain { get; set; } = ConnectorDomain.Electrical;

    /// <summary>Flow direction through this connector.</summary>
    public ConnectorFlow Flow { get; set; } = ConnectorFlow.Bidirectional;

    /// <summary>
    /// ID of the <see cref="ElectricalCircuit"/> this connector participates in,
    /// or <c>null</c> if unconnected.
    /// </summary>
    public string? CircuitId { get; set; }

    /// <summary>True when this connector is wired to an <see cref="ElectricalCircuit"/>.</summary>
    public bool IsConnected => CircuitId != null;
}

/// <summary>
/// Manages electrical connectors for a placed component instance.
/// Mirrors Revit's <c>MEPModel.ConnectorManager</c>.
/// </summary>
public class ElectricalConnectorManager
{
    public List<ElectricalConnector> Connectors { get; set; } = new();

    /// <summary>Returns all connectors not yet wired to a circuit.</summary>
    public IReadOnlyList<ElectricalConnector> GetUnconnected()
    {
        return Connectors.Where(c => !c.IsConnected).ToList();
    }

    /// <summary>Finds a connector by its port name (case-insensitive).</summary>
    public ElectricalConnector? FindConnector(string portName)
    {
        return Connectors.FirstOrDefault(
            c => string.Equals(c.PortName, portName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Finds a connector by its ID.</summary>
    public ElectricalConnector? GetConnector(string id)
    {
        return Connectors.FirstOrDefault(c => c.Id == id);
    }

    /// <summary>Returns all connectors matching the given system type.</summary>
    public IReadOnlyList<ElectricalConnector> GetBySystemType(ElectricalSystemType systemType)
    {
        return Connectors.Where(c => c.SystemType == systemType).ToList();
    }
}

using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

public enum ElectricalCircuitValidationSeverity
{
    Error,
    Warning,
    Info
}

public enum ElectricalCircuitValidationCategory
{
    Topology,
    MissingConnector,
    DuplicateConnector,
    SourceEquipment,
    ConnectorAssignment,
    ConnectorDomain,
    SystemType,
    Voltage,
    Phase
}

public record ElectricalCircuitValidationFinding
{
    public string CircuitId { get; init; } = string.Empty;
    public string? ConnectorId { get; init; }
    public string? ComponentId { get; init; }
    public ElectricalCircuitValidationCategory Category { get; init; }
    public ElectricalCircuitValidationSeverity Severity { get; init; } = ElectricalCircuitValidationSeverity.Error;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

internal sealed record ElectricalConnectorContext(
    ElectricalConnector Connector,
    ElectricalComponent Component);

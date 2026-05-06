using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

public static partial class ElectricalCircuitService
{
    /// <summary>
    /// Preflights connector compatibility before creating a circuit. Callers can use
    /// the returned findings to show a non-destructive compatibility preview.
    /// </summary>
    public static IReadOnlyList<ElectricalCircuitValidationFinding> ValidateNewCircuitConnection(
        ElectricalConnector panelConnector,
        IReadOnlyList<ElectricalConnector> deviceConnectors,
        ElectricalSystemType systemType)
    {
        ArgumentNullException.ThrowIfNull(panelConnector);
        ArgumentNullException.ThrowIfNull(deviceConnectors);

        var findings = new List<ElectricalCircuitValidationFinding>();

        if (deviceConnectors.Count == 0)
        {
            findings.Add(Error(
                string.Empty,
                ElectricalCircuitValidationCategory.Topology,
                "Circuit has no device connectors",
                "At least one device connector is required before creating a circuit."));
            return findings;
        }

        ValidateConnectorCandidate(panelConnector, systemType, findings, isSource: true);
        foreach (var deviceConnector in deviceConnectors)
            ValidateConnectorCandidate(deviceConnector, systemType, findings, isSource: false);

        ValidateConnectorIdentitySet(panelConnector, deviceConnectors, findings);
        ValidateCandidateVoltageAndPhase(panelConnector, deviceConnectors, systemType, findings);

        return findings;
    }

    /// <summary>
    /// Preflights whether a connector can be appended to an existing circuit.
    /// This overload validates connector-level compatibility that does not require
    /// resolving the original source connector from the project model.
    /// </summary>
    public static IReadOnlyList<ElectricalCircuitValidationFinding> ValidateDeviceForCircuit(
        ElectricalCircuit circuit,
        ElectricalConnector deviceConnector)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(deviceConnector);

        var findings = new List<ElectricalCircuitValidationFinding>();

        if (circuit.ConnectorIds.Contains(deviceConnector.Id, StringComparer.Ordinal))
        {
            findings.Add(Error(
                circuit.Id,
                ElectricalCircuitValidationCategory.DuplicateConnector,
                "Duplicate connector in circuit",
                $"Connector '{deviceConnector.Id}' is already part of circuit '{circuit.Id}'.",
                deviceConnector.Id,
                deviceConnector.ComponentId));
        }

        ValidateConnectorCandidate(deviceConnector, circuit.SystemType, findings, isSource: false, circuit.Id);
        return findings;
    }

    private static void ValidateConnectorCandidate(
        ElectricalConnector connector,
        ElectricalSystemType systemType,
        List<ElectricalCircuitValidationFinding> findings,
        bool isSource,
        string circuitId = "")
    {
        var role = isSource ? "Source" : "Device";

        AddMissingIdFinding(connector, findings, role, circuitId);
        AddAssignedConnectorFinding(connector, findings, role, circuitId);
        AddDomainFinding(connector, findings, role, circuitId);
        AddSystemTypeFinding(connector, findings, role, circuitId, systemType);
    }

    private static void AddMissingIdFinding(
        ElectricalConnector connector,
        List<ElectricalCircuitValidationFinding> findings,
        string role,
        string circuitId)
    {
        if (!string.IsNullOrWhiteSpace(connector.Id)) return;

        findings.Add(Error(
            circuitId,
            ElectricalCircuitValidationCategory.MissingConnector,
            $"{role} connector has no ID",
            $"{role} connector must have a stable ID before circuit creation.",
            connector.Id,
            connector.ComponentId));
    }

    private static void AddAssignedConnectorFinding(
        ElectricalConnector connector,
        List<ElectricalCircuitValidationFinding> findings,
        string role,
        string circuitId)
    {
        if (!connector.IsConnected) return;

        findings.Add(Error(
            circuitId,
            ElectricalCircuitValidationCategory.ConnectorAssignment,
            $"{role} connector is already assigned",
            $"Connector '{connector.Id}' is already wired to circuit '{connector.CircuitId}'.",
            connector.Id,
            connector.ComponentId));
    }

    private static void AddDomainFinding(
        ElectricalConnector connector,
        List<ElectricalCircuitValidationFinding> findings,
        string role,
        string circuitId)
    {
        if (connector.Domain == ConnectorDomain.Electrical) return;

        findings.Add(Error(
            circuitId,
            ElectricalCircuitValidationCategory.ConnectorDomain,
            $"{role} connector domain mismatch",
            $"Connector '{connector.Id}' is a {connector.Domain} connector, not an Electrical connector.",
            connector.Id,
            connector.ComponentId));
    }

    private static void AddSystemTypeFinding(
        ElectricalConnector connector,
        List<ElectricalCircuitValidationFinding> findings,
        string role,
        string circuitId,
        ElectricalSystemType systemType)
    {
        if (connector.SystemType == systemType) return;

        findings.Add(Error(
            circuitId,
            ElectricalCircuitValidationCategory.SystemType,
            $"{role} connector system type mismatch",
            $"Connector '{connector.Id}' is {connector.SystemType}, but the requested circuit type is {systemType}.",
            connector.Id,
            connector.ComponentId));
    }

    private static void ValidateConnectorIdentitySet(
        ElectricalConnector panelConnector,
        IReadOnlyList<ElectricalConnector> deviceConnectors,
        List<ElectricalCircuitValidationFinding> findings)
    {
        var allConnectors = new[] { panelConnector }
            .Concat(deviceConnectors)
            .Where(connector => !string.IsNullOrWhiteSpace(connector.Id));

        foreach (var duplicateGroup in allConnectors
            .GroupBy(connector => connector.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            findings.Add(Error(
                string.Empty,
                ElectricalCircuitValidationCategory.DuplicateConnector,
                "Duplicate connector in circuit request",
                $"Connector '{duplicateGroup.Key}' appears more than once in the requested circuit.",
                duplicateGroup.Key));
        }
    }

    private static void ValidateCandidateVoltageAndPhase(
        ElectricalConnector panelConnector,
        IReadOnlyList<ElectricalConnector> deviceConnectors,
        ElectricalSystemType systemType,
        List<ElectricalCircuitValidationFinding> findings)
    {
        var sourcePhase = ParsePhaseSet(panelConnector.Phase);
        ValidateCandidatePhaseText(panelConnector, sourcePhase, findings);

        foreach (var deviceConnector in deviceConnectors)
        {
            ValidateCandidateVoltage(panelConnector, deviceConnector, findings);
            var devicePhase = ParsePhaseSet(deviceConnector.Phase);
            ValidateCandidatePhaseText(deviceConnector, devicePhase, findings);
            ValidateCandidatePhaseCompatibility(
                panelConnector,
                deviceConnector,
                sourcePhase,
                devicePhase,
                systemType,
                findings);
        }
    }

    private static void ValidateCandidateVoltage(
        ElectricalConnector panelConnector,
        ElectricalConnector deviceConnector,
        List<ElectricalCircuitValidationFinding> findings)
    {
        if (panelConnector.Voltage <= 0 || deviceConnector.Voltage <= 0) return;
        if (Math.Abs(panelConnector.Voltage - deviceConnector.Voltage) <= 0.01) return;

        findings.Add(Error(
            string.Empty,
            ElectricalCircuitValidationCategory.Voltage,
            "Connector voltage mismatch",
            $"Connector '{deviceConnector.Id}' is {deviceConnector.Voltage:g} V, but source connector '{panelConnector.Id}' is {panelConnector.Voltage:g} V.",
            deviceConnector.Id,
            deviceConnector.ComponentId));
    }

    private static void ValidateCandidatePhaseCompatibility(
        ElectricalConnector panelConnector,
        ElectricalConnector deviceConnector,
        ParsedPhase sourcePhase,
        ParsedPhase devicePhase,
        ElectricalSystemType systemType,
        List<ElectricalCircuitValidationFinding> findings)
    {
        if (!ShouldValidatePowerPhaseCompatibility(sourcePhase, devicePhase, systemType)) return;
        if (devicePhase.Values.IsSubsetOf(sourcePhase.Values)) return;

        findings.Add(Error(
            string.Empty,
            ElectricalCircuitValidationCategory.Phase,
            "Connector phase mismatch",
            $"Connector '{deviceConnector.Id}' phase '{deviceConnector.Phase}' is not compatible with source phase '{panelConnector.Phase}'.",
            deviceConnector.Id,
            deviceConnector.ComponentId));
    }

    private static void ValidateCandidatePhaseText(
        ElectricalConnector connector,
        ParsedPhase phase,
        List<ElectricalCircuitValidationFinding> findings)
    {
        if (phase.IsValid) return;

        findings.Add(Error(
            string.Empty,
            ElectricalCircuitValidationCategory.Phase,
            "Invalid connector phase",
            $"Connector '{connector.Id}' has invalid phase text '{connector.Phase}'. Use A, B, C, AB, BC, AC, or ABC.",
            connector.Id,
            connector.ComponentId));
    }

    private static bool ShouldValidatePowerPhaseCompatibility(
        ParsedPhase sourcePhase,
        ParsedPhase devicePhase,
        ElectricalSystemType systemType)
    {
        return systemType == ElectricalSystemType.PowerCircuit &&
            sourcePhase.IsValid &&
            devicePhase.IsValid &&
            sourcePhase.Values.Count > 0 &&
            devicePhase.Values.Count > 0;
    }

    private static ParsedPhase ParsePhaseSet(string? phase)
    {
        if (string.IsNullOrWhiteSpace(phase))
            return new ParsedPhase(true, new HashSet<char>());

        var values = new HashSet<char>();
        foreach (var ch in phase.Trim().ToUpperInvariant())
        {
            if (ch is not ('A' or 'B' or 'C'))
                return new ParsedPhase(false, values);

            values.Add(ch);
        }

        return new ParsedPhase(values.Count > 0, values);
    }

    private static ElectricalCircuitValidationFinding Error(
        string circuitId,
        ElectricalCircuitValidationCategory category,
        string title,
        string description,
        string? connectorId = null,
        string? componentId = null)
    {
        return new ElectricalCircuitValidationFinding
        {
            CircuitId = circuitId,
            ConnectorId = connectorId,
            ComponentId = componentId,
            Category = category,
            Severity = ElectricalCircuitValidationSeverity.Error,
            Title = title,
            Description = description
        };
    }

    private static void ThrowIfInvalidConnection(
        IEnumerable<ElectricalCircuitValidationFinding> findings)
    {
        var errors = findings
            .Where(f => f.Severity == ElectricalCircuitValidationSeverity.Error)
            .ToList();
        if (errors.Count == 0) return;

        var summary = string.Join("; ", errors.Select(f => f.Description));
        throw new InvalidOperationException(summary);
    }

    private sealed record ParsedPhase(bool IsValid, HashSet<char> Values);
}

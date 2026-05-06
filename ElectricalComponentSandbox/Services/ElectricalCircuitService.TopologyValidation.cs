using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

public static partial class ElectricalCircuitService
{
    /// <summary>
    /// Validates a connector-based circuit against the placed component graph.
    /// This is the first production-grade circuit topology guardrail: it catches
    /// missing connector references, connector assignment drift, domain/system
    /// mismatches, voltage mismatches, and incompatible power phases.
    /// </summary>
    public static IReadOnlyList<ElectricalCircuitValidationFinding> ValidateCircuit(
        ElectricalCircuit circuit,
        IEnumerable<ElectricalComponent> components)
    {
        ArgumentNullException.ThrowIfNull(circuit);
        ArgumentNullException.ThrowIfNull(components);

        var findings = new List<ElectricalCircuitValidationFinding>();
        var connectorLookup = BuildConnectorLookup(components, findings, circuit.Id);

        ValidateCircuitShape(circuit, findings);

        var resolved = new List<ElectricalConnectorContext>();
        foreach (var connectorId in circuit.ConnectorIds)
            ResolveCircuitConnector(circuit, connectorLookup, connectorId, findings, resolved);

        ValidateSourceConnector(circuit, resolved.FirstOrDefault(), findings);
        ValidateVoltageAndPhase(circuit, resolved, findings);

        return findings;
    }

    /// <summary>
    /// Validates all connector-based circuits and adds cross-circuit checks such as
    /// duplicate connector ownership across multiple circuits.
    /// </summary>
    public static IReadOnlyList<ElectricalCircuitValidationFinding> ValidateCircuitSet(
        IEnumerable<ElectricalCircuit> circuits,
        IEnumerable<ElectricalComponent> components)
    {
        ArgumentNullException.ThrowIfNull(circuits);
        ArgumentNullException.ThrowIfNull(components);

        var circuitList = circuits.ToList();
        var componentList = components.ToList();
        var findings = new List<ElectricalCircuitValidationFinding>();

        foreach (var circuit in circuitList)
            findings.AddRange(ValidateCircuit(circuit, componentList));

        AddSharedConnectorFindings(circuitList, findings);
        return findings;
    }

    private static void ResolveCircuitConnector(
        ElectricalCircuit circuit,
        Dictionary<string, ElectricalConnectorContext> connectorLookup,
        string connectorId,
        List<ElectricalCircuitValidationFinding> findings,
        List<ElectricalConnectorContext> resolved)
    {
        if (string.IsNullOrWhiteSpace(connectorId))
        {
            findings.Add(Error(
                circuit.Id,
                ElectricalCircuitValidationCategory.MissingConnector,
                "Blank connector reference",
                "Circuit contains a blank connector ID."));
            return;
        }

        if (!connectorLookup.TryGetValue(connectorId, out var context))
        {
            findings.Add(Error(
                circuit.Id,
                ElectricalCircuitValidationCategory.MissingConnector,
                "Missing connector reference",
                $"Connector '{connectorId}' referenced by circuit '{circuit.Id}' was not found in project components.",
                connectorId));
            return;
        }

        resolved.Add(context);
        ValidateConnectorContext(circuit, context, findings);
    }

    private static void AddSharedConnectorFindings(
        IReadOnlyList<ElectricalCircuit> circuitList,
        List<ElectricalCircuitValidationFinding> findings)
    {
        var sharedConnectorGroups = circuitList
            .SelectMany(c => c.ConnectorIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Select(id => (Circuit: c, ConnectorId: id)))
            .GroupBy(x => x.ConnectorId, StringComparer.Ordinal)
            .Where(g => g.Select(x => x.Circuit.Id).Distinct(StringComparer.Ordinal).Count() > 1);

        foreach (var group in sharedConnectorGroups)
        {
            var circuitIds = string.Join(", ", group.Select(x => x.Circuit.Id).Distinct(StringComparer.Ordinal));
            foreach (var item in group)
            {
                findings.Add(Error(
                    item.Circuit.Id,
                    ElectricalCircuitValidationCategory.DuplicateConnector,
                    "Connector assigned to multiple circuits",
                    $"Connector '{group.Key}' is referenced by multiple circuits: {circuitIds}.",
                    group.Key));
            }
        }
    }

    private static Dictionary<string, ElectricalConnectorContext> BuildConnectorLookup(
        IEnumerable<ElectricalComponent> components,
        List<ElectricalCircuitValidationFinding> findings,
        string circuitId)
    {
        var connectorContexts = components
            .Where(c => c.ElectricalConnectors != null)
            .SelectMany(c => c.ElectricalConnectors!.Connectors.Select(connector => new ElectricalConnectorContext(connector, c)))
            .ToList();

        foreach (var duplicateGroup in connectorContexts
            .Where(c => !string.IsNullOrWhiteSpace(c.Connector.Id))
            .GroupBy(c => c.Connector.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1))
        {
            findings.Add(Error(
                circuitId,
                ElectricalCircuitValidationCategory.DuplicateConnector,
                "Duplicate connector ID in project",
                $"Connector ID '{duplicateGroup.Key}' appears on multiple component instances.",
                duplicateGroup.Key));
        }

        return connectorContexts
            .Where(c => !string.IsNullOrWhiteSpace(c.Connector.Id))
            .GroupBy(c => c.Connector.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    private static void ValidateCircuitShape(
        ElectricalCircuit circuit,
        List<ElectricalCircuitValidationFinding> findings)
    {
        AddMissingCircuitEndpointFindings(circuit, findings);
        AddDuplicateCircuitConnectorFindings(circuit, findings);
    }

    private static void AddMissingCircuitEndpointFindings(
        ElectricalCircuit circuit,
        List<ElectricalCircuitValidationFinding> findings)
    {
        if (circuit.ConnectorIds.Count == 0)
        {
            findings.Add(Error(
                circuit.Id,
                ElectricalCircuitValidationCategory.Topology,
                "Circuit has no source connector",
                "Circuit must reference a source connector followed by at least one device connector."));
        }
        else if (circuit.ConnectorIds.Count == 1)
        {
            findings.Add(Error(
                circuit.Id,
                ElectricalCircuitValidationCategory.Topology,
                "Circuit has no device connectors",
                "Circuit must include at least one downstream device connector."));
        }
    }

    private static void AddDuplicateCircuitConnectorFindings(
        ElectricalCircuit circuit,
        List<ElectricalCircuitValidationFinding> findings)
    {
        foreach (var duplicateId in circuit.ConnectorIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key))
        {
            findings.Add(Error(
                circuit.Id,
                ElectricalCircuitValidationCategory.DuplicateConnector,
                "Duplicate connector in circuit",
                $"Connector '{duplicateId}' appears more than once in circuit '{circuit.Id}'.",
                duplicateId));
        }
    }

    private static void ValidateConnectorContext(
        ElectricalCircuit circuit,
        ElectricalConnectorContext context,
        List<ElectricalCircuitValidationFinding> findings)
    {
        AddConnectorAssignmentFinding(circuit, context, findings);
        AddCircuitDomainFinding(circuit, context, findings);
        AddCircuitSystemTypeFinding(circuit, context, findings);
    }

    private static void AddConnectorAssignmentFinding(
        ElectricalCircuit circuit,
        ElectricalConnectorContext context,
        List<ElectricalCircuitValidationFinding> findings)
    {
        var connector = context.Connector;
        if (!string.IsNullOrWhiteSpace(connector.CircuitId) &&
            !string.Equals(connector.CircuitId, circuit.Id, StringComparison.Ordinal))
        {
            findings.Add(Error(
                circuit.Id,
                ElectricalCircuitValidationCategory.ConnectorAssignment,
                "Connector assigned to another circuit",
                $"Connector '{connector.Id}' is stamped with circuit '{connector.CircuitId}' instead of '{circuit.Id}'.",
                connector.Id,
                context.Component.Id));
        }
        else if (string.IsNullOrWhiteSpace(connector.CircuitId))
        {
            findings.Add(Error(
                circuit.Id,
                ElectricalCircuitValidationCategory.ConnectorAssignment,
                "Connector is not stamped with circuit ID",
                $"Connector '{connector.Id}' is referenced by circuit '{circuit.Id}' but its CircuitId is blank.",
                connector.Id,
                context.Component.Id));
        }
    }

    private static void AddCircuitDomainFinding(
        ElectricalCircuit circuit,
        ElectricalConnectorContext context,
        List<ElectricalCircuitValidationFinding> findings)
    {
        var connector = context.Connector;
        if (connector.Domain == ConnectorDomain.Electrical) return;

        findings.Add(Error(
            circuit.Id,
            ElectricalCircuitValidationCategory.ConnectorDomain,
            "Connector domain mismatch",
            $"Connector '{connector.Id}' is a {connector.Domain} connector, not an Electrical connector.",
            connector.Id,
            context.Component.Id));
    }

    private static void AddCircuitSystemTypeFinding(
        ElectricalCircuit circuit,
        ElectricalConnectorContext context,
        List<ElectricalCircuitValidationFinding> findings)
    {
        var connector = context.Connector;
        if (connector.SystemType == circuit.SystemType) return;

        findings.Add(Error(
            circuit.Id,
            ElectricalCircuitValidationCategory.SystemType,
            "Connector system type mismatch",
            $"Connector '{connector.Id}' is {connector.SystemType}, but circuit '{circuit.Id}' is {circuit.SystemType}.",
            connector.Id,
            context.Component.Id));
    }

    private static void ValidateSourceConnector(
        ElectricalCircuit circuit,
        ElectricalConnectorContext? source,
        List<ElectricalCircuitValidationFinding> findings)
    {
        if (source == null) return;

        if (source.Component.Type is not (ComponentType.Panel or ComponentType.PowerSource or ComponentType.TransferSwitch or ComponentType.Bus))
        {
            findings.Add(new ElectricalCircuitValidationFinding
            {
                CircuitId = circuit.Id,
                ConnectorId = source.Connector.Id,
                ComponentId = source.Component.Id,
                Category = ElectricalCircuitValidationCategory.SourceEquipment,
                Severity = ElectricalCircuitValidationSeverity.Warning,
                Title = "Circuit source is not distribution equipment",
                Description = $"First connector belongs to '{source.Component.Name}' ({source.Component.Type}); source should be panel, power source, transfer switch, or bus equipment."
            });
        }
    }

    private static void ValidateVoltageAndPhase(
        ElectricalCircuit circuit,
        IReadOnlyList<ElectricalConnectorContext> resolved,
        List<ElectricalCircuitValidationFinding> findings)
    {
        if (resolved.Count == 0) return;

        var source = resolved[0];
        var sourcePhase = ParsePhaseSet(source.Connector.Phase);
        ValidatePhaseText(circuit, source, sourcePhase, findings);

        foreach (var context in resolved.Skip(1))
        {
            ValidateResolvedVoltage(circuit, source, context, findings);
            var devicePhase = ParsePhaseSet(context.Connector.Phase);
            ValidatePhaseText(circuit, context, devicePhase, findings);
            ValidateResolvedPhaseCompatibility(circuit, source, context, sourcePhase, devicePhase, findings);
        }
    }

    private static void ValidateResolvedVoltage(
        ElectricalCircuit circuit,
        ElectricalConnectorContext source,
        ElectricalConnectorContext context,
        List<ElectricalCircuitValidationFinding> findings)
    {
        var connector = context.Connector;
        if (source.Connector.Voltage <= 0 || connector.Voltage <= 0) return;
        if (Math.Abs(source.Connector.Voltage - connector.Voltage) <= 0.01) return;

        findings.Add(Error(
            circuit.Id,
            ElectricalCircuitValidationCategory.Voltage,
            "Connector voltage mismatch",
            $"Connector '{connector.Id}' is {connector.Voltage:g} V, but source connector '{source.Connector.Id}' is {source.Connector.Voltage:g} V.",
            connector.Id,
            context.Component.Id));
    }

    private static void ValidateResolvedPhaseCompatibility(
        ElectricalCircuit circuit,
        ElectricalConnectorContext source,
        ElectricalConnectorContext context,
        ParsedPhase sourcePhase,
        ParsedPhase devicePhase,
        List<ElectricalCircuitValidationFinding> findings)
    {
        if (!ShouldValidatePowerPhaseCompatibility(sourcePhase, devicePhase, circuit.SystemType)) return;
        if (devicePhase.Values.IsSubsetOf(sourcePhase.Values)) return;

        var connector = context.Connector;
        findings.Add(Error(
            circuit.Id,
            ElectricalCircuitValidationCategory.Phase,
            "Connector phase mismatch",
            $"Connector '{connector.Id}' phase '{connector.Phase}' is not compatible with source phase '{source.Connector.Phase}'.",
            connector.Id,
            context.Component.Id));
    }

    private static void ValidatePhaseText(
        ElectricalCircuit circuit,
        ElectricalConnectorContext context,
        ParsedPhase phase,
        List<ElectricalCircuitValidationFinding> findings)
    {
        if (phase.IsValid) return;

        findings.Add(Error(
            circuit.Id,
            ElectricalCircuitValidationCategory.Phase,
            "Invalid connector phase",
            $"Connector '{context.Connector.Id}' has invalid phase text '{context.Connector.Phase}'. Use A, B, C, AB, BC, AC, or ABC.",
            context.Connector.Id,
            context.Component.Id));
    }
}

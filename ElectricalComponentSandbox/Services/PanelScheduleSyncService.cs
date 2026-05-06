using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Synchronises connector-based <see cref="ElectricalCircuit"/> instances to their
/// corresponding <see cref="Circuit"/> panel-schedule rows.
///
/// The two models are linked via <see cref="ElectricalCircuit.ScheduleCircuitId"/> /
/// <see cref="Circuit.Id"/>. This service reads the connector data (phase, voltage,
/// connected device names) to create or refresh schedule rows, and reports which
/// existing rows are stale (no matching circuit).
///
/// Callers are responsible for persisting the returned circuits into
/// <see cref="ProjectModel"/> and for writing
/// <see cref="ElectricalCircuit.ScheduleCircuitId"/> from
/// <see cref="CircuitSyncEntry.Circuit"/>.<see cref="Circuit.Id"/>.
/// </summary>
public static class PanelScheduleSyncService
{
    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Syncs one panel's wired <see cref="ElectricalCircuit"/> instances to
    /// <see cref="Circuit"/> schedule rows.
    /// </summary>
    /// <param name="panelId">ID of the <see cref="PanelComponent"/> to sync.</param>
    /// <param name="components">All components in the project.</param>
    /// <param name="electricalCircuits">All connector-based circuits in the project.</param>
    /// <param name="existingCircuits">All existing schedule rows for the project.</param>
    public static PanelScheduleSyncResult SyncPanel(
        string panelId,
        IReadOnlyList<ElectricalComponent> components,
        IReadOnlyList<ElectricalCircuit> electricalCircuits,
        IReadOnlyList<Circuit> existingCircuits)
    {
        var panel = components.OfType<PanelComponent>().FirstOrDefault(p => p.Id == panelId);
        string panelName = panel?.Name ?? panelId;

        var connectorById = BuildConnectorIndex(components);
        var componentById = components.ToDictionary(c => c.Id);

        var panelConnectorIds = connectorById.Values
            .Where(c => c.ComponentId == panelId)
            .Select(c => c.Id)
            .ToHashSet();

        var panelCircuits = electricalCircuits
            .Where(ec => ec.PanelConnectorId != null
                         && panelConnectorIds.Contains(ec.PanelConnectorId))
            .ToList();

        var existingForPanel = existingCircuits
            .Where(c => c.PanelId == panelId)
            .ToDictionary(c => c.Id);

        var matchedIds = new HashSet<string>();
        var usedNumbers = existingForPanel.Values.Select(c => c.CircuitNumber).ToHashSet();
        int nextOdd = 1;
        var synced = new List<CircuitSyncEntry>(panelCircuits.Count);

        foreach (var ec in panelCircuits)
        {
            var entry = SyncOneCircuit(
                ec, panelId, connectorById, componentById,
                existingForPanel, matchedIds, usedNumbers, ref nextOdd);
            synced.Add(entry);
        }

        var staleIds = existingForPanel.Keys.Except(matchedIds).ToList();
        return new PanelScheduleSyncResult(panelId, panelName, synced, staleIds);
    }

    /// <summary>
    /// Syncs every <see cref="PanelComponent"/> found in <paramref name="components"/>.
    /// Returns one result per panel.
    /// </summary>
    public static IReadOnlyList<PanelScheduleSyncResult> SyncAllPanels(
        IReadOnlyList<ElectricalComponent> components,
        IReadOnlyList<ElectricalCircuit> electricalCircuits,
        IReadOnlyList<Circuit> existingCircuits)
    {
        var results = new List<PanelScheduleSyncResult>();
        foreach (var panel in components.OfType<PanelComponent>())
        {
            results.Add(SyncPanel(panel.Id, components, electricalCircuits, existingCircuits));
        }
        return results;
    }

    // ── Internal helpers ───────────────────────────────────────────────────────

    /// <summary>Returns the number of breaker poles implied by a phase string.</summary>
    internal static int PhaseToPoles(string phase) =>
        string.IsNullOrEmpty(phase) ? 1 : phase.Length switch
        {
            >= 3 => 3,
            2 => 2,
            _ => 1
        };

    private static string BuildDescription(
        ElectricalCircuit ec,
        Dictionary<string, ElectricalConnector> connectorById,
        Dictionary<string, ElectricalComponent> componentById)
    {
        var names = new List<string>();
        foreach (var devId in ec.DeviceConnectorIds)
        {
            if (!connectorById.TryGetValue(devId, out var dc)) continue;
            if (!componentById.TryGetValue(dc.ComponentId, out var comp)) continue;
            if (!string.IsNullOrWhiteSpace(comp.Name) && !names.Contains(comp.Name))
                names.Add(comp.Name);
        }
        return string.Join(", ", names);
    }

    private static int DefaultBreakerAmps(int poles) => poles >= 3 ? 30 : 20;

    private static Dictionary<string, ElectricalConnector> BuildConnectorIndex(
        IReadOnlyList<ElectricalComponent> components)
    {
        var index = new Dictionary<string, ElectricalConnector>();
        foreach (var comp in components)
        {
            if (comp.ElectricalConnectors == null) continue;
            foreach (var conn in comp.ElectricalConnectors.Connectors)
                index[conn.Id] = conn;
        }
        return index;
    }

    private static CircuitSyncEntry SyncOneCircuit(
        ElectricalCircuit ec,
        string panelId,
        Dictionary<string, ElectricalConnector> connectorById,
        Dictionary<string, ElectricalComponent> componentById,
        Dictionary<string, Circuit> existingForPanel,
        HashSet<string> matchedIds,
        HashSet<string> usedNumbers,
        ref int nextOdd)
    {
        connectorById.TryGetValue(ec.PanelConnectorId!, out var panelConnector);
        string phase = panelConnector?.Phase ?? "A";
        double voltage = panelConnector?.Voltage ?? 120.0;
        var systemType = panelConnector?.SystemType ?? ElectricalSystemType.PowerCircuit;
        int poles = PhaseToPoles(phase);
        string description = BuildDescription(ec, connectorById, componentById);
        var spec = new CircuitSpec(panelId, phase, voltage, poles, systemType, description);

        var existing = FindLinked(ec, existingForPanel);
        if (existing != null)
        {
            matchedIds.Add(existing.Id);
            return UpdateOrUnchanged(existing, ec.Id, spec);
        }

        return AddCircuit(ec.Id, spec, usedNumbers, ref nextOdd);
    }

    private static Circuit? FindLinked(
        ElectricalCircuit ec,
        Dictionary<string, Circuit> existingForPanel)
    {
        if (string.IsNullOrEmpty(ec.ScheduleCircuitId)) return null;
        existingForPanel.TryGetValue(ec.ScheduleCircuitId, out var c);
        return c;
    }

    private static CircuitSyncEntry UpdateOrUnchanged(
        Circuit existing, string ecId, in CircuitSpec s)
    {
        bool changed = existing.Description != s.Description
            || existing.Phase != s.Phase
            || existing.Voltage != s.Voltage
            || existing.Poles != s.Poles
            || existing.SystemType != s.SystemType;

        if (!changed)
            return new CircuitSyncEntry(existing, ecId, CircuitSyncAction.Unchanged);

        existing.Description = s.Description;
        existing.Phase = s.Phase;
        existing.Voltage = s.Voltage;
        existing.Poles = s.Poles;
        existing.Breaker.Poles = s.Poles;
        existing.SystemType = s.SystemType;
        return new CircuitSyncEntry(existing, ecId, CircuitSyncAction.Updated);
    }

    private static CircuitSyncEntry AddCircuit(
        string ecId, in CircuitSpec s,
        HashSet<string> usedNumbers, ref int nextOdd)
    {
        while (usedNumbers.Contains(nextOdd.ToString()))
            nextOdd += 2;

        string circuitNum = nextOdd.ToString();
        usedNumbers.Add(circuitNum);
        nextOdd += 2;

        var added = new Circuit
        {
            PanelId = s.PanelId,
            CircuitNumber = circuitNum,
            Description = s.Description,
            Phase = s.Phase,
            Voltage = s.Voltage,
            Poles = s.Poles,
            SystemType = s.SystemType,
            Breaker = new CircuitBreaker { TripAmps = DefaultBreakerAmps(s.Poles), Poles = s.Poles },
            SlotType = CircuitSlotType.Circuit,
            DemandFactor = 1.0,
        };
        return new CircuitSyncEntry(added, ecId, CircuitSyncAction.Added);
    }

    private readonly record struct CircuitSpec(
        string PanelId, string Phase, double Voltage,
        int Poles, ElectricalSystemType SystemType, string Description);
}

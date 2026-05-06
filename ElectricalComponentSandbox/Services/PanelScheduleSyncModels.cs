using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>Indicates the action taken for a circuit during panel schedule sync.</summary>
public enum CircuitSyncAction
{
    /// <summary>A new Circuit schedule row was created for this ElectricalCircuit.</summary>
    Added,

    /// <summary>The existing Circuit schedule row was refreshed from connector data.</summary>
    Updated,

    /// <summary>The Circuit schedule row already matched its connector data; no change was needed.</summary>
    Unchanged
}

/// <summary>
/// A single circuit sync outcome pairing an <see cref="ElectricalCircuit"/> to its
/// <see cref="Circuit"/> schedule row.
/// </summary>
public sealed record CircuitSyncEntry(
    /// <summary>The created or updated Circuit schedule row.</summary>
    Circuit Circuit,

    /// <summary>The ID of the ElectricalCircuit this schedule row represents.</summary>
    string ElectricalCircuitId,

    /// <summary>Action taken during this sync pass.</summary>
    CircuitSyncAction Action
);

/// <summary>
/// Result of synchronising one panel's <see cref="ElectricalCircuit"/> instances to its
/// <see cref="Circuit"/> panel-schedule rows.
/// </summary>
public sealed record PanelScheduleSyncResult(
    /// <summary>Panel component ID that was synced.</summary>
    string PanelId,

    /// <summary>Panel display name at the time of sync.</summary>
    string PanelName,

    /// <summary>All circuits synced during this pass (added, updated, or unchanged).</summary>
    IReadOnlyList<CircuitSyncEntry> SyncedCircuits,

    /// <summary>
    /// IDs of existing <see cref="Circuit"/> schedule rows for this panel that have no matching
    /// <see cref="ElectricalCircuit"/>. Callers may remove or flag these as stale.
    /// </summary>
    IReadOnlyList<string> StaleCircuitIds
);

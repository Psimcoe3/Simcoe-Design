namespace ElectricalComponentSandbox.Services;

using ElectricalComponentSandbox.Models;

/// <summary>
/// Manages the project's collection of named distribution system types.
/// Provides resolution, migration, and in-use guard logic.
/// </summary>
public class DistributionSystemService
{
    /// <summary>
    /// Ensures the project's <see cref="ProjectModel.DistributionSystems"/> list
    /// contains at least the four built-in defaults. Leaves user-added entries intact.
    /// </summary>
    public void EnsureDefaults(ProjectModel project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var existing = new HashSet<string>(project.DistributionSystems.Select(d => d.Id));
        foreach (var def in DistributionSystemType.GetBuiltInDefaults())
        {
            if (!existing.Contains(def.Id))
                project.DistributionSystems.Add(def);
        }
    }

    /// <summary>
    /// Resolves the <see cref="DistributionSystemType"/> for a panel schedule.
    /// Prefers <see cref="PanelSchedule.DistributionSystemId"/> when set;
    /// otherwise falls back to <see cref="PanelSchedule.VoltageConfig"/>
    /// via <see cref="DistributionSystemType.MigrateFromVoltageConfig"/>.
    /// </summary>
    /// <param name="schedule">The panel schedule to resolve.</param>
    /// <param name="systems">The project's available distribution system types.</param>
    public DistributionSystemType Resolve(PanelSchedule schedule, IEnumerable<DistributionSystemType> systems)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var lookup = (systems ?? Array.Empty<DistributionSystemType>())
            .ToDictionary(s => s.Id, s => s);

        // Prefer explicit DistributionSystemId
        if (!string.IsNullOrEmpty(schedule.DistributionSystemId)
            && lookup.TryGetValue(schedule.DistributionSystemId, out var found))
            return found;

        // Fallback: map legacy VoltageConfig → built-in system
        string migratedId = DistributionSystemType.MigrateFromVoltageConfig(schedule.VoltageConfig);
        if (lookup.TryGetValue(migratedId, out var migrated))
            return migrated;

        // Last resort: construct from built-in defaults
        return DistributionSystemType.GetBuiltInDefaults()
            .First(d => d.Id == migratedId);
    }

    /// <summary>
    /// Migrates all panel schedules in a list so that
    /// <see cref="PanelSchedule.DistributionSystemId"/> is set from their legacy
    /// <see cref="PanelSchedule.VoltageConfig"/>.
    /// Only touches schedules without an existing DistributionSystemId.
    /// </summary>
    public int MigrateFromLegacy(IEnumerable<PanelSchedule> schedules)
    {
        int migrated = 0;
        foreach (var s in schedules ?? Enumerable.Empty<PanelSchedule>())
        {
            if (string.IsNullOrEmpty(s.DistributionSystemId))
            {
                s.DistributionSystemId =
                    DistributionSystemType.MigrateFromVoltageConfig(s.VoltageConfig);
                migrated++;
            }
        }
        return migrated;
    }

    /// <summary>
    /// Returns <c>true</c> when the given distribution system ID is referenced by
    /// any panel schedule, meaning it should not be deleted.
    /// </summary>
    public bool IsInUse(string distributionSystemId, IEnumerable<PanelSchedule> schedules)
    {
        return schedules?.Any(s => s.DistributionSystemId == distributionSystemId) == true;
    }
}

using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Generates a wire pull schedule — a tabular report listing every wire
/// pull with its source panel, circuit, conduit run, wire size, conductor
/// count, conduit trade size, and length. Used for estimating, ordering,
/// and field planning.
/// </summary>
public static class WirePullScheduleService
{
    /// <summary>A single row in the wire pull schedule.</summary>
    public record WirePullEntry
    {
        public string PullId { get; init; } = "";
        public string PanelId { get; init; } = "";
        public string CircuitNumber { get; init; } = "";
        public string Description { get; init; } = "";
        public string From { get; init; } = "";
        public string To { get; init; } = "";

        // Wire
        public string WireSize { get; init; } = "";
        public int HotConductors { get; init; }
        public bool HasNeutral { get; init; }
        public string GroundSize { get; init; } = "";
        public string InsulationType { get; init; } = "";
        public string Material { get; init; } = "";
        public string ConductorSummary { get; init; } = "";

        // Conduit
        public string ConduitTradeSize { get; init; } = "";
        public string ConduitMaterial { get; init; } = "";

        // Length
        public double LengthFeet { get; init; }
    }

    /// <summary>Summary totals for the schedule.</summary>
    public record WirePullSummary
    {
        public int TotalPulls { get; init; }
        public double TotalWireFeet { get; init; }
        public int TotalConductors { get; init; }
        public Dictionary<string, double> WireFeetBySize { get; init; } = new();
        public Dictionary<string, int> PullsByConduitSize { get; init; } = new();
    }

    /// <summary>Complete wire pull schedule.</summary>
    public record WirePullSchedule
    {
        public string ProjectName { get; init; } = "";
        public List<WirePullEntry> Entries { get; init; } = new();
        public WirePullSummary Summary { get; init; } = new();
    }

    /// <summary>
    /// Generates a wire pull schedule from panels and their circuits.
    /// Each circuit with a wire spec produces a pull entry.
    /// </summary>
    public static WirePullSchedule Generate(
        IReadOnlyList<PanelSchedule> schedules,
        string projectName = "")
    {
        var entries = new List<WirePullEntry>();
        int pullIndex = 1;

        foreach (var panel in schedules)
        {
            foreach (var circuit in panel.Circuits)
            {
                if (circuit.SlotType != CircuitSlotType.Circuit)
                    continue;

                var wire = circuit.Wire;
                if (wire == null || string.IsNullOrEmpty(wire.Size))
                    continue;

                int hotCount = wire.Conductors > 0 ? wire.Conductors : circuit.Poles;
                bool hasNeutral = NeedsNeutral(circuit);
                int totalConductors = hotCount + (hasNeutral ? 1 : 0) + 1; // +1 ground

                string conductorSummary = FormatConductorSummary(hotCount, hasNeutral, wire);

                entries.Add(new WirePullEntry
                {
                    PullId = $"WP-{pullIndex:D3}",
                    PanelId = panel.PanelId,
                    CircuitNumber = circuit.CircuitNumber,
                    Description = circuit.Description,
                    From = panel.PanelName,
                    To = circuit.Description,
                    WireSize = wire.Size,
                    HotConductors = hotCount,
                    HasNeutral = hasNeutral,
                    GroundSize = wire.GroundSize ?? "",
                    InsulationType = wire.InsulationType ?? "THHN",
                    Material = wire.Material.ToString(),
                    ConductorSummary = conductorSummary,
                    LengthFeet = circuit.WireLengthFeet > 0 ? circuit.WireLengthFeet : 0,
                });

                pullIndex++;
            }
        }

        var summary = BuildSummary(entries);

        return new WirePullSchedule
        {
            ProjectName = projectName,
            Entries = entries,
            Summary = summary,
        };
    }

    /// <summary>
    /// Generates a text table version for quick review.
    /// </summary>
    public static string GenerateTextReport(WirePullSchedule schedule)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"WIRE PULL SCHEDULE: {schedule.ProjectName}");
        sb.AppendLine($"Total Pulls: {schedule.Summary.TotalPulls}  Total Wire: {schedule.Summary.TotalWireFeet:N0} ft");
        sb.AppendLine(new string('─', 90));
        sb.AppendLine($"{"Pull",-8} {"Panel",-10} {"Ckt",-6} {"Size",-6} {"Cond",-20} {"Length",-10} {"Description"}");
        sb.AppendLine(new string('─', 90));

        foreach (var e in schedule.Entries)
        {
            sb.AppendLine($"{e.PullId,-8} {e.PanelId,-10} {e.CircuitNumber,-6} {e.WireSize,-6} {e.ConductorSummary,-20} {e.LengthFeet,8:N0} ft  {e.Description}");
        }

        sb.AppendLine(new string('─', 90));
        sb.AppendLine("WIRE SUMMARY BY SIZE:");
        foreach (var (size, feet) in schedule.Summary.WireFeetBySize.OrderBy(kv => kv.Key))
            sb.AppendLine($"  #{size}: {feet:N0} ft");

        return sb.ToString();
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private static bool NeedsNeutral(Circuit circuit)
    {
        // Single-phase circuits with line-to-neutral voltage need a neutral
        // 3-pole circuits on a wye system also typically include neutral
        if (circuit.Poles == 1) return true;
        if (circuit.Voltage <= 120) return true;
        // 2-pole 208V or 240V: no neutral needed
        return false;
    }

    private static string FormatConductorSummary(int hotCount, bool hasNeutral, WireSpec wire)
    {
        var parts = new List<string>();
        parts.Add($"{hotCount}#{wire.Size}");
        if (hasNeutral)
            parts.Add($"1#{wire.Size}N");
        if (!string.IsNullOrEmpty(wire.GroundSize))
            parts.Add($"1#{wire.GroundSize}G");
        return string.Join(", ", parts);
    }

    private static WirePullSummary BuildSummary(List<WirePullEntry> entries)
    {
        var wireFeetBySize = new Dictionary<string, double>();
        int totalConductors = 0;

        foreach (var e in entries)
        {
            int conds = e.HotConductors + (e.HasNeutral ? 1 : 0) + (!string.IsNullOrEmpty(e.GroundSize) ? 1 : 0);
            double wireFt = e.LengthFeet * conds;
            totalConductors += conds;

            if (!wireFeetBySize.ContainsKey(e.WireSize))
                wireFeetBySize[e.WireSize] = 0;
            wireFeetBySize[e.WireSize] += e.LengthFeet * e.HotConductors;

            if (e.HasNeutral)
            {
                if (!wireFeetBySize.ContainsKey(e.WireSize))
                    wireFeetBySize[e.WireSize] = 0;
                wireFeetBySize[e.WireSize] += e.LengthFeet;
            }

            if (!string.IsNullOrEmpty(e.GroundSize))
            {
                if (!wireFeetBySize.ContainsKey(e.GroundSize))
                    wireFeetBySize[e.GroundSize] = 0;
                wireFeetBySize[e.GroundSize] += e.LengthFeet;
            }
        }

        return new WirePullSummary
        {
            TotalPulls = entries.Count,
            TotalWireFeet = entries.Sum(e => e.LengthFeet),
            TotalConductors = totalConductors,
            WireFeetBySize = wireFeetBySize,
        };
    }
}

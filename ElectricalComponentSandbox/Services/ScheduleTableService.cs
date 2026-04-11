using System.Windows;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Visual style for a row in a schedule table, used by the renderer to apply distinct backgrounds.
/// </summary>
public enum ScheduleRowStyle
{
    Normal,
    Subheader,
    Spare,
    Space,
    Footer
}

/// <summary>
/// Defines a column in a schedule table
/// </summary>
public class ScheduleColumn
{
    public string Header { get; set; } = string.Empty;
    public double Width { get; set; } = 100;
    public HorizontalAlignment Alignment { get; set; } = HorizontalAlignment.Left;
}

/// <summary>
/// A generated schedule table that can be rendered as a 2D annotation on the drawing.
/// Contains the table structure and cell values ready for rendering.
/// </summary>
public class ScheduleTable
{
    public string Title { get; set; } = string.Empty;
    public List<ScheduleColumn> Columns { get; set; } = new();
    public List<string[]> Rows { get; set; } = new();

    /// <summary>
    /// Optional per-row style hints for the renderer. Indexed parallel to <see cref="Rows"/>.
    /// If shorter than Rows, missing entries are treated as <see cref="ScheduleRowStyle.Normal"/>.
    /// </summary>
    public List<ScheduleRowStyle> RowStyles { get; set; } = new();

    /// <summary>Row height in document units</summary>
    public double RowHeight { get; set; } = 18;

    /// <summary>Title row height in document units</summary>
    public double TitleHeight { get; set; } = 24;

    /// <summary>Total width computed from columns</summary>
    public double TotalWidth => Columns.Sum(c => c.Width);

    /// <summary>Total height including title and all rows</summary>
    public double TotalHeight => TitleHeight + RowHeight + (Rows.Count * RowHeight);
}

/// <summary>
/// Generates schedule tables from project data for embedding as drawing annotations.
/// Produces tables matching the format used in Bluebeam/AutoCAD electrical drawings.
/// </summary>
public class ScheduleTableService
{
    /// <summary>
    /// Generates a panel schedule table for a given panel.
    /// </summary>
    public ScheduleTable GeneratePanelSchedule(
        string panelName,
        IReadOnlyList<Circuit> circuits)
    {
        var table = new ScheduleTable
        {
            Title = $"PANEL SCHEDULE - {panelName}",
            Columns =
            {
                new ScheduleColumn { Header = "CKT", Width = 40 },
                new ScheduleColumn { Header = "DESCRIPTION", Width = 160 },
                new ScheduleColumn { Header = "VOLTAGE", Width = 60, Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "POLES", Width = 45, Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "BREAKER", Width = 60, Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "WIRE", Width = 50, Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "VA", Width = 60, Alignment = HorizontalAlignment.Right }
            }
        };

        foreach (var c in circuits.OrderBy(c => c.CircuitNumber))
        {
            table.Rows.Add(new[]
            {
                c.CircuitNumber.ToString(),
                c.Description,
                c.Voltage.ToString("F0"),
                c.Breaker.Poles.ToString(),
                $"{c.Breaker.TripAmps}A",
                $"#{c.Wire.Size}",
                c.ConnectedLoadVA.ToString("F0")
            });
        }

        return table;
    }

    /// <summary>
    /// Generates an equipment schedule from all components.
    /// </summary>
    public ScheduleTable GenerateEquipmentSchedule(
        IReadOnlyList<ElectricalComponent> components,
        IReadOnlyList<ProjectParameterDefinition>? projectParameters = null)
    {
        var parameterLookup = ProjectParameterScheduleSupport.CreateParameterLookup(projectParameters);
        var table = new ScheduleTable
        {
            Title = "EQUIPMENT SCHEDULE",
            Columns =
            {
                new ScheduleColumn { Header = "TAG", Width = 80 },
                new ScheduleColumn { Header = "TYPE", Width = 80 },
                new ScheduleColumn { Header = "SIZE (W x H x D)", Width = 120 },
                new ScheduleColumn { Header = "ELEVATION", Width = 70, Alignment = HorizontalAlignment.Right },
                new ScheduleColumn { Header = "PARAMETERS", Width = 150 },
                new ScheduleColumn { Header = "MATERIAL", Width = 80 },
                new ScheduleColumn { Header = "MANUFACTURER", Width = 100 },
                new ScheduleColumn { Header = "LAYER", Width = 80 }
            }
        };

        foreach (var c in components.OrderBy(c => c.Type).ThenBy(c => c.Name))
        {
            table.Rows.Add(new[]
            {
                c.Name,
                c.Type.ToString(),
                $"{c.Parameters.Width:F1} x {c.Parameters.Height:F1} x {c.Parameters.Depth:F1}",
                c.Parameters.Elevation.ToString("F1"),
                ProjectParameterScheduleSupport.BuildComponentBindingSummary(c, parameterLookup),
                c.Parameters.Material,
                c.Parameters.Manufacturer,
                c.LayerId
            });
        }

        return table;
    }

    /// <summary>
    /// Generates a conduit schedule from conduit components.
    /// </summary>
    public ScheduleTable GenerateConduitSchedule(
        IReadOnlyList<ElectricalComponent> components,
        IReadOnlyList<ProjectParameterDefinition>? projectParameters = null)
    {
        var parameterLookup = ProjectParameterScheduleSupport.CreateParameterLookup(projectParameters);
        var conduits = components
            .Where(c => c.Type == ComponentType.Conduit)
            .Cast<ConduitComponent>()
            .OrderBy(c => c.Name)
            .ToList();

        var table = new ScheduleTable
        {
            Title = "CONDUIT SCHEDULE",
            Columns =
            {
                new ScheduleColumn { Header = "TAG", Width = 80 },
                new ScheduleColumn { Header = "TYPE", Width = 60 },
                new ScheduleColumn { Header = "SIZE", Width = 50, Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "LENGTH (ft)", Width = 80, Alignment = HorizontalAlignment.Right },
                new ScheduleColumn { Header = "BENDS", Width = 50, Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "ELEVATION", Width = 70, Alignment = HorizontalAlignment.Right },
                new ScheduleColumn { Header = "PARAMETERS", Width = 150 },
                new ScheduleColumn { Header = "LAYER", Width = 80 }
            }
        };

        foreach (var c in conduits)
        {
            table.Rows.Add(new[]
            {
                c.Name,
                c.ConduitType,
                $"{c.Diameter:F2}\"",
                c.Length.ToString("F1"),
                c.BendPoints.Count.ToString(),
                c.Parameters.Elevation.ToString("F1"),
                ProjectParameterScheduleSupport.BuildComponentBindingSummary(c, parameterLookup),
                c.LayerId
            });
        }

        return table;
    }

    public ScheduleTable GenerateProjectParameterSchedule(
        IReadOnlyList<ProjectParameterDefinition> parameters,
        IReadOnlyList<ElectricalComponent> components)
    {
        var usageLookup = ProjectParameterScheduleSupport.BuildUsageMap(components);
        var table = new ScheduleTable
        {
            Title = "PROJECT PARAMETERS",
            Columns =
            {
                new ScheduleColumn { Header = "NAME", Width = 120 },
                new ScheduleColumn { Header = "TYPE", Width = 75, Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "VALUE", Width = 110, Alignment = HorizontalAlignment.Left },
                new ScheduleColumn { Header = "FORMULA", Width = 180 },
                new ScheduleColumn { Header = "BOUND FIELDS", Width = 85, Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "USED BY", Width = 110 },
                new ScheduleColumn { Header = "STATUS", Width = 150 }
            }
        };

        foreach (var parameter in parameters.OrderBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase))
        {
            var usage = usageLookup.TryGetValue(parameter.Id, out var summary)
                ? summary
                : ProjectParameterUsageSummary.Empty;
            table.Rows.Add(new[]
            {
                parameter.Name,
                parameter.ValueKind.ToString(),
                ProjectParameterScheduleSupport.FormatParameterValue(parameter),
                parameter.SupportsFormula
                    ? (string.IsNullOrWhiteSpace(parameter.Formula) ? "(fixed)" : parameter.Formula)
                    : "(n/a)",
                usage.TargetSummary,
                ProjectParameterScheduleSupport.FormatUsageSummary(usage),
                string.IsNullOrWhiteSpace(parameter.FormulaError) ? "OK" : parameter.FormulaError
            });
        }

        return table;
    }

    /// <summary>
    /// Generates a circuit summary table.
    /// </summary>
    public ScheduleTable GenerateCircuitSummary(
        IReadOnlyList<Circuit> circuits)
    {
        var table = new ScheduleTable
        {
            Title = "CIRCUIT SUMMARY",
            Columns =
            {
                new ScheduleColumn { Header = "CKT#", Width = 45 },
                new ScheduleColumn { Header = "DESCRIPTION", Width = 150 },
                new ScheduleColumn { Header = "V", Width = 40, Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "BKR", Width = 50, Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "WIRE", Width = 50, Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "LENGTH", Width = 60, Alignment = HorizontalAlignment.Right },
                new ScheduleColumn { Header = "LOAD (VA)", Width = 70, Alignment = HorizontalAlignment.Right },
                new ScheduleColumn { Header = "V-DROP %", Width = 65, Alignment = HorizontalAlignment.Right }
            }
        };

        foreach (var c in circuits.OrderBy(c => c.CircuitNumber))
        {
            // Simplified voltage drop calculation
            double resistance = GetWireResistance(c.Wire.Size);
            double amps = c.Voltage > 0 ? c.ConnectedLoadVA / c.Voltage : 0;
            double vDrop = 2 * resistance * amps * c.WireLengthFeet / 1000.0;

            double vDropPercent = c.Voltage > 0 ? (vDrop / c.Voltage) * 100 : 0;

            table.Rows.Add(new[]
            {
                c.CircuitNumber.ToString(),
                c.Description,
                c.Voltage.ToString("F0"),
                $"{c.Breaker.TripAmps}A/{c.Breaker.Poles}P",
                $"#{c.Wire.Size}",
                $"{c.WireLengthFeet:F0} ft",
                c.ConnectedLoadVA.ToString("F0"),
                $"{vDropPercent:F1}%"
            });
        }

        return table;
    }

    // ── Slot-Map Panel Schedule ───────────────────────────────────────────────

    /// <summary>
    /// Assigns slot numbers to all circuits in a panel based on the panel's
    /// <see cref="CircuitSequence"/> setting. Circuits are sorted, then each
    /// grabs consecutive slots equal to its pole count. Spare and Space slots
    /// each consume one slot. Circuits whose SlotNumber is already set are
    /// renumbered along with the rest (full rebuild).
    /// </summary>
    public void AssignSlotNumbers(PanelSchedule schedule)
    {
        IEnumerable<Circuit> ordered = schedule.CircuitSequence switch
        {
            CircuitSequence.GroupByPhase => schedule.Circuits
                .OrderBy(c => PhaseSortKey(c.Phase))
                .ThenBy(c => ParseCircuitNumber(c.CircuitNumber)),

            // Odd circuit numbers first (left column fills first), then even
            CircuitSequence.OddThenEven => schedule.Circuits
                .OrderBy(c => ParseCircuitNumber(c.CircuitNumber) % 2 == 0 ? 1 : 0)
                .ThenBy(c => ParseCircuitNumber(c.CircuitNumber)),

            // Numerical (default)
            _ => schedule.Circuits
                .OrderBy(c => ParseCircuitNumber(c.CircuitNumber))
                .ThenBy(c => c.CircuitNumber, StringComparer.OrdinalIgnoreCase)
        };

        int nextSlot = 1;
        foreach (var c in ordered)
        {
            c.SlotNumber = nextSlot;
            nextSlot += c.Breaker.Poles;
        }
    }

    /// <summary>
    /// Generates a two-column slot-map panel schedule from a <see cref="PanelSchedule"/>.
    /// Odd slots appear in the left column, even slots in the right column.
    /// Includes a panel-info subheader row and a per-phase load footer row.
    /// Slot numbers are auto-assigned if any circuit has SlotNumber == 0.
    /// </summary>
    public ScheduleTable GeneratePanelSchedule(PanelSchedule schedule)
    {
        if (schedule.Circuits.Any(c => c.SlotNumber == 0))
            AssignSlotNumbers(schedule);

        // Build slot→circuit map (each pole position maps to its parent circuit)
        var slotMap = new Dictionary<int, Circuit>();
        foreach (var c in schedule.Circuits.Where(c => c.SlotNumber > 0))
            for (int i = 0; i < c.Breaker.Poles; i++)
                slotMap[c.SlotNumber + i] = c;

        int maxSlot = slotMap.Keys.Any() ? slotMap.Keys.Max() : 0;
        if (maxSlot % 2 != 0) maxSlot++; // always an even number of slots
        int totalRows = maxSlot / 2;

        string voltageLabel = schedule.VoltageConfig switch
        {
            PanelVoltageConfig.V120_208_3Ph => "120/208V 3\u00d83W",
            PanelVoltageConfig.V277_480_3Ph => "277/480V 3\u00d83W",
            PanelVoltageConfig.V120_240_1Ph => "120/240V 1\u00d83W",
            PanelVoltageConfig.V240_3Ph     => "240V 3\u00d83W",
            _ => "\u2014"
        };
        string mainInfo = schedule.IsMainLugsOnly
            ? "MLO"
            : $"MB {schedule.MainBreakerAmps}A";

        var table = new ScheduleTable
        {
            Title = $"PANEL SCHEDULE \u2014 {schedule.PanelName}",
            Columns =
            {
                new ScheduleColumn { Header = "CKT",         Width = 40,  Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "DESCRIPTION", Width = 130, Alignment = HorizontalAlignment.Left   },
                new ScheduleColumn { Header = "BKR",         Width = 55,  Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "VA",          Width = 58,  Alignment = HorizontalAlignment.Right  },
                new ScheduleColumn { Header = "VA",          Width = 58,  Alignment = HorizontalAlignment.Right  },
                new ScheduleColumn { Header = "BKR",         Width = 55,  Alignment = HorizontalAlignment.Center },
                new ScheduleColumn { Header = "DESCRIPTION", Width = 130, Alignment = HorizontalAlignment.Left   },
                new ScheduleColumn { Header = "CKT",         Width = 40,  Alignment = HorizontalAlignment.Center },
            }
        };

        // Subheader: panel summary info
        table.Rows.Add(new[]
        {
            schedule.PanelName,
            voltageLabel,
            $"{schedule.BusAmps}A Bus",
            mainInfo,
            $"{schedule.AvailableFaultCurrentKA:F0} kAIC",
            "", "", ""
        });
        table.RowStyles.Add(ScheduleRowStyle.Subheader);

        // Circuit rows: one table row per left/right slot pair
        for (int row = 1; row <= totalRows; row++)
        {
            int leftSlot  = 2 * row - 1;
            int rightSlot = 2 * row;

            slotMap.TryGetValue(leftSlot,  out var leftCircuit);
            slotMap.TryGetValue(rightSlot, out var rightCircuit);

            bool leftIsPrimary  = leftCircuit  != null && leftCircuit.SlotNumber  == leftSlot;
            bool rightIsPrimary = rightCircuit != null && rightCircuit.SlotNumber == rightSlot;

            string[] cells = new string[8];
            string[] lc = BuildLeftCells(leftCircuit,  leftSlot,  leftIsPrimary);
            string[] rc = BuildRightCells(rightCircuit, rightSlot, rightIsPrimary);
            Array.Copy(lc, 0, cells, 0, 4);
            Array.Copy(rc, 0, cells, 4, 4);
            table.Rows.Add(cells);

            ScheduleRowStyle style = ScheduleRowStyle.Normal;
            if (leftCircuit?.SlotType  == CircuitSlotType.Spare ||
                rightCircuit?.SlotType == CircuitSlotType.Spare)
                style = ScheduleRowStyle.Spare;
            else if (leftCircuit?.SlotType  == CircuitSlotType.Space ||
                     rightCircuit?.SlotType == CircuitSlotType.Space)
                style = ScheduleRowStyle.Space;
            table.RowStyles.Add(style);
        }

        // Footer: per-phase load totals
        var (phA, phB, phC) = schedule.PhaseDemandVA;
        bool is3Ph = schedule.VoltageConfig != PanelVoltageConfig.V120_240_1Ph;
        double lineV = schedule.VoltageConfig switch
        {
            PanelVoltageConfig.V120_240_1Ph => 240,
            PanelVoltageConfig.V120_208_3Ph => 208,
            PanelVoltageConfig.V277_480_3Ph => 480,
            PanelVoltageConfig.V240_3Ph     => 240,
            _ => 208
        };
        double totalCurrent = is3Ph
            ? schedule.TotalDemandVA / (lineV * Math.Sqrt(3))
            : schedule.TotalDemandVA / lineV;

        table.Rows.Add(new[]
        {
            $"Ph A: {phA:F0} VA",
            $"Ph B: {phB:F0} VA",
            $"Ph C: {phC:F0} VA",
            $"Total: {schedule.TotalDemandVA:F0} VA",
            $"{totalCurrent:F1} A",
            "", "", ""
        });
        table.RowStyles.Add(ScheduleRowStyle.Footer);

        return table;
    }

    private static string[] BuildLeftCells(Circuit? c, int slot, bool isPrimary)
    {
        if (c == null)                              return new[] { slot.ToString(), "", "", "" };
        if (c.SlotType == CircuitSlotType.Spare)    return new[] { slot.ToString(), "SPARE", "", "" };
        if (c.SlotType == CircuitSlotType.Space)    return new[] { slot.ToString(), "SPACE", "", "" };
        if (!isPrimary)                             return new[] { "\u2193", "", "", "" }; // ↓ continuation
        return new[]
        {
            c.CircuitNumber,
            c.Description,
            FormatBreaker(c.Breaker),
            c.ConnectedLoadVA.ToString("F0")
        };
    }

    private static string[] BuildRightCells(Circuit? c, int slot, bool isPrimary)
    {
        if (c == null)                              return new[] { "", "", "", slot.ToString() };
        if (c.SlotType == CircuitSlotType.Spare)    return new[] { "", "", "SPARE", slot.ToString() };
        if (c.SlotType == CircuitSlotType.Space)    return new[] { "", "", "SPACE", slot.ToString() };
        if (!isPrimary)                             return new[] { "", "", "\u2193", "\u2193" }; // ↓ continuation
        return new[]
        {
            c.ConnectedLoadVA.ToString("F0"),
            FormatBreaker(c.Breaker),
            c.Description,
            c.CircuitNumber
        };
    }

    private static string FormatBreaker(CircuitBreaker b)
        => $"{b.TripAmps}A {b.Poles}P";

    private static int PhaseSortKey(string phase)
    {
        if (phase.Contains('A')) return 0;
        if (phase.Contains('B')) return 1;
        if (phase.Contains('C')) return 2;
        return 3;
    }

    private static int ParseCircuitNumber(string s)
        => int.TryParse(s, out int n) ? n : int.MaxValue;

    private static double GetWireResistance(string size) => size switch
    {
        "14" => 3.14,
        "12" => 1.98,
        "10" => 1.24,
        "8"  => 0.778,
        "6"  => 0.491,
        "4"  => 0.308,
        "2"  => 0.194,
        "1"  => 0.154,
        "1/0" => 0.122,
        "2/0" => 0.0967,
        "3/0" => 0.0766,
        "4/0" => 0.0608,
        _ => 1.98
    };
}

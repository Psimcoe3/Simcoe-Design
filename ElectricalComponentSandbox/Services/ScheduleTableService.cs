using System.Windows;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

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
                new ScheduleColumn { Header = "VALUE", Width = 70, Alignment = HorizontalAlignment.Right },
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
                parameter.Value.ToString("0.###"),
                string.IsNullOrWhiteSpace(parameter.Formula) ? "(fixed)" : parameter.Formula,
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

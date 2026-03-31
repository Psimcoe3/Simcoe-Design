using ClosedXML.Excel;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services.Export;

/// <summary>
/// Exports a formatted equipment/conduit schedule to an .xlsx workbook using ClosedXML.
///
/// Sheets produced:
///   "All Components"  — one row per component, common fields
///   "Conduit Schedule"— conduit-specific columns (type, diameter, length, bends)
///   "Panel Schedule"  — panel-specific columns (amperage, circuits, type)
///   "Box Schedule"    — junction box schedule
/// </summary>
public class ScheduleExcelExporter
{
    // ── Configuration ─────────────────────────────────────────────────────────

    /// <summary>Project name written into cell A1 of each sheet header.</summary>
    public string ProjectName { get; set; } = "Electrical Project";
    /// <summary>Colour of header row cells (ARGB hex without #).</summary>
    public string HeaderColorArgb { get; set; } = "FF1F3864";   // Dark navy
    /// <summary>Colour of title row (project + date) cells.</summary>
    public string TitleColorArgb  { get; set; } = "FF2E75B6";   // Medium blue
    /// <summary>Freeze the header row for easy scrolling.</summary>
    public bool FreezeHeader { get; set; } = true;
    /// <summary>Apply alternating row fill.</summary>
    public bool AlternateRows { get; set; } = true;
    /// <summary>ARGB colour for even-row fill when AlternateRows = true.</summary>
    public string AlternateRowColorArgb { get; set; } = "FFD9E1F2"; // Light blue-grey

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates schedule workbook and saves it to <paramref name="outputPath"/>.
    /// </summary>
    public void ExportSchedule(IEnumerable<ElectricalComponent> components, string outputPath, IEnumerable<ProjectParameterDefinition>? projectParameters = null)
    {
        var all = components.ToList();
        var parameterList = projectParameters?.ToList() ?? [];
        var parameterLookup = ProjectParameterScheduleSupport.CreateParameterLookup(parameterList);

        using var wb = new XLWorkbook();

        AddAllComponentsSheet(wb, all, parameterLookup);
        AddConduitSheet(wb, all.OfType<ConduitComponent>().ToList(), parameterLookup);
        AddPanelSheet(wb, all.OfType<PanelComponent>().ToList(), parameterLookup);
        AddBoxSheet(wb, all.OfType<BoxComponent>().ToList(), parameterLookup);
        AddProjectParametersSheet(wb, parameterList, all);

        wb.SaveAs(outputPath);
    }

    // ── Sheet builders ────────────────────────────────────────────────────────

    private void AddAllComponentsSheet(XLWorkbook wb, List<ElectricalComponent> all, IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup)
    {
        var ws = wb.Worksheets.Add("All Components");

        WriteTitleRow(ws, 1, "All Components Schedule");

        var headers = new[]
        {
            "ID", "Name", "Type", "Layer",
            "Width (in)", "Height (in)", "Depth (in)", "Elevation (in)",
            "Material", "Manufacturer", "Part Number", "Reference URL", "Parameter Bindings"
        };
        WriteHeaderRow(ws, 2, headers);

        int row = 3;
        foreach (var c in all)
        {
            var p = c.Parameters;
            ws.Cell(row, 1).Value  = c.Id;
            ws.Cell(row, 2).Value  = c.Name;
            ws.Cell(row, 3).Value  = c.Type.ToString();
            ws.Cell(row, 4).Value  = c.LayerId;
            ws.Cell(row, 5).Value  = p.Width;
            ws.Cell(row, 6).Value  = p.Height;
            ws.Cell(row, 7).Value  = p.Depth;
            ws.Cell(row, 8).Value  = p.Elevation;
            ws.Cell(row, 9).Value  = p.Material;
            ws.Cell(row, 10).Value = p.Manufacturer;
            ws.Cell(row, 11).Value = p.PartNumber;
            ws.Cell(row, 12).Value = p.ReferenceUrl;
            ws.Cell(row, 13).Value = ProjectParameterScheduleSupport.BuildComponentBindingSummary(c, parameterLookup);

            ApplyAlternateRowFill(ws, row, headers.Length);
            row++;
        }

        FormatSheet(ws, headers.Length);
    }

    private void AddConduitSheet(XLWorkbook wb, List<ConduitComponent> conduits, IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup)
    {
        var ws = wb.Worksheets.Add("Conduit Schedule");

        WriteTitleRow(ws, 1, "Conduit Schedule");

        var headers = new[]
        {
            "ID", "Name", "Conduit Type", "Trade Diameter (in)", "Length (ft)",
            "Bend Type", "Bend Radius (in)", "Material", "Manufacturer", "Part Number",
            "Elevation (in)", "Layer", "Parameter Bindings"
        };
        WriteHeaderRow(ws, 2, headers);

        int row = 3;
        foreach (var c in conduits)
        {
            var p = c.Parameters;
            ws.Cell(row, 1).Value  = c.Id;
            ws.Cell(row, 2).Value  = c.Name;
            ws.Cell(row, 3).Value  = c.ConduitType;
            ws.Cell(row, 4).Value  = c.Diameter;
            ws.Cell(row, 5).Value  = Math.Round(c.Length / 12.0, 3); // inches → feet
            ws.Cell(row, 6).Value  = c.BendType.ToString();
            ws.Cell(row, 7).Value  = c.BendRadius;
            ws.Cell(row, 8).Value  = p.Material;
            ws.Cell(row, 9).Value  = p.Manufacturer;
            ws.Cell(row, 10).Value = p.PartNumber;
            ws.Cell(row, 11).Value = p.Elevation;
            ws.Cell(row, 12).Value = c.LayerId;
            ws.Cell(row, 13).Value = ProjectParameterScheduleSupport.BuildComponentBindingSummary(c, parameterLookup);

            ApplyAlternateRowFill(ws, row, headers.Length);
            row++;
        }

        FormatSheet(ws, headers.Length);

        // Summary row
        if (conduits.Count > 0)
        {
            int sumRow = row + 1;
            ws.Cell(sumRow, 1).Value = "TOTAL LENGTH (ft):";
            ws.Cell(sumRow, 1).Style.Font.Bold = true;
            ws.Cell(sumRow, 5).FormulaA1 = $"=SUM(E3:E{row - 1})";
            ws.Cell(sumRow, 5).Style.Font.Bold = true;
            ws.Cell(sumRow, 5).Style.NumberFormat.Format = "#,##0.000";
        }
    }

    private void AddPanelSheet(XLWorkbook wb, List<PanelComponent> panels, IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup)
    {
        var ws = wb.Worksheets.Add("Panel Schedule");

        WriteTitleRow(ws, 1, "Panel Schedule");

        var headers = new[]
        {
            "ID", "Name", "Panel Type", "Amperage (A)", "Circuit Count",
            "Width (in)", "Height (in)", "Depth (in)", "Elevation (in)",
            "Material", "Manufacturer", "Part Number", "Layer", "Parameter Bindings"
        };
        WriteHeaderRow(ws, 2, headers);

        int row = 3;
        foreach (var p2 in panels)
        {
            var p = p2.Parameters;
            ws.Cell(row, 1).Value  = p2.Id;
            ws.Cell(row, 2).Value  = p2.Name;
            ws.Cell(row, 3).Value  = p2.PanelType;
            ws.Cell(row, 4).Value  = p2.Amperage;
            ws.Cell(row, 5).Value  = p2.CircuitCount;
            ws.Cell(row, 6).Value  = p.Width;
            ws.Cell(row, 7).Value  = p.Height;
            ws.Cell(row, 8).Value  = p.Depth;
            ws.Cell(row, 9).Value  = p.Elevation;
            ws.Cell(row, 10).Value = p.Material;
            ws.Cell(row, 11).Value = p.Manufacturer;
            ws.Cell(row, 12).Value = p.PartNumber;
            ws.Cell(row, 13).Value = p2.LayerId;
            ws.Cell(row, 14).Value = ProjectParameterScheduleSupport.BuildComponentBindingSummary(p2, parameterLookup);

            ApplyAlternateRowFill(ws, row, headers.Length);
            row++;
        }

        FormatSheet(ws, headers.Length);
    }

    private void AddBoxSheet(XLWorkbook wb, List<BoxComponent> boxes, IReadOnlyDictionary<string, ProjectParameterDefinition> parameterLookup)
    {
        var ws = wb.Worksheets.Add("Box Schedule");

        WriteTitleRow(ws, 1, "Junction Box Schedule");

        var headers = new[]
        {
            "ID", "Name", "Box Type", "Knockout Count",
            "Width (in)", "Height (in)", "Depth (in)", "Elevation (in)",
            "Material", "Manufacturer", "Part Number", "Layer", "Parameter Bindings"
        };
        WriteHeaderRow(ws, 2, headers);

        int row = 3;
        foreach (var b in boxes)
        {
            var p = b.Parameters;
            ws.Cell(row, 1).Value  = b.Id;
            ws.Cell(row, 2).Value  = b.Name;
            ws.Cell(row, 3).Value  = b.BoxType;
            ws.Cell(row, 4).Value  = b.KnockoutCount;
            ws.Cell(row, 5).Value  = p.Width;
            ws.Cell(row, 6).Value  = p.Height;
            ws.Cell(row, 7).Value  = p.Depth;
            ws.Cell(row, 8).Value  = p.Elevation;
            ws.Cell(row, 9).Value  = p.Material;
            ws.Cell(row, 10).Value = p.Manufacturer;
            ws.Cell(row, 11).Value = p.PartNumber;
            ws.Cell(row, 12).Value = b.LayerId;
            ws.Cell(row, 13).Value = ProjectParameterScheduleSupport.BuildComponentBindingSummary(b, parameterLookup);

            ApplyAlternateRowFill(ws, row, headers.Length);
            row++;
        }

        FormatSheet(ws, headers.Length);
    }

    private void AddProjectParametersSheet(XLWorkbook wb, List<ProjectParameterDefinition> parameters, List<ElectricalComponent> all)
    {
        var ws = wb.Worksheets.Add("Project Parameters");

        WriteTitleRow(ws, 1, "Project Parameters Schedule");

        var headers = new[]
        {
            "Name", "Value (ft)", "Formula", "Bound Fields", "Used By", "Status"
        };
        WriteHeaderRow(ws, 2, headers);

        int row = 3;
        foreach (var parameter in parameters.OrderBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase))
        {
            var usage = ProjectParameterScheduleSupport.GetUsage(parameter, all);
            ws.Cell(row, 1).Value = parameter.Name;
            ws.Cell(row, 2).Value = parameter.Value;
            ws.Cell(row, 3).Value = string.IsNullOrWhiteSpace(parameter.Formula) ? "(fixed)" : parameter.Formula;
            ws.Cell(row, 4).Value = usage.TargetSummary;
            ws.Cell(row, 5).Value = ProjectParameterScheduleSupport.FormatUsageSummary(usage);
            ws.Cell(row, 6).Value = string.IsNullOrWhiteSpace(parameter.FormulaError) ? "OK" : parameter.FormulaError;

            ApplyAlternateRowFill(ws, row, headers.Length);
            row++;
        }

        FormatSheet(ws, headers.Length);
        ws.Column(2).Style.NumberFormat.Format = "0.###";
    }

    // ── Style helpers ─────────────────────────────────────────────────────────

    private void WriteTitleRow(IXLWorksheet ws, int row, string subtitle)
    {
        var dateStr = DateTime.Now.ToString("yyyy-MM-dd");
        ws.Cell(row, 1).Value = $"{ProjectName}  |  {subtitle}  |  {dateStr}";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(
            HexToArgb(TitleColorArgb));
        ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
    }

    private void WriteHeaderRow(IXLWorksheet ws, int row, string[] headers)
    {
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(row, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(HexToArgb(HeaderColorArgb));
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        }

        if (FreezeHeader)
            ws.SheetView.FreezeRows(row);
    }

    private void ApplyAlternateRowFill(IXLWorksheet ws, int row, int colCount)
    {
        if (!AlternateRows || row % 2 == 0) return;
        var fill = XLColor.FromArgb(HexToArgb(AlternateRowColorArgb));
        for (int c = 1; c <= colCount; c++)
            ws.Cell(row, c).Style.Fill.BackgroundColor = fill;
    }

    private static void FormatSheet(IXLWorksheet ws, int colCount)
    {
        // Auto-fit all data columns
        for (int c = 1; c <= colCount; c++)
            ws.Column(c).AdjustToContents();

        // Minimum column width to keep it readable
        for (int c = 1; c <= colCount; c++)
            if (ws.Column(c).Width < 10) ws.Column(c).Width = 10;

        // Merge title across all data columns
        ws.Range(1, 1, 1, colCount).Merge();
    }

    private static int HexToArgb(string hex)
    {
        // Parse 8-char ARGB e.g. "FF1F3864"
        if (hex.Length == 8)
            return (int)Convert.ToUInt32(hex, 16);
        // Parse 6-char RGB (assume opaque)
        if (hex.Length == 6)
            return (int)(0xFF000000u | Convert.ToUInt32(hex, 16));
        return unchecked((int)0xFF808080u);
    }
}

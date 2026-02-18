using System.Text;
using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Conduit.Core.Routing;

namespace ElectricalComponentSandbox.Conduit.Persistence;

/// <summary>
/// A run schedule entry for export.
/// </summary>
public class RunScheduleEntry
{
    public string RunId { get; set; } = string.Empty;
    public string ConduitType { get; set; } = string.Empty;
    public string TradeSize { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public double TotalLengthFeet { get; set; }
    public int SegmentCount { get; set; }
    public int FittingCount { get; set; }
    public string StartEquipment { get; set; } = string.Empty;
    public string EndEquipment { get; set; } = string.Empty;
    public string Voltage { get; set; } = string.Empty;
    public double ConductorFillPercent { get; set; }
    public List<string> FittingTypes { get; set; } = new();
    public List<double> CutLengthsInches { get; set; } = new();
}

/// <summary>
/// Generates run schedule reports and CSV exports.
/// </summary>
public class RunScheduleService
{
    private readonly ConduitModelStore _store;
    private readonly SmartBendService _bends;

    public RunScheduleService(ConduitModelStore store, SmartBendService? bends = null)
    {
        _store = store;
        _bends = bends ?? new SmartBendService();
    }

    /// <summary>
    /// Generates schedule entries for all runs in the store.
    /// </summary>
    public List<RunScheduleEntry> GenerateSchedule()
    {
        var entries = new List<RunScheduleEntry>();

        foreach (var run in _store.GetAllRuns())
        {
            var segments = run.GetSegments(_store).ToList();
            var fittings = run.GetFittings(_store).ToList();

            var entry = new RunScheduleEntry
            {
                RunId = run.RunId,
                ConduitType = _store.GetType(run.ConduitTypeId)?.Name ?? run.Material.ToString(),
                TradeSize = run.TradeSize,
                Material = run.Material.ToString(),
                TotalLengthFeet = run.ComputeTotalLength(_store),
                SegmentCount = segments.Count,
                FittingCount = fittings.Count,
                StartEquipment = run.StartEquipment,
                EndEquipment = run.EndEquipment,
                Voltage = run.Voltage,
                ConductorFillPercent = run.ConductorFillPercent,
                FittingTypes = fittings.Select(f => f.Type.ToString()).ToList()
            };

            // Compute cut lengths per segment
            for (int i = 0; i < segments.Count; i++)
            {
                double rawInches = segments[i].Length * 12.0;
                double? startAngle = null;
                double? endAngle = null;

                // Find fittings at start/end
                if (i > 0)
                {
                    var fit = fittings.FirstOrDefault(f =>
                        f.ConnectedSegmentIds.Contains(segments[i - 1].Id) &&
                        f.ConnectedSegmentIds.Contains(segments[i].Id));
                    startAngle = fit?.AngleDegrees;
                }
                if (i < segments.Count - 1)
                {
                    var fit = fittings.FirstOrDefault(f =>
                        f.ConnectedSegmentIds.Contains(segments[i].Id) &&
                        f.ConnectedSegmentIds.Contains(segments[i + 1].Id));
                    endAngle = fit?.AngleDegrees;
                }

                double cutLength = _bends.ComputeCutLength(rawInches, run.TradeSize, startAngle, endAngle);
                entry.CutLengthsInches.Add(cutLength);
            }

            entries.Add(entry);
        }

        return entries;
    }

    /// <summary>
    /// Exports the run schedule as CSV.
    /// </summary>
    public string ExportScheduleCsv()
    {
        var schedule = GenerateSchedule();
        var sb = new StringBuilder();
        sb.AppendLine("RunId,Type,TradeSize,Material,TotalLength(ft),Segments,Fittings,StartEquip,EndEquip,Voltage,FillPct,FittingTypes,CutLengths(in)");

        foreach (var entry in schedule)
        {
            sb.AppendLine(
                $"\"{entry.RunId}\"," +
                $"\"{entry.ConduitType}\"," +
                $"\"{entry.TradeSize}\"," +
                $"\"{entry.Material}\"," +
                $"{entry.TotalLengthFeet:F2}," +
                $"{entry.SegmentCount}," +
                $"{entry.FittingCount}," +
                $"\"{entry.StartEquipment}\"," +
                $"\"{entry.EndEquipment}\"," +
                $"\"{entry.Voltage}\"," +
                $"{entry.ConductorFillPercent:F1}," +
                $"\"{string.Join(";", entry.FittingTypes)}\"," +
                $"\"{string.Join(";", entry.CutLengthsInches.Select(c => c.ToString("F2")))}\"");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Saves the run schedule to a CSV file.
    /// </summary>
    public async Task ExportScheduleCsvAsync(string filePath)
    {
        var csv = ExportScheduleCsv();
        await File.WriteAllTextAsync(filePath, csv);
    }
}

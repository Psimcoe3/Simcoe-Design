using System.Text;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Manages a "Markups List" with filtering, sorting, and CSV/BOM export
/// </summary>
public class MarkupListService
{
    private readonly MeasurementService _measurement;

    public MarkupListService(MeasurementService measurement)
    {
        _measurement = measurement;
    }

    /// <summary>
    /// Filters markups by type
    /// </summary>
    public IEnumerable<MarkupRecord> FilterByType(IEnumerable<MarkupRecord> markups, MarkupType type)
    {
        return markups.Where(m => m.Type == type);
    }

    /// <summary>
    /// Filters markups by layer
    /// </summary>
    public IEnumerable<MarkupRecord> FilterByLayer(IEnumerable<MarkupRecord> markups, string layerId)
    {
        return markups.Where(m => m.LayerId == layerId);
    }

    /// <summary>
    /// Filters markups by label substring (case-insensitive)
    /// </summary>
    public IEnumerable<MarkupRecord> FilterByLabel(IEnumerable<MarkupRecord> markups, string labelSearch)
    {
        return markups.Where(m =>
            m.Metadata.Label.Contains(labelSearch, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Sorts markups by label
    /// </summary>
    public IEnumerable<MarkupRecord> SortByLabel(IEnumerable<MarkupRecord> markups, bool ascending = true)
    {
        return ascending
            ? markups.OrderBy(m => m.Metadata.Label)
            : markups.OrderByDescending(m => m.Metadata.Label);
    }

    /// <summary>
    /// Sorts markups by type
    /// </summary>
    public IEnumerable<MarkupRecord> SortByType(IEnumerable<MarkupRecord> markups, bool ascending = true)
    {
        return ascending
            ? markups.OrderBy(m => m.Type)
            : markups.OrderByDescending(m => m.Type);
    }

    /// <summary>
    /// Sorts markups by creation date
    /// </summary>
    public IEnumerable<MarkupRecord> SortByDate(IEnumerable<MarkupRecord> markups, bool ascending = true)
    {
        return ascending
            ? markups.OrderBy(m => m.Metadata.CreatedUtc)
            : markups.OrderByDescending(m => m.Metadata.CreatedUtc);
    }

    /// <summary>
    /// Exports a BOM/CSV of markup records with measurements
    /// </summary>
    public string ExportCsv(IEnumerable<MarkupRecord> markups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Type,Label,Subject,Layer,Measurement,Depth,Author,Created");

        int row = 0;
        foreach (var m in markups)
        {
            row++;
            string measurement = _measurement.GetMeasurementSummary(m);
            sb.AppendLine(
                $"\"{EscapeCsv(m.Id)}\"," +
                $"{m.Type}," +
                $"\"{EscapeCsv(m.Metadata.Label)}\"," +
                $"\"{EscapeCsv(m.Metadata.Subject)}\"," +
                $"\"{EscapeCsv(m.LayerId)}\"," +
                $"\"{EscapeCsv(measurement)}\"," +
                $"{m.Metadata.Depth:F2}," +
                $"\"{EscapeCsv(m.Metadata.Author)}\"," +
                $"{m.Metadata.CreatedUtc:O}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports a grouped BOM (quantity per type + label)
    /// </summary>
    public string ExportGroupedBom(IEnumerable<MarkupRecord> markups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Item,Type,Label,Quantity,TypicalMeasurement");

        int item = 1;
        var groups = markups.GroupBy(m => new { m.Type, m.Metadata.Label });
        foreach (var g in groups)
        {
            var first = g.First();
            string measurement = _measurement.GetMeasurementSummary(first);
            sb.AppendLine(
                $"{item}," +
                $"{g.Key.Type}," +
                $"\"{EscapeCsv(g.Key.Label)}\"," +
                $"{g.Count()}," +
                $"\"{EscapeCsv(measurement)}\"");
            item++;
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }
}

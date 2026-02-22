using System.IO;
using System.Text;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Exports a Bill of Materials (BOM) from project components to CSV
/// </summary>
public class BomExportService
{
    /// <summary>
    /// Generates BOM CSV content from a list of components
    /// </summary>
    public string GenerateBomCsv(IEnumerable<ElectricalComponent> components)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Item,Name,Type,Manufacturer,PartNumber,Material,Width,Height,Depth,Elevation,Quantity");
        
        int item = 1;
        var grouped = components
            .GroupBy(c => new { c.Type, c.Name, c.Parameters.Material, 
                c.Parameters.Manufacturer, c.Parameters.PartNumber,
                c.Parameters.Width, c.Parameters.Height, c.Parameters.Depth,
                c.Parameters.Elevation });
        
        foreach (var group in grouped)
        {
            var first = group.First();
            sb.AppendLine($"{item}," +
                $"\"{EscapeCsv(first.Name)}\"," +
                $"{first.Type}," +
                $"\"{EscapeCsv(first.Parameters.Manufacturer)}\"," +
                $"\"{EscapeCsv(first.Parameters.PartNumber)}\"," +
                $"\"{EscapeCsv(first.Parameters.Material)}\"," +
                $"{first.Parameters.Width:F2}," +
                $"{first.Parameters.Height:F2}," +
                $"{first.Parameters.Depth:F2}," +
                $"{first.Parameters.Elevation:F2}," +
                $"{group.Count()}");
            item++;
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Exports BOM to a CSV file
    /// </summary>
    public async Task ExportToCsvAsync(IEnumerable<ElectricalComponent> components, string filePath)
    {
        var csv = GenerateBomCsv(components);
        await File.WriteAllTextAsync(filePath, csv);
    }
    
    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }
}

using System.Text;
using System.Text.Json;
using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Conduit.Persistence;

/// <summary>
/// A spool package grouping conduit runs and fittings for prefabrication.
/// </summary>
public class SpoolPackage
{
    public string SpoolId { get; set; } = Guid.NewGuid().ToString();
    public string SpoolName { get; set; } = string.Empty;
    public List<string> RunIds { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Bill of materials entry for a spool.
/// </summary>
public class SpoolBomEntry
{
    public string ItemType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TradeSize { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public double TotalLengthInches { get; set; }
}

/// <summary>
/// Manages spool packages for prefabrication workflows.
/// </summary>
public class SpoolManager
{
    private readonly List<SpoolPackage> _spools = new();
    private readonly ConduitModelStore _store;
    private int _nextSpoolNumber = 1;

    public IReadOnlyList<SpoolPackage> Spools => _spools;

    public SpoolManager(ConduitModelStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates a new spool package from selected run IDs.
    /// </summary>
    public SpoolPackage CreateSpool(List<string> runIds, string? name = null)
    {
        var spool = new SpoolPackage
        {
            SpoolName = name ?? $"SP-{_nextSpoolNumber++:D3}",
            RunIds = new List<string>(runIds)
        };
        _spools.Add(spool);
        return spool;
    }

    /// <summary>
    /// Generates a BOM for a given spool package.
    /// </summary>
    public List<SpoolBomEntry> GenerateBom(SpoolPackage spool)
    {
        var bomMap = new Dictionary<string, SpoolBomEntry>();

        foreach (var runId in spool.RunIds)
        {
            var run = _store.GetRun(runId);
            if (run == null) continue;

            // Conduit segments
            foreach (var seg in run.GetSegments(_store))
            {
                string key = $"Conduit|{seg.Material}|{seg.TradeSize}";
                if (!bomMap.TryGetValue(key, out var entry))
                {
                    entry = new SpoolBomEntry
                    {
                        ItemType = "Conduit",
                        Description = $"{seg.Material} {seg.TradeSize}\" Conduit",
                        TradeSize = seg.TradeSize,
                        Material = seg.Material.ToString()
                    };
                    bomMap[key] = entry;
                }
                entry.Quantity++;
                entry.TotalLengthInches += seg.Length * 12.0;
            }

            // Fittings
            foreach (var fit in run.GetFittings(_store))
            {
                string key = $"Fitting|{fit.Type}|{fit.TradeSize}";
                if (!bomMap.TryGetValue(key, out var entry))
                {
                    entry = new SpoolBomEntry
                    {
                        ItemType = "Fitting",
                        Description = $"{fit.Type} {fit.TradeSize}\"",
                        TradeSize = fit.TradeSize,
                        Material = "Steel"
                    };
                    bomMap[key] = entry;
                }
                entry.Quantity++;
            }
        }

        return bomMap.Values.OrderBy(e => e.ItemType).ThenBy(e => e.TradeSize).ToList();
    }

    /// <summary>
    /// Exports a spool sheet (BOM + cut list) as CSV.
    /// </summary>
    public string ExportSpoolSheetCsv(SpoolPackage spool)
    {
        var bom = GenerateBom(spool);
        var sb = new StringBuilder();

        sb.AppendLine($"Spool Sheet: {spool.SpoolName}");
        sb.AppendLine($"Created: {spool.CreatedUtc:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Runs: {string.Join(", ", spool.RunIds)}");
        sb.AppendLine();
        sb.AppendLine("Item,Description,TradeSize,Material,Qty,TotalLength(in)");

        int item = 1;
        foreach (var entry in bom)
        {
            sb.AppendLine(
                $"{item}," +
                $"\"{entry.Description}\"," +
                $"\"{entry.TradeSize}\"," +
                $"\"{entry.Material}\"," +
                $"{entry.Quantity}," +
                $"{entry.TotalLengthInches:F2}");
            item++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Serializes all spool packages to JSON.
    /// </summary>
    public string SerializeToJson()
    {
        return JsonSerializer.Serialize(_spools, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Loads spool packages from JSON.
    /// </summary>
    public void LoadFromJson(string json)
    {
        var spools = JsonSerializer.Deserialize<List<SpoolPackage>>(json);
        if (spools != null)
        {
            _spools.Clear();
            _spools.AddRange(spools);
        }
    }
}

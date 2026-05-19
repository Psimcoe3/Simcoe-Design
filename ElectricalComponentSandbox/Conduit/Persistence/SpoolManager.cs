using System.Text;
using System.Text.Json;
using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Conduit.Persistence;

/// <summary>
/// Lifecycle states for a spool package — mirrors the eVolve
/// <c>eV_PackageStatus</c> values used to track prefab from shop to field.
/// </summary>
public enum SpoolPackageStatus
{
    /// <summary>Authored in the model; not yet released to the shop.</summary>
    Drawn,

    /// <summary>Released for fabrication; locked from authoring edits.</summary>
    Released,

    /// <summary>On the shop floor being fabricated.</summary>
    InFab,

    /// <summary>Built and shipped to the jobsite.</summary>
    Shipped,

    /// <summary>Installed in the field; record-drawing eligible.</summary>
    Installed,
}

/// <summary>
/// Audit log entry recording a single status transition for a spool package.
/// </summary>
public sealed record SpoolPackageStatusChange(
    SpoolPackageStatus From,
    SpoolPackageStatus To,
    DateTime ChangedUtc,
    string ChangedBy,
    string? Reason);

/// <summary>
/// A spool package grouping conduit runs and fittings for prefabrication.
/// Carries lifecycle state (Drawn → Released → InFab → Shipped → Installed)
/// and an audit log of transitions so a release is traceable from the field
/// back to the model.
/// </summary>
public class SpoolPackage
{
    public string SpoolId { get; set; } = Guid.NewGuid().ToString();
    public string SpoolName { get; set; } = string.Empty;
    public List<string> RunIds { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;

    /// <summary>Current lifecycle state.</summary>
    public SpoolPackageStatus Status { get; set; } = SpoolPackageStatus.Drawn;

    /// <summary>Audit log of every status transition recorded for this package.</summary>
    public List<SpoolPackageStatusChange> StatusHistory { get; set; } = new();

    /// <summary>
    /// When the package was first released to the shop. Null until the
    /// package has been transitioned past <see cref="SpoolPackageStatus.Drawn"/>.
    /// </summary>
    public DateTime? ReleasedUtc { get; set; }

    /// <summary>User who released the package.</summary>
    public string? ReleasedBy { get; set; }

    /// <summary>
    /// Cached spool sheet artifacts (one per run). Populated by
    /// <see cref="SpoolManager.BuildSheets"/> and persisted with the package so
    /// re-opening a project shows the same shop drawing without re-deriving it.
    /// </summary>
    public List<SpoolSheet> Sheets { get; set; } = new();
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

    // ── Lifecycle transitions ────────────────────────────────────────────

    /// <summary>
    /// Transitions a spool package to a new lifecycle state, appending an
    /// entry to the audit log. Transitions are validated against the
    /// release order so a package can't skip backwards by accident; pass
    /// <paramref name="allowBackwards"/> to override (e.g. a release was
    /// pulled back to drafting).
    /// </summary>
    public SpoolPackageStatusChange TransitionStatus(
        string spoolId,
        SpoolPackageStatus toStatus,
        string? changedBy = null,
        string? reason = null,
        bool allowBackwards = false)
    {
        var spool = _spools.FirstOrDefault(s => s.SpoolId == spoolId)
            ?? throw new ArgumentException($"Spool '{spoolId}' not found.", nameof(spoolId));

        if (spool.Status == toStatus)
            throw new InvalidOperationException($"Spool '{spool.SpoolName}' is already {toStatus}.");

        if (!allowBackwards && toStatus < spool.Status)
            throw new InvalidOperationException(
                $"Cannot move spool '{spool.SpoolName}' from {spool.Status} back to {toStatus}. " +
                "Pass allowBackwards=true to override.");

        var entry = new SpoolPackageStatusChange(
            From: spool.Status,
            To: toStatus,
            ChangedUtc: DateTime.UtcNow,
            ChangedBy: changedBy ?? Environment.UserName,
            Reason: reason);

        spool.StatusHistory.Add(entry);
        spool.Status = toStatus;

        if (toStatus == SpoolPackageStatus.Released && spool.ReleasedUtc == null)
        {
            spool.ReleasedUtc = entry.ChangedUtc;
            spool.ReleasedBy = entry.ChangedBy;
        }

        return entry;
    }

    // ── Spool sheet building ─────────────────────────────────────────────

    /// <summary>
    /// Builds the per-run spool sheets for a package and caches them on the
    /// package's <see cref="SpoolPackage.Sheets"/> list. Replaces any
    /// previously cached sheets so the result reflects the current model
    /// state. <paramref name="hangerSelector"/> can supply hangers per run
    /// for the hanger schedule; if null, hanger sections render empty.
    /// </summary>
    public IReadOnlyList<SpoolSheet> BuildSheets(
        string spoolId,
        Func<string, IReadOnlyList<Models.HangerComponent>>? hangerSelector = null,
        SpoolSheetTitleBlock? titleTemplate = null)
    {
        var spool = _spools.FirstOrDefault(s => s.SpoolId == spoolId)
            ?? throw new ArgumentException($"Spool '{spoolId}' not found.", nameof(spoolId));

        var builder = new SpoolSheetBuilder(_store);
        var sheets = new List<SpoolSheet>(spool.RunIds.Count);
        int index = 1;
        foreach (var runId in spool.RunIds)
        {
            var run = _store.GetAllRuns().FirstOrDefault(r => r.Id == runId || r.RunId == runId);
            if (run == null) continue;

            var hangers = hangerSelector?.Invoke(run.Id) ?? Array.Empty<Models.HangerComponent>();
            var title = MergeTitleBlock(titleTemplate, spool, run, index, spool.RunIds.Count);
            var sheet = builder.Build(run.Id, hangers, title);
            sheets.Add(sheet);
            index++;
        }

        spool.Sheets.Clear();
        spool.Sheets.AddRange(sheets);
        return sheets;
    }

    private static SpoolSheetTitleBlock MergeTitleBlock(
        SpoolSheetTitleBlock? template,
        SpoolPackage spool,
        ConduitRun run,
        int index,
        int count)
    {
        return new SpoolSheetTitleBlock
        {
            ProjectName = template?.ProjectName ?? string.Empty,
            ProjectNumber = template?.ProjectNumber ?? string.Empty,
            SheetNumber = template?.SheetNumber ?? $"{spool.SpoolName}-{index:D2}",
            SheetTitle = template?.SheetTitle ?? $"Spool {spool.SpoolName} — {run.RunId}",
            DrawnBy = template?.DrawnBy ?? Environment.UserName,
            DrawnDateUtc = template?.DrawnDateUtc ?? DateTime.UtcNow,
            DrawingScale = template?.DrawingScale ?? "NTS",
            SpoolPackage = $"{spool.SpoolName}  ({index} of {count})",
            Status = template?.Status ?? spool.Status.ToString().ToUpperInvariant(),
        };
    }
}

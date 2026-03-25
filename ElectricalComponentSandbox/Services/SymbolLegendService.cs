using System.Windows;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Represents a single entry in the symbol legend.
/// </summary>
public class LegendEntry
{
    public string SymbolName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Reference to the symbol definition from the library, if one exists.</summary>
    public SymbolDefinition? SymbolDefinition { get; set; }

    /// <summary>Component type for entries derived from placed components (null for symbol-only entries).</summary>
    public ComponentType? ComponentType { get; set; }

    /// <summary>Number of instances placed in the drawing.</summary>
    public int Count { get; set; }
}

/// <summary>
/// A generated symbol legend table that lists every unique symbol / component
/// present in the drawing along with its description and count.
/// </summary>
public class SymbolLegend
{
    public string Title { get; set; } = "SYMBOL LEGEND";
    public List<LegendEntry> Entries { get; set; } = new();

    public double SymbolColumnWidth { get; set; } = 40;
    public double NameColumnWidth { get; set; } = 120;
    public double DescriptionColumnWidth { get; set; } = 200;
    public double RowHeight { get; set; } = 25;
    public double TitleHeight { get; set; } = 30;

    /// <summary>Total width computed from column widths.</summary>
    public double TotalWidth => SymbolColumnWidth + NameColumnWidth + DescriptionColumnWidth;

    /// <summary>Total height including title row, header row, and all entry rows.</summary>
    public double TotalHeight => TitleHeight + RowHeight + (Entries.Count * RowHeight);
}

/// <summary>
/// Auto-generates a symbol legend from placed components and symbols in the drawing.
/// The legend lists each unique symbol with its name, description, and placement count,
/// sorted by category then name.
/// </summary>
public class SymbolLegendService
{
    /// <summary>
    /// Maps from visual-profile string to symbol definition name where a direct
    /// correspondence exists between a component profile and a library symbol.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ProfileToSymbolName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ElectricalComponentCatalog.Profiles.PanelDistribution] = "Panel Board",
            [ElectricalComponentCatalog.Profiles.PanelLighting] = "Panel Board",
            [ElectricalComponentCatalog.Profiles.PanelSwitchboard] = "Panel Board",
            [ElectricalComponentCatalog.Profiles.PanelMcc] = "Motor",
            [ElectricalComponentCatalog.Profiles.BoxDisconnectSwitch] = "Disconnect Switch",
            [ElectricalComponentCatalog.Profiles.BoxFloor] = "Floor Receptacle",
        };

    /// <summary>
    /// Generates a symbol legend from the placed components in the drawing.
    /// Collects unique component types and their visual profiles, maps profiles to
    /// symbol definitions where possible, counts placements, and sorts by category then name.
    /// </summary>
    public SymbolLegend GenerateLegend(
        IReadOnlyList<ElectricalComponent> components,
        ElectricalSymbolLibrary library)
    {
        var legend = new SymbolLegend();

        if (components.Count == 0)
            return legend;

        // Group components by their resolved visual profile so each unique profile
        // produces exactly one legend row.
        var groups = components
            .GroupBy(c => ElectricalComponentCatalog.GetProfile(c), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groups)
        {
            var profile = group.Key;
            var representative = group.First();

            // Attempt to find a matching symbol definition in the library.
            SymbolDefinition? symbol = ResolveSymbol(profile, representative, library);

            var entry = new LegendEntry
            {
                SymbolName = symbol?.Name ?? representative.Name,
                Category = symbol?.Category ?? representative.Type.ToString(),
                Description = symbol?.Description ?? BuildFallbackDescription(representative),
                SymbolDefinition = symbol,
                ComponentType = representative.Type,
                Count = group.Count()
            };

            legend.Entries.Add(entry);
        }

        // Sort by category then by name for a clean, grouped presentation.
        legend.Entries = legend.Entries
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.SymbolName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return legend;
    }

    /// <summary>
    /// Creates a symbol legend from an explicit list of symbol names drawn from the library.
    /// Symbols that do not exist in the library are silently skipped.
    /// </summary>
    public SymbolLegend GenerateFromSymbolNames(
        IEnumerable<string> symbolNames,
        ElectricalSymbolLibrary library)
    {
        var legend = new SymbolLegend();

        foreach (var name in symbolNames)
        {
            var symbol = library.GetSymbol(name);
            if (symbol is null)
                continue;

            legend.Entries.Add(new LegendEntry
            {
                SymbolName = symbol.Name,
                Category = symbol.Category,
                Description = symbol.Description,
                SymbolDefinition = symbol,
                ComponentType = null,
                Count = 0
            });
        }

        // Deduplicate in case the caller passed the same name more than once.
        legend.Entries = legend.Entries
            .GroupBy(e => e.SymbolName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.SymbolName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return legend;
    }

    /// <summary>
    /// Converts a <see cref="SymbolLegend"/> to a <see cref="ScheduleTable"/> so it can
    /// be rendered using the same table-drawing infrastructure as panel / equipment schedules.
    /// Columns: Symbol, Name, Description, Count.
    /// </summary>
    public ScheduleTable ToScheduleTable(SymbolLegend legend)
    {
        var table = new ScheduleTable
        {
            Title = legend.Title,
            TitleHeight = legend.TitleHeight,
            RowHeight = legend.RowHeight,
            Columns =
            {
                new ScheduleColumn
                {
                    Header = "SYMBOL",
                    Width = legend.SymbolColumnWidth,
                    Alignment = HorizontalAlignment.Center
                },
                new ScheduleColumn
                {
                    Header = "NAME",
                    Width = legend.NameColumnWidth,
                    Alignment = HorizontalAlignment.Left
                },
                new ScheduleColumn
                {
                    Header = "DESCRIPTION",
                    Width = legend.DescriptionColumnWidth,
                    Alignment = HorizontalAlignment.Left
                },
                new ScheduleColumn
                {
                    Header = "COUNT",
                    Width = 50,
                    Alignment = HorizontalAlignment.Center
                }
            }
        };

        foreach (var entry in legend.Entries)
        {
            table.Rows.Add(new[]
            {
                entry.SymbolDefinition?.Name ?? "-",
                entry.SymbolName,
                entry.Description,
                entry.Count > 0 ? entry.Count.ToString() : "-"
            });
        }

        return table;
    }

    // ──────────────────────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to resolve a <see cref="SymbolDefinition"/> for a given component profile.
    /// First checks the explicit profile-to-symbol mapping, then falls back to a
    /// name-based lookup in the library.
    /// </summary>
    private static SymbolDefinition? ResolveSymbol(
        string profile,
        ElectricalComponent component,
        ElectricalSymbolLibrary library)
    {
        // 1. Explicit mapping from visual profile to known symbol name.
        if (ProfileToSymbolName.TryGetValue(profile, out var mappedName))
        {
            var mapped = library.GetSymbol(mappedName);
            if (mapped is not null)
                return mapped;
        }

        // 2. Try to match the component name directly against the symbol library.
        var byName = library.GetSymbol(component.Name);
        if (byName is not null)
            return byName;

        // 3. Try to match the component type name (e.g. "Panel" -> "Panel Board").
        var byType = library.GetSymbol(component.Type.ToString());
        if (byType is not null)
            return byType;

        return null;
    }

    /// <summary>
    /// Builds a human-readable description when no symbol definition is available.
    /// </summary>
    private static string BuildFallbackDescription(ElectricalComponent component)
    {
        var material = component.Parameters.Material;
        var typeName = component.Type.ToString();
        return string.IsNullOrWhiteSpace(material)
            ? typeName
            : $"{typeName} ({material})";
    }
}

using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Manages saved layer state snapshots (AutoCAD Layer States Manager equivalent).
/// Captures and restores layer visibility, freeze, lock, color, line type, and line weight.
/// </summary>
public class LayerStateService
{
    private readonly Dictionary<string, LayerStateSnapshot> _states = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, LayerStateSnapshot> SavedStates => _states;

    /// <summary>
    /// Captures the current state of all layers into a named snapshot.
    /// Overwrites if the name already exists.
    /// </summary>
    public LayerStateSnapshot SaveState(string name, IReadOnlyList<Layer> layers)
    {
        var snapshot = new LayerStateSnapshot
        {
            Name = name,
            SavedAt = DateTime.UtcNow,
            LayerEntries = layers.Select(l => new LayerStateEntry
            {
                LayerId = l.Id,
                LayerName = l.Name,
                IsVisible = l.IsVisible,
                IsFrozen = l.IsFrozen,
                IsLocked = l.IsLocked,
                IsPlotted = l.IsPlotted,
                Color = l.Color,
                LineType = l.LineType,
                LineWeight = l.LineWeight
            }).ToList()
        };

        _states[name] = snapshot;
        return snapshot;
    }

    /// <summary>
    /// Restores a named layer state snapshot onto the given layers.
    /// Matches by LayerId, falls back to LayerName.
    /// Returns the layers that were modified.
    /// </summary>
    public List<Layer> RestoreState(string name, IList<Layer> layers)
    {
        if (!_states.TryGetValue(name, out var snapshot))
            return new List<Layer>();

        var modified = new List<Layer>();

        foreach (var entry in snapshot.LayerEntries)
        {
            var layer = FindLayer(layers, entry);
            if (layer == null) continue;

            layer.IsVisible = entry.IsVisible;
            layer.IsFrozen = entry.IsFrozen;
            layer.IsLocked = entry.IsLocked;
            layer.IsPlotted = entry.IsPlotted;
            layer.Color = entry.Color;
            layer.LineType = entry.LineType;
            layer.LineWeight = entry.LineWeight;
            modified.Add(layer);
        }

        return modified;
    }

    /// <summary>
    /// Deletes a named layer state.
    /// </summary>
    public bool DeleteState(string name) => _states.Remove(name);

    /// <summary>
    /// Renames a layer state.
    /// </summary>
    public bool RenameState(string oldName, string newName)
    {
        if (!_states.Remove(oldName, out var snapshot)) return false;
        snapshot.Name = newName;
        _states[newName] = snapshot;
        return true;
    }

    /// <summary>
    /// Returns a list of all saved state names.
    /// </summary>
    public List<string> GetStateNames() => _states.Keys.OrderBy(k => k).ToList();

    /// <summary>
    /// Creates common preset layer states for electrical drawings.
    /// </summary>
    public void CreatePresets(IReadOnlyList<Layer> layers)
    {
        // "All On" - everything visible, unfrozen
        var allOn = layers.Select(l => new LayerStateEntry
        {
            LayerId = l.Id, LayerName = l.Name,
            IsVisible = true, IsFrozen = false, IsLocked = l.IsLocked,
            IsPlotted = l.IsPlotted, Color = l.Color,
            LineType = l.LineType, LineWeight = l.LineWeight
        }).ToList();
        _states["All On"] = new LayerStateSnapshot
        {
            Name = "All On", SavedAt = DateTime.UtcNow, LayerEntries = allOn
        };

        // "All Off" - everything hidden
        var allOff = layers.Select(l => new LayerStateEntry
        {
            LayerId = l.Id, LayerName = l.Name,
            IsVisible = false, IsFrozen = false, IsLocked = l.IsLocked,
            IsPlotted = l.IsPlotted, Color = l.Color,
            LineType = l.LineType, LineWeight = l.LineWeight
        }).ToList();
        _states["All Off"] = new LayerStateSnapshot
        {
            Name = "All Off", SavedAt = DateTime.UtcNow, LayerEntries = allOff
        };

        // "Electrical Only" - only E- prefixed layers visible
        var elecOnly = layers.Select(l => new LayerStateEntry
        {
            LayerId = l.Id, LayerName = l.Name,
            IsVisible = l.Name.StartsWith("E-", StringComparison.OrdinalIgnoreCase)
                     || l.Name.Equals("Default", StringComparison.OrdinalIgnoreCase),
            IsFrozen = false, IsLocked = l.IsLocked,
            IsPlotted = l.IsPlotted, Color = l.Color,
            LineType = l.LineType, LineWeight = l.LineWeight
        }).ToList();
        _states["Electrical Only"] = new LayerStateSnapshot
        {
            Name = "Electrical Only", SavedAt = DateTime.UtcNow, LayerEntries = elecOnly
        };

        // "Print Ready" - non-plotted layers frozen
        var printReady = layers.Select(l => new LayerStateEntry
        {
            LayerId = l.Id, LayerName = l.Name,
            IsVisible = l.IsPlotted, IsFrozen = !l.IsPlotted,
            IsLocked = l.IsLocked, IsPlotted = l.IsPlotted,
            Color = l.Color, LineType = l.LineType, LineWeight = l.LineWeight
        }).ToList();
        _states["Print Ready"] = new LayerStateSnapshot
        {
            Name = "Print Ready", SavedAt = DateTime.UtcNow, LayerEntries = printReady
        };
    }

    private static Layer? FindLayer(IList<Layer> layers, LayerStateEntry entry)
    {
        // Match by Id first, then by Name
        foreach (var l in layers)
        {
            if (l.Id == entry.LayerId) return l;
        }
        foreach (var l in layers)
        {
            if (string.Equals(l.Name, entry.LayerName, StringComparison.OrdinalIgnoreCase))
                return l;
        }
        return null;
    }
}

/// <summary>
/// A snapshot of all layer states at a point in time.
/// </summary>
public class LayerStateSnapshot
{
    public string Name { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; }
    public List<LayerStateEntry> LayerEntries { get; set; } = new();
}

/// <summary>
/// The saved state of a single layer.
/// </summary>
public class LayerStateEntry
{
    public string LayerId { get; set; } = string.Empty;
    public string LayerName { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public bool IsFrozen { get; set; }
    public bool IsLocked { get; set; }
    public bool IsPlotted { get; set; } = true;
    public string Color { get; set; } = "#808080";
    public LineType LineType { get; set; } = LineType.Continuous;
    public double LineWeight { get; set; }
}

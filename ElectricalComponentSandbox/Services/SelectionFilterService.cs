using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides AutoCAD-style selection filtering (Quick Select, Select Similar,
/// Select by Layer/Type/Property).
/// </summary>
public class SelectionFilterService
{
    /// <summary>
    /// Filters components by layer ID.
    /// </summary>
    public IReadOnlyList<ElectricalComponent> SelectByLayer(
        IEnumerable<ElectricalComponent> components, string layerId)
    {
        return components
            .Where(c => string.Equals(c.LayerId, layerId, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Filters components by component type.
    /// </summary>
    public IReadOnlyList<ElectricalComponent> SelectByType(
        IEnumerable<ElectricalComponent> components, ComponentType type)
    {
        return components.Where(c => c.Type == type).ToList();
    }

    /// <summary>
    /// Selects components similar to a reference component (same type, same layer).
    /// </summary>
    public IReadOnlyList<ElectricalComponent> SelectSimilar(
        IEnumerable<ElectricalComponent> components, ElectricalComponent reference)
    {
        return components
            .Where(c => c.Type == reference.Type &&
                        string.Equals(c.LayerId, reference.LayerId, StringComparison.Ordinal) &&
                        !string.Equals(c.Id, reference.Id, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// AutoCAD-style Quick Select: filters by multiple criteria.
    /// All criteria must match (AND logic).
    /// </summary>
    public IReadOnlyList<ElectricalComponent> QuickSelect(
        IEnumerable<ElectricalComponent> components, SelectionCriteria criteria)
    {
        var query = components.AsEnumerable();

        if (criteria.ComponentType.HasValue)
            query = query.Where(c => c.Type == criteria.ComponentType.Value);

        if (!string.IsNullOrEmpty(criteria.LayerId))
            query = query.Where(c => string.Equals(c.LayerId, criteria.LayerId, StringComparison.Ordinal));

        if (!string.IsNullOrEmpty(criteria.NameContains))
            query = query.Where(c => c.Name.Contains(criteria.NameContains, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(criteria.Material))
            query = query.Where(c => string.Equals(c.Parameters.Material, criteria.Material, StringComparison.OrdinalIgnoreCase));

        if (criteria.MinElevation.HasValue)
            query = query.Where(c => c.Parameters.Elevation >= criteria.MinElevation.Value);

        if (criteria.MaxElevation.HasValue)
            query = query.Where(c => c.Parameters.Elevation <= criteria.MaxElevation.Value);

        if (!string.IsNullOrEmpty(criteria.Manufacturer))
            query = query.Where(c => string.Equals(c.Parameters.Manufacturer, criteria.Manufacturer, StringComparison.OrdinalIgnoreCase));

        return query.ToList();
    }

    /// <summary>
    /// Applies a bulk property change to a set of components, returning an undo action.
    /// </summary>
    public BulkPropertyChangeResult ApplyBulkPropertyChange(
        IReadOnlyList<ElectricalComponent> components, BulkPropertyChange change)
    {
        var actions = new List<(ElectricalComponent Component, string Property, object? OldValue)>();

        foreach (var c in components)
        {
            if (change.LayerId != null)
            {
                actions.Add((c, "LayerId", c.LayerId));
                c.LayerId = change.LayerId;
            }

            if (change.Elevation.HasValue)
            {
                actions.Add((c, "Elevation", c.Parameters.Elevation));
                c.Parameters.Elevation = change.Elevation.Value;
            }

            if (change.Material != null)
            {
                actions.Add((c, "Material", c.Parameters.Material));
                c.Parameters.Material = change.Material;
            }

            if (change.Color != null)
            {
                actions.Add((c, "Color", c.Parameters.Color));
                c.Parameters.Color = change.Color;
            }

            if (change.LineWeight.HasValue)
            {
                actions.Add((c, "LineWeightOverride", c.Parameters.LineWeightOverride));
                c.Parameters.LineWeightOverride = change.LineWeight;
            }
        }

        return new BulkPropertyChangeResult
        {
            AffectedCount = components.Count,
            PropertyChanges = actions
        };
    }

    /// <summary>
    /// Reverts a bulk property change.
    /// </summary>
    public void RevertBulkPropertyChange(BulkPropertyChangeResult result)
    {
        foreach (var (component, property, oldValue) in result.PropertyChanges)
        {
            switch (property)
            {
                case "LayerId": component.LayerId = (string)oldValue!; break;
                case "Elevation": component.Parameters.Elevation = (double)oldValue!; break;
                case "Material": component.Parameters.Material = (string)oldValue!; break;
                case "Color": component.Parameters.Color = (string)oldValue!; break;
                case "LineWeightOverride": component.Parameters.LineWeightOverride = (double?)oldValue; break;
            }
        }
    }
}

/// <summary>
/// Criteria for Quick Select filtering.
/// </summary>
public class SelectionCriteria
{
    public ComponentType? ComponentType { get; set; }
    public string? LayerId { get; set; }
    public string? NameContains { get; set; }
    public string? Material { get; set; }
    public double? MinElevation { get; set; }
    public double? MaxElevation { get; set; }
    public string? Manufacturer { get; set; }
}

/// <summary>
/// Properties to apply in a bulk edit operation.
/// Null values are not applied.
/// </summary>
public class BulkPropertyChange
{
    public string? LayerId { get; set; }
    public double? Elevation { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public double? LineWeight { get; set; }
}

public class BulkPropertyChangeResult
{
    public int AffectedCount { get; init; }
    public List<(ElectricalComponent Component, string Property, object? OldValue)> PropertyChanges { get; init; } = new();
}

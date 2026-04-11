using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Resolves component family types and applies parameter inheritance.
/// Mirrors the Revit pattern: Family → FamilySymbol (type) → FamilyInstance,
/// where each instance inherits type parameters but can override per-instance.
/// </summary>
public class FamilyTypeService
{
    /// <summary>
    /// Resolves the effective parameter value for a component instance.
    /// Resolution order: instance value → type override → default.
    /// </summary>
    public static string ResolveParameter(
        ElectricalComponent instance,
        string parameterName,
        IEnumerable<ComponentFamily> families)
    {
        // 1. Check if instance already has a non-default value (per-instance override)
        string? instanceValue = GetParameterValue(instance.Parameters, parameterName);

        // 2. Look up the family type
        ComponentFamilyType? familyType = null;
        if (instance.FamilyTypeId != null)
            familyType = FindType(instance.FamilyTypeId, families);

        string? typeValue = null;
        if (familyType != null && familyType.ParameterOverrides.TryGetValue(parameterName, out var ov))
            typeValue = ov;

        // Instance value takes precedence if it differs from default
        if (instanceValue != null)
            return instanceValue;

        return typeValue ?? string.Empty;
    }

    /// <summary>
    /// Applies type parameter overrides to a component instance's parameters.
    /// Only sets values where the instance does not already have a non-default override.
    /// </summary>
    public static void ApplyTypeDefaults(
        ElectricalComponent instance,
        IEnumerable<ComponentFamily> families)
    {
        if (instance.FamilyTypeId == null) return;

        var familyType = FindType(instance.FamilyTypeId, families);
        if (familyType == null) return;

        foreach (var (key, value) in familyType.ParameterOverrides)
        {
            SetParameterIfDefault(instance.Parameters, key, value);
        }
    }

    /// <summary>
    /// Checks whether a family type is in use by any component in the project.
    /// Used to guard against deleting types that have placed instances.
    /// </summary>
    public static bool IsTypeInUse(string typeId, IEnumerable<ElectricalComponent> components)
    {
        return components.Any(c => c.FamilyTypeId == typeId);
    }

    /// <summary>
    /// Finds a <see cref="ComponentFamilyType"/> by ID across all families.
    /// </summary>
    public static ComponentFamilyType? FindType(string typeId, IEnumerable<ComponentFamily> families)
    {
        foreach (var family in families)
        {
            var type = family.Types.FirstOrDefault(t => t.Id == typeId);
            if (type != null) return type;
        }
        return null;
    }

    /// <summary>
    /// Finds the <see cref="ComponentFamily"/> that contains a given type ID.
    /// </summary>
    public static ComponentFamily? FindFamilyForType(string typeId, IEnumerable<ComponentFamily> families)
    {
        return families.FirstOrDefault(f => f.Types.Any(t => t.Id == typeId));
    }

    /// <summary>
    /// Gets the connector definitions for a component's assigned family type.
    /// Returns an empty list when the component has no type assignment or no connectors.
    /// </summary>
    public static List<ConnectorDefinition> GetConnectors(
        ElectricalComponent instance,
        IEnumerable<ComponentFamily> families)
    {
        if (instance.FamilyTypeId == null) return new();
        var familyType = FindType(instance.FamilyTypeId, families);
        return familyType?.Connectors ?? new();
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private static string? GetParameterValue(ComponentParameters p, string name)
    {
        return name switch
        {
            "Width" when p.Width != 1.0 => p.Width.ToString(),
            "Height" when p.Height != 1.0 => p.Height.ToString(),
            "Depth" when p.Depth != 1.0 => p.Depth.ToString(),
            "Material" when p.Material != "Steel" => p.Material,
            "Elevation" when p.Elevation != 0.0 => p.Elevation.ToString(),
            "Color" when p.Color != "#808080" => p.Color,
            "Manufacturer" when !string.IsNullOrEmpty(p.Manufacturer) => p.Manufacturer,
            "PartNumber" when !string.IsNullOrEmpty(p.PartNumber) => p.PartNumber,
            _ => null
        };
    }

    private static void SetParameterIfDefault(ComponentParameters p, string name, string value)
    {
        switch (name)
        {
            case "Width" when p.Width == 1.0 && double.TryParse(value, out var w):
                p.Width = w;
                break;
            case "Height" when p.Height == 1.0 && double.TryParse(value, out var h):
                p.Height = h;
                break;
            case "Depth" when p.Depth == 1.0 && double.TryParse(value, out var d):
                p.Depth = d;
                break;
            case "Material" when p.Material == "Steel":
                p.Material = value;
                break;
            case "Elevation" when p.Elevation == 0.0 && double.TryParse(value, out var e):
                p.Elevation = e;
                break;
            case "Color" when p.Color == "#808080":
                p.Color = value;
                break;
            case "Manufacturer" when string.IsNullOrEmpty(p.Manufacturer):
                p.Manufacturer = value;
                break;
            case "PartNumber" when string.IsNullOrEmpty(p.PartNumber):
                p.PartNumber = value;
                break;
        }
    }
}

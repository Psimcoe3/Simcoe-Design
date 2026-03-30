using System;
using System.Windows.Media.Media3D;
using Newtonsoft.Json;

namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Base class for all electrical components
/// </summary>
public abstract class ElectricalComponent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public ComponentType Type { get; set; }
    public string VisualProfile { get; set; } = string.Empty;
    
    // Transformation properties
    public Point3D Position { get; set; }
    public Vector3D Rotation { get; set; }
    public Vector3D Scale { get; set; } = new Vector3D(1, 1, 1);
    
    // Parameters
    public ComponentParameters Parameters { get; set; } = new();
    
    // Constraints
    public List<string> Constraints { get; set; } = new();
    
    // Layer assignment
    public string LayerId { get; set; } = "default";
}

public enum ComponentType
{
    Conduit,
    Box,
    Panel,
    Support,
    CableTray,
    Hanger
}

public class ComponentParameters
{
    public double Width { get; set; } = 1.0;
    public double Height { get; set; } = 1.0;
    public double Depth { get; set; } = 1.0;
    public double? CatalogWidth { get; set; }
    public double? CatalogHeight { get; set; }
    public double? CatalogDepth { get; set; }
    public string Material { get; set; } = "Steel";
    public double Elevation { get; set; } = 0.0;
    public string Color { get; set; } = "#808080";
    public string Manufacturer { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string ReferenceUrl { get; set; } = string.Empty;
    public ComponentParameterBindings Bindings { get; set; } = new();

    public string? GetBinding(ProjectParameterBindingTarget target)
    {
        return target switch
        {
            ProjectParameterBindingTarget.Width => Bindings.WidthParameterId,
            ProjectParameterBindingTarget.Height => Bindings.HeightParameterId,
            ProjectParameterBindingTarget.Depth => Bindings.DepthParameterId,
            ProjectParameterBindingTarget.Elevation => Bindings.ElevationParameterId,
            _ => null
        };
    }

    public void SetBinding(ProjectParameterBindingTarget target, string? parameterId)
    {
        switch (target)
        {
            case ProjectParameterBindingTarget.Width:
                Bindings.WidthParameterId = parameterId;
                break;
            case ProjectParameterBindingTarget.Height:
                Bindings.HeightParameterId = parameterId;
                break;
            case ProjectParameterBindingTarget.Depth:
                Bindings.DepthParameterId = parameterId;
                break;
            case ProjectParameterBindingTarget.Elevation:
                Bindings.ElevationParameterId = parameterId;
                break;
        }
    }

    public void ClearBindingReference(string parameterId)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
            return;

        if (string.Equals(Bindings.WidthParameterId, parameterId, StringComparison.Ordinal))
            Bindings.WidthParameterId = null;
        if (string.Equals(Bindings.HeightParameterId, parameterId, StringComparison.Ordinal))
            Bindings.HeightParameterId = null;
        if (string.Equals(Bindings.DepthParameterId, parameterId, StringComparison.Ordinal))
            Bindings.DepthParameterId = null;
        if (string.Equals(Bindings.ElevationParameterId, parameterId, StringComparison.Ordinal))
            Bindings.ElevationParameterId = null;
    }

    // ── Per-object CAD property overrides (null = inherit from layer) ──────────

    /// <summary>Override line weight in points.  Null = inherit from layer.</summary>
    public double? LineWeightOverride { get; set; }

    /// <summary>Override line type.  Null = inherit from layer.</summary>
    public LineType? LineTypeOverride { get; set; }

    /// <summary>Override display color (hex).  Null = inherit from layer.</summary>
    public string? ColorOverride { get; set; }
}

public class ComponentParameterBindings
{
    public string? WidthParameterId { get; set; }
    public string? HeightParameterId { get; set; }
    public string? DepthParameterId { get; set; }
    public string? ElevationParameterId { get; set; }

    [JsonIgnore]
    public bool HasAnyBinding =>
        !string.IsNullOrWhiteSpace(WidthParameterId) ||
        !string.IsNullOrWhiteSpace(HeightParameterId) ||
        !string.IsNullOrWhiteSpace(DepthParameterId) ||
        !string.IsNullOrWhiteSpace(ElevationParameterId);
}

using System;
using System.Windows.Media.Media3D;
using ElectricalComponentSandbox.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Base class for all electrical components
/// </summary>
public abstract class ElectricalComponent
{
    private ComponentInteropMetadata _interopMetadata = new();
    private ComponentProtectionSettings _protectionSettings = new();

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public ComponentType Type { get; set; }
    public string VisualProfile { get; set; } = string.Empty;

    /// <summary>
    /// ID of the <see cref="ComponentFamilyType"/> this instance was placed from.
    /// Null for legacy components or when no family type catalog is in use.
    /// </summary>
    public string? FamilyTypeId { get; set; }

    /// <summary>
    /// Electrical connectors on this placed instance.
    /// Populated from <see cref="ConnectorDefinition"/> templates in the family type.
    /// </summary>
    public ElectricalConnectorManager? ElectricalConnectors { get; set; }
    
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

    public ComponentInteropMetadata InteropMetadata
    {
        get => _interopMetadata;
        set => _interopMetadata = value ?? new ComponentInteropMetadata();
    }

    public ComponentProtectionSettings ProtectionSettings
    {
        get => _protectionSettings;
        set => _protectionSettings = value ?? new ComponentProtectionSettings();
    }
}

public class ComponentInteropMetadata
{
    public string SourceSystem { get; set; } = string.Empty;
    public string SourceDocumentId { get; set; } = string.Empty;
    public string SourceDocumentName { get; set; } = string.Empty;
    public string SourceElementId { get; set; } = string.Empty;
    public string SourceFamilyName { get; set; } = string.Empty;
    public string SourceTypeName { get; set; } = string.Empty;
    public string LastInterchangeFormat { get; set; } = string.Empty;
    public DateTime? LastImportedUtc { get; set; }
    public DateTime? LastExportedUtc { get; set; }
    [JsonConverter(typeof(StringEnumConverter))]
    public ComponentInteropReviewStatus ReviewStatus { get; set; } = ComponentInteropReviewStatus.Unreviewed;
    public string ReviewedBy { get; set; } = string.Empty;
    public string ReviewNote { get; set; } = string.Empty;
    public DateTime? LastReviewedUtc { get; set; }

    [JsonIgnore]
    public bool HasAnyValue =>
        !string.IsNullOrWhiteSpace(SourceSystem) ||
        !string.IsNullOrWhiteSpace(SourceDocumentId) ||
        !string.IsNullOrWhiteSpace(SourceDocumentName) ||
        !string.IsNullOrWhiteSpace(SourceElementId) ||
        !string.IsNullOrWhiteSpace(SourceFamilyName) ||
        !string.IsNullOrWhiteSpace(SourceTypeName) ||
        !string.IsNullOrWhiteSpace(LastInterchangeFormat) ||
        ReviewStatus != ComponentInteropReviewStatus.Unreviewed ||
        !string.IsNullOrWhiteSpace(ReviewedBy) ||
        !string.IsNullOrWhiteSpace(ReviewNote) ||
        LastReviewedUtc.HasValue ||
        LastImportedUtc.HasValue ||
        LastExportedUtc.HasValue;
}

public class ComponentProtectionSettings
{
    private StoredProtectiveRelaySettings _studyRelay = new();
    private StoredProtectiveRelaySettings _fieldRelay = new();

    public StoredProtectiveRelaySettings StudyRelay
    {
        get => _studyRelay;
        set => _studyRelay = value ?? new StoredProtectiveRelaySettings();
    }

    public StoredProtectiveRelaySettings FieldRelay
    {
        get => _fieldRelay;
        set => _fieldRelay = value ?? new StoredProtectiveRelaySettings();
    }

    [JsonIgnore]
    public bool HasAnySettings => StudyRelay.HasValues || FieldRelay.HasValues;
}

public class StoredProtectiveRelaySettings
{
    [JsonConverter(typeof(StringEnumConverter))]
    public ProtectiveRelayService.RelayFunction? Function { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public ProtectiveRelayService.CurveType? Curve { get; set; }

    public double? CtRatio { get; set; }
    public double? PickupAmps { get; set; }
    public double? TimeDial { get; set; }
    public double? InstantaneousAmps { get; set; }

    [JsonIgnore]
    public bool HasValues => Function.HasValue
        || Curve.HasValue
        || CtRatio.HasValue
        || PickupAmps.HasValue
        || TimeDial.HasValue
        || InstantaneousAmps.HasValue;
}

    public enum ComponentInteropReviewStatus
    {
        Unreviewed,
        Reviewed,
        NeedsChanges
    }

public enum ComponentType
{
    Conduit,
    Box,
    Panel,
    Support,
    CableTray,
    Hanger,
    Transformer,
    Bus,
    PowerSource,
    TransferSwitch
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

    public double GetLengthValue(ProjectParameterBindingTarget target)
    {
        return target switch
        {
            ProjectParameterBindingTarget.Width => Width,
            ProjectParameterBindingTarget.Height => Height,
            ProjectParameterBindingTarget.Depth => Depth,
            ProjectParameterBindingTarget.Elevation => Elevation,
            _ => throw new InvalidOperationException($"Binding target '{target}' does not store a length value.")
        };
    }

    public void SetLengthValue(ProjectParameterBindingTarget target, double value)
    {
        switch (target)
        {
            case ProjectParameterBindingTarget.Width:
                Width = value;
                break;
            case ProjectParameterBindingTarget.Height:
                Height = value;
                break;
            case ProjectParameterBindingTarget.Depth:
                Depth = value;
                break;
            case ProjectParameterBindingTarget.Elevation:
                Elevation = value;
                break;
            default:
                throw new InvalidOperationException($"Binding target '{target}' does not store a length value.");
        }
    }

    public string GetTextValue(ProjectParameterBindingTarget target)
    {
        return target switch
        {
            ProjectParameterBindingTarget.Material => Material,
            ProjectParameterBindingTarget.Manufacturer => Manufacturer,
            ProjectParameterBindingTarget.PartNumber => PartNumber,
            ProjectParameterBindingTarget.ReferenceUrl => ReferenceUrl,
            _ => throw new InvalidOperationException($"Binding target '{target}' does not store text.")
        };
    }

    public void SetTextValue(ProjectParameterBindingTarget target, string? value)
    {
        var nextValue = value ?? string.Empty;
        switch (target)
        {
            case ProjectParameterBindingTarget.Material:
                Material = nextValue;
                break;
            case ProjectParameterBindingTarget.Manufacturer:
                Manufacturer = nextValue;
                break;
            case ProjectParameterBindingTarget.PartNumber:
                PartNumber = nextValue;
                break;
            case ProjectParameterBindingTarget.ReferenceUrl:
                ReferenceUrl = nextValue;
                break;
            default:
                throw new InvalidOperationException($"Binding target '{target}' does not store text.");
        }
    }

    public string? GetBinding(ProjectParameterBindingTarget target)
    {
        return target switch
        {
            ProjectParameterBindingTarget.Width => Bindings.WidthParameterId,
            ProjectParameterBindingTarget.Height => Bindings.HeightParameterId,
            ProjectParameterBindingTarget.Depth => Bindings.DepthParameterId,
            ProjectParameterBindingTarget.Elevation => Bindings.ElevationParameterId,
            ProjectParameterBindingTarget.Material => Bindings.MaterialParameterId,
            ProjectParameterBindingTarget.Manufacturer => Bindings.ManufacturerParameterId,
            ProjectParameterBindingTarget.PartNumber => Bindings.PartNumberParameterId,
            ProjectParameterBindingTarget.ReferenceUrl => Bindings.ReferenceUrlParameterId,
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
            case ProjectParameterBindingTarget.Material:
                Bindings.MaterialParameterId = parameterId;
                break;
            case ProjectParameterBindingTarget.Manufacturer:
                Bindings.ManufacturerParameterId = parameterId;
                break;
            case ProjectParameterBindingTarget.PartNumber:
                Bindings.PartNumberParameterId = parameterId;
                break;
            case ProjectParameterBindingTarget.ReferenceUrl:
                Bindings.ReferenceUrlParameterId = parameterId;
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
        if (string.Equals(Bindings.MaterialParameterId, parameterId, StringComparison.Ordinal))
            Bindings.MaterialParameterId = null;
        if (string.Equals(Bindings.ManufacturerParameterId, parameterId, StringComparison.Ordinal))
            Bindings.ManufacturerParameterId = null;
        if (string.Equals(Bindings.PartNumberParameterId, parameterId, StringComparison.Ordinal))
            Bindings.PartNumberParameterId = null;
        if (string.Equals(Bindings.ReferenceUrlParameterId, parameterId, StringComparison.Ordinal))
            Bindings.ReferenceUrlParameterId = null;
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
    public string? MaterialParameterId { get; set; }
    public string? ManufacturerParameterId { get; set; }
    public string? PartNumberParameterId { get; set; }
    public string? ReferenceUrlParameterId { get; set; }

    [JsonIgnore]
    public bool HasAnyBinding =>
        !string.IsNullOrWhiteSpace(WidthParameterId) ||
        !string.IsNullOrWhiteSpace(HeightParameterId) ||
        !string.IsNullOrWhiteSpace(DepthParameterId) ||
        !string.IsNullOrWhiteSpace(ElevationParameterId) ||
        !string.IsNullOrWhiteSpace(MaterialParameterId) ||
        !string.IsNullOrWhiteSpace(ManufacturerParameterId) ||
        !string.IsNullOrWhiteSpace(PartNumberParameterId) ||
        !string.IsNullOrWhiteSpace(ReferenceUrlParameterId);
}

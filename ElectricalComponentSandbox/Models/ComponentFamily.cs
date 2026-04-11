namespace ElectricalComponentSandbox.Models;

/// <summary>
/// Domain that a connector services — mirrors Revit's <c>Domain</c> enum.
/// </summary>
public enum ConnectorDomain
{
    Electrical,
    Conduit,
    CableTray
}

/// <summary>
/// Direction of flow through a connector.
/// </summary>
public enum ConnectorFlow
{
    In,
    Out,
    Bidirectional
}

/// <summary>
/// Defines a connector port on a component family type.
/// Maps to Revit's <c>ConnectorElement</c> + <c>Connector</c> properties.
/// </summary>
public class ConnectorDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public ConnectorDomain Domain { get; set; } = ConnectorDomain.Electrical;
    public ConnectorFlow Flow { get; set; } = ConnectorFlow.Bidirectional;

    /// <summary>Connector position relative to the component origin (local coords).</summary>
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double OffsetZ { get; set; }
}

/// <summary>
/// A named type within a component family, analogous to Revit's <c>FamilySymbol</c>.
/// Each type can override a subset of <see cref="ComponentParameters"/> properties.
/// </summary>
public class ComponentFamilyType
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FamilyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parameter overrides keyed by <see cref="ComponentParameters"/> property name
    /// (e.g. "Width", "Material", "Manufacturer"). Values are stored as strings
    /// and parsed/applied by the service layer.
    /// </summary>
    public Dictionary<string, string> ParameterOverrides { get; set; } = new();

    /// <summary>Connector ports defined for this type.</summary>
    public List<ConnectorDefinition> Connectors { get; set; } = new();
}

/// <summary>
/// A component family containing one or more types, analogous to Revit's <c>Family</c>.
/// Instances placed in the project reference a specific <see cref="ComponentFamilyType"/>.
/// </summary>
public class ComponentFamily
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    /// <summary>The component category this family belongs to.</summary>
    public ComponentType Category { get; set; }

    /// <summary>ID of the default type used when placing without explicit selection.</summary>
    public string DefaultTypeId { get; set; } = string.Empty;

    /// <summary>All types within this family.</summary>
    public List<ComponentFamilyType> Types { get; set; } = new();

    /// <summary>Whether this family is a built-in system family.</summary>
    public bool IsBuiltIn { get; set; }
}

using ElectricalComponentSandbox.Services.RevitIntrospection;

namespace ElectricalComponentSandbox.Services.RevitIntrospection.Adapters;

public interface IGeometryNode
{
    string SourceTypeName { get; }
    string NodeKind { get; }
    IReadOnlyDictionary<string, string> Metadata { get; }
}

public interface IMeasurementValue
{
    double Value { get; }
    string? UnitSymbol { get; }
    string? SpecIdentifier { get; }
}

public interface IUnitSpec
{
    string Name { get; }
    string? Identifier { get; }
}

public sealed record GeometryNodeSnapshot(
    string SourceTypeName,
    string NodeKind,
    IReadOnlyDictionary<string, string> Metadata) : IGeometryNode;

public sealed record MeasurementValueSnapshot(
    double Value,
    string? UnitSymbol,
    string? SpecIdentifier) : IMeasurementValue;

public sealed record UnitSpecSnapshot(
    string Name,
    string? Identifier) : IUnitSpec;

public sealed class RevitGeometryMeasurementAdapterSnapshot
{
    public IReadOnlyList<IGeometryNode> GeometryNodes { get; init; } = Array.Empty<IGeometryNode>();
    public IReadOnlyList<IMeasurementValue> MeasurementValues { get; init; } = Array.Empty<IMeasurementValue>();
    public IReadOnlyList<IUnitSpec> UnitSpecs { get; init; } = Array.Empty<IUnitSpec>();
}

public interface IRevitGeometryMeasurementAdapter
{
    RevitGeometryMeasurementAdapterSnapshot BuildSnapshot(RevitIntrospectionReport report);
}

public sealed class MetadataDrivenRevitGeometryMeasurementAdapter : IRevitGeometryMeasurementAdapter
{
    public RevitGeometryMeasurementAdapterSnapshot BuildSnapshot(RevitIntrospectionReport report)
    {
        var geometryNodes = report.GeometryIndex.TopTypes
            .Select(type => new GeometryNodeSnapshot(
                SourceTypeName: RemoveCountSuffix(type),
                NodeKind: InferNodeKind(type),
                Metadata: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = "metadata-introspection",
                    ["note"] = "scaffold-only-no-runtime-binding"
                }))
            .Cast<IGeometryNode>()
            .ToList();

        var unitSpecs = report.UnitsAndParametersIndex.TopTypes
            .Select(type => new UnitSpecSnapshot(
                Name: RemoveCountSuffix(type),
                Identifier: null))
            .Cast<IUnitSpec>()
            .ToList();

        return new RevitGeometryMeasurementAdapterSnapshot
        {
            GeometryNodes = geometryNodes,
            MeasurementValues = Array.Empty<IMeasurementValue>(),
            UnitSpecs = unitSpecs
        };
    }

    private static string InferNodeKind(string value)
    {
        if (value.Contains("Solid", StringComparison.OrdinalIgnoreCase))
            return "Solid";
        if (value.Contains("Face", StringComparison.OrdinalIgnoreCase))
            return "Face";
        if (value.Contains("Edge", StringComparison.OrdinalIgnoreCase))
            return "Edge";
        if (value.Contains("Curve", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Line", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Arc", StringComparison.OrdinalIgnoreCase))
            return "Curve";

        return "Unknown";
    }

    private static string RemoveCountSuffix(string value)
    {
        var marker = value.LastIndexOf(" (", StringComparison.Ordinal);
        return marker > 0 ? value[..marker] : value;
    }
}

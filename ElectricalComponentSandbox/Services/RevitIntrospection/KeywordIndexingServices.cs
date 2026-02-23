namespace ElectricalComponentSandbox.Services.RevitIntrospection;

public interface IGeometryKeywordIndexer
{
    KeywordIndexSection BuildGeometryIndex(
        IReadOnlyList<ManagedAssemblyMetadata> assemblies,
        int maxItemsPerSection);

    KeywordIndexSection BuildIndex(
        IReadOnlyList<ManagedAssemblyMetadata> assemblies,
        IReadOnlyList<string> keywords,
        int maxItemsPerSection);
}

public interface IUnitsAndParametersInspector
{
    KeywordIndexSection BuildUnitsAndParametersIndex(
        IReadOnlyList<ManagedAssemblyMetadata> assemblies,
        int maxItemsPerSection);
}

public sealed class GeometryKeywordIndexer : IGeometryKeywordIndexer
{
    public static readonly IReadOnlyList<string> GeometryKeywords =
    [
        "geometry",
        "geom",
        "solid",
        "face",
        "edge",
        "curve",
        "line",
        "arc",
        "reference",
        "intersect",
        "distance"
    ];

    public KeywordIndexSection BuildGeometryIndex(
        IReadOnlyList<ManagedAssemblyMetadata> assemblies,
        int maxItemsPerSection)
    {
        return BuildIndex(assemblies, GeometryKeywords, maxItemsPerSection);
    }

    public KeywordIndexSection BuildIndex(
        IReadOnlyList<ManagedAssemblyMetadata> assemblies,
        IReadOnlyList<string> keywords,
        int maxItemsPerSection)
    {
        var namespaceHits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var typeHits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var methodHits = new Dictionary<string, int>(StringComparer.Ordinal);
        var propertyHits = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.Types)
            {
                if (ContainsKeyword(type.Namespace, keywords))
                    Increment(namespaceHits, type.Namespace);

                if (ContainsKeyword(type.FullTypeName, keywords))
                {
                    Increment(typeHits, type.FullTypeName);
                    if (!string.IsNullOrWhiteSpace(type.Namespace))
                        Increment(namespaceHits, type.Namespace);
                }

                foreach (var method in type.Methods)
                {
                    if (ContainsKeyword(method, keywords))
                        Increment(methodHits, method);
                }

                foreach (var property in type.Properties)
                {
                    if (ContainsKeyword(property, keywords))
                        Increment(propertyHits, property);
                }
            }
        }

        return new KeywordIndexSection
        {
            TopNamespaces = FormatCounts(namespaceHits, maxItemsPerSection),
            TopTypes = FormatCounts(typeHits, maxItemsPerSection),
            NotableMethods = FormatCounts(methodHits, maxItemsPerSection),
            NotableProperties = FormatCounts(propertyHits, maxItemsPerSection)
        };
    }

    private static bool ContainsKeyword(string? value, IReadOnlyList<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        for (int i = 0; i < keywords.Count; i++)
        {
            if (value.Contains(keywords[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IReadOnlyList<string> FormatCounts(Dictionary<string, int> input, int maxItems)
    {
        return input
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .Select(pair => $"{pair.Key} ({pair.Value})")
            .ToList();
    }

    private static void Increment(Dictionary<string, int> map, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        map.TryGetValue(key, out var count);
        map[key] = count + 1;
    }
}

public sealed class UnitsAndParametersInspector : IUnitsAndParametersInspector
{
    public static readonly IReadOnlyList<string> UnitAndParameterKeywords =
    [
        "measure",
        "measurement",
        "dimension",
        "unit",
        "spec",
        "parameter",
        "format",
        "convert"
    ];

    private readonly IGeometryKeywordIndexer _keywordIndexer;

    public UnitsAndParametersInspector(IGeometryKeywordIndexer keywordIndexer)
    {
        _keywordIndexer = keywordIndexer;
    }

    public KeywordIndexSection BuildUnitsAndParametersIndex(
        IReadOnlyList<ManagedAssemblyMetadata> assemblies,
        int maxItemsPerSection)
    {
        return _keywordIndexer.BuildIndex(assemblies, UnitAndParameterKeywords, maxItemsPerSection);
    }
}

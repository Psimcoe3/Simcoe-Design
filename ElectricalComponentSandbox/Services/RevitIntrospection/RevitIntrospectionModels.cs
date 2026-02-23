namespace ElectricalComponentSandbox.Services.RevitIntrospection;

public enum RevitBinaryClassification
{
    Missing = 0,
    ManagedAssembly = 1,
    NativeBinary = 2
}

public sealed record RevitBinaryInfo(
    string FileName,
    string FullPath,
    bool Exists,
    RevitBinaryClassification Classification,
    long? FileSizeBytes,
    string? FileVersion);

public sealed class RevitBinaryCatalog
{
    public string InstallPath { get; init; } = string.Empty;
    public IReadOnlyList<RevitBinaryInfo> Entries { get; init; } = Array.Empty<RevitBinaryInfo>();
}

public sealed record ManagedTypeMetadata(
    string Namespace,
    string FullTypeName,
    IReadOnlyList<string> Methods,
    IReadOnlyList<string> Properties);

public sealed class ManagedAssemblyMetadata
{
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public string AssemblyName { get; init; } = string.Empty;
    public string? AssemblyVersion { get; init; }
    public string? InspectionError { get; init; }
    public int ExportedTypeCount { get; init; }
    public IReadOnlyList<ManagedTypeMetadata> Types { get; init; } = Array.Empty<ManagedTypeMetadata>();
}

public sealed class KeywordIndexSection
{
    public IReadOnlyList<string> TopNamespaces { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> TopTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NotableMethods { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> NotableProperties { get; init; } = Array.Empty<string>();
}

public sealed class RevitIntrospectionReport
{
    public DateTime GeneratedUtc { get; init; } = DateTime.UtcNow;
    public string ScannedPath { get; init; } = string.Empty;
    public RevitBinaryCatalog BinaryCatalog { get; init; } = new();
    public IReadOnlyList<ManagedAssemblyMetadata> ManagedAssemblies { get; init; } = Array.Empty<ManagedAssemblyMetadata>();
    public KeywordIndexSection GeometryIndex { get; init; } = new();
    public KeywordIndexSection UnitsAndParametersIndex { get; init; } = new();
    public IReadOnlyList<string> RecommendedNextIntegrationPoints { get; init; } = Array.Empty<string>();
}

public sealed record RevitIntrospectionOutput(string JsonReportPath, string SummaryReportPath);

public sealed class RevitIntrospectionExecutionResult
{
    public bool Success { get; init; }
    public bool RequiresInstallPathSelection { get; init; }
    public string UserMessage { get; init; } = string.Empty;
    public RevitIntrospectionReport? Report { get; init; }
    public RevitIntrospectionOutput? Output { get; init; }

    public static RevitIntrospectionExecutionResult Failed(string message, bool requiresInstallPathSelection = false)
    {
        return new RevitIntrospectionExecutionResult
        {
            Success = false,
            RequiresInstallPathSelection = requiresInstallPathSelection,
            UserMessage = message
        };
    }

    public static RevitIntrospectionExecutionResult Completed(
        string message,
        RevitIntrospectionReport report,
        RevitIntrospectionOutput output)
    {
        return new RevitIntrospectionExecutionResult
        {
            Success = true,
            UserMessage = message,
            Report = report,
            Output = output
        };
    }
}

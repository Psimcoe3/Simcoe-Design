namespace ElectricalComponentSandbox.Services.RevitIntrospection;

public sealed class RevitIntrospectionOptions
{
    public const string EnableEnvVar = "REVIT_INTROSPECTION_ENABLED";
    public const string InstallPathEnvVar = "REVIT_INSTALL_DIR";
    public const string OutputDirEnvVar = "REVIT_INTROSPECTION_OUTPUT_DIR";

    public bool IsEnabled { get; init; } = true;
    public string? InstallPathOverride { get; init; }
    public string? OutputDirectoryOverride { get; init; }
    public IReadOnlyList<string> TargetFileNames { get; init; } =
    [
        "RevitDB.dll",
        "RevitDBCore.dll",
        "GeomUtil.dll",
        "ASMAHL229A.dll",
        "ForgeUnits.dll",
        "ForgeParameters.dll"
    ];

    public int MaxTypesPerAssembly { get; init; } = 350;
    public int MaxMembersPerType { get; init; } = 40;
    public int MaxIndexedItemsPerSection { get; init; } = 50;

    public static RevitIntrospectionOptions FromEnvironment()
    {
        var enabled = ParseBoolean(Environment.GetEnvironmentVariable(EnableEnvVar), defaultValue: true);
        var installPath = Environment.GetEnvironmentVariable(InstallPathEnvVar);
        var outputPath = Environment.GetEnvironmentVariable(OutputDirEnvVar);

        return new RevitIntrospectionOptions
        {
            IsEnabled = enabled,
            InstallPathOverride = string.IsNullOrWhiteSpace(installPath) ? null : installPath.Trim(),
            OutputDirectoryOverride = string.IsNullOrWhiteSpace(outputPath) ? null : outputPath.Trim()
        };
    }

    private static bool ParseBoolean(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        var normalized = value.Trim();
        if (bool.TryParse(normalized, out var parsed))
            return parsed;

        return normalized switch
        {
            "1" => true,
            "0" => false,
            _ => defaultValue
        };
    }
}

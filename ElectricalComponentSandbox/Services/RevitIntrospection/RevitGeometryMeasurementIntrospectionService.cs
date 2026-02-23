namespace ElectricalComponentSandbox.Services.RevitIntrospection;

public sealed class RevitGeometryMeasurementIntrospectionService
{
    private readonly RevitIntrospectionOptions _options;
    private readonly IRevitInstallLocator _installLocator;
    private readonly IRevitBinaryCatalogService _binaryCatalogService;
    private readonly IRevitManagedAssemblyInspector _managedAssemblyInspector;
    private readonly IGeometryKeywordIndexer _geometryKeywordIndexer;
    private readonly IUnitsAndParametersInspector _unitsAndParametersInspector;
    private readonly IIntrospectionReportWriter _reportWriter;

    public RevitGeometryMeasurementIntrospectionService(
        RevitIntrospectionOptions options,
        IRevitInstallLocator installLocator,
        IRevitBinaryCatalogService binaryCatalogService,
        IRevitManagedAssemblyInspector managedAssemblyInspector,
        IGeometryKeywordIndexer geometryKeywordIndexer,
        IUnitsAndParametersInspector unitsAndParametersInspector,
        IIntrospectionReportWriter reportWriter)
    {
        _options = options;
        _installLocator = installLocator;
        _binaryCatalogService = binaryCatalogService;
        _managedAssemblyInspector = managedAssemblyInspector;
        _geometryKeywordIndexer = geometryKeywordIndexer;
        _unitsAndParametersInspector = unitsAndParametersInspector;
        _reportWriter = reportWriter;
    }

    public static RevitGeometryMeasurementIntrospectionService CreateDefault(RevitIntrospectionOptions? options = null)
    {
        var resolvedOptions = options ?? RevitIntrospectionOptions.FromEnvironment();
        var indexer = new GeometryKeywordIndexer();

        return new RevitGeometryMeasurementIntrospectionService(
            resolvedOptions,
            new RevitInstallLocator(),
            new RevitBinaryCatalogService(),
            new RevitManagedAssemblyInspector(),
            indexer,
            new UnitsAndParametersInspector(indexer),
            new IntrospectionReportWriter());
    }

    public RevitIntrospectionExecutionResult RunScan(string? installPathOverride = null)
    {
        if (!_options.IsEnabled)
        {
            return RevitIntrospectionExecutionResult.Failed(
                $"Revit introspection is disabled. Set {RevitIntrospectionOptions.EnableEnvVar}=true to enable.");
        }

        var installPath = _installLocator.LocateInstallPath(installPathOverride ?? _options.InstallPathOverride);
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return RevitIntrospectionExecutionResult.Failed(
                "No Revit installation folder could be located. Provide a path override or set REVIT_INSTALL_DIR.",
                requiresInstallPathSelection: true);
        }

        var catalog = _binaryCatalogService.BuildCatalog(installPath, _options.TargetFileNames);
        var managedAssemblyMetadata = _managedAssemblyInspector.InspectAssemblies(catalog.Entries, _options);
        var geometryIndex = _geometryKeywordIndexer.BuildGeometryIndex(managedAssemblyMetadata, _options.MaxIndexedItemsPerSection);
        var unitsAndParametersIndex = _unitsAndParametersInspector.BuildUnitsAndParametersIndex(managedAssemblyMetadata, _options.MaxIndexedItemsPerSection);

        var report = new RevitIntrospectionReport
        {
            GeneratedUtc = DateTime.UtcNow,
            ScannedPath = installPath,
            BinaryCatalog = catalog,
            ManagedAssemblies = managedAssemblyMetadata,
            GeometryIndex = geometryIndex,
            UnitsAndParametersIndex = unitsAndParametersIndex,
            RecommendedNextIntegrationPoints = BuildRecommendations(catalog, geometryIndex, unitsAndParametersIndex)
        };

        var output = _reportWriter.WriteReports(report, _options.OutputDirectoryOverride);

        return RevitIntrospectionExecutionResult.Completed(
            "Revit geometry and measurement introspection completed.",
            report,
            output);
    }

    private static IReadOnlyList<string> BuildRecommendations(
        RevitBinaryCatalog catalog,
        KeywordIndexSection geometryIndex,
        KeywordIndexSection unitsIndex)
    {
        var recommendations = new List<string>();

        if (catalog.Entries.Any(entry => entry.FileName.Equals("ASMAHL229A.dll", StringComparison.OrdinalIgnoreCase) && entry.Exists))
        {
            recommendations.Add("Treat ASM binaries as native dependencies only; keep integration at metadata/reporting level unless a supported managed wrapper exists.");
        }

        if (geometryIndex.TopTypes.Any(type => type.Contains("Autodesk.Revit.DB", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add("Create app-level geometry abstractions around discovered Revit DB types (Solid/Face/Edge/Curve) instead of direct UI coupling.");
        }

        if (unitsIndex.TopTypes.Any(type => type.Contains("Unit", StringComparison.OrdinalIgnoreCase) ||
                                            type.Contains("Spec", StringComparison.OrdinalIgnoreCase) ||
                                            type.Contains("Parameter", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add("Build a translation layer mapping Revit specs/units/parameters into app-native measurement models with explicit conversion boundaries.");
        }

        if (catalog.Entries.Any(entry => !entry.Exists))
        {
            recommendations.Add("Add per-binary capability checks before future adapter steps so missing DLLs degrade gracefully without blocking the rest of the app.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Use the generated metadata report to define a strict adapter contract before introducing any runtime Revit integration points.");
        }

        return recommendations;
    }
}

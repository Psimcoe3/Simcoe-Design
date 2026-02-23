using System.IO;
using System.Threading;
using ElectricalComponentSandbox.Services.RevitIntrospection;

namespace ElectricalComponentSandbox.Tests.Services.RevitIntrospection;

public sealed class RevitGeometryMeasurementIntrospectionServiceTests : IDisposable
{
    private readonly string _tempInstallDir;
    private readonly string _tempOutputDir;

    public RevitGeometryMeasurementIntrospectionServiceTests()
    {
        _tempInstallDir = Path.Combine(Path.GetTempPath(), "ECS_RevitInstall_" + Guid.NewGuid().ToString("N"));
        _tempOutputDir = Path.Combine(Path.GetTempPath(), "ECS_RevitOutput_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempInstallDir);
        Directory.CreateDirectory(_tempOutputDir);
    }

    [Fact]
    public void RunScan_WithMockedBinaryInputs_GeneratesReports()
    {
        var sourceManagedAssembly = typeof(RevitGeometryMeasurementIntrospectionServiceTests).Assembly.Location;

        File.Copy(sourceManagedAssembly, Path.Combine(_tempInstallDir, "RevitDB.dll"), overwrite: true);
        File.Copy(sourceManagedAssembly, Path.Combine(_tempInstallDir, "RevitDBCore.dll"), overwrite: true);
        File.Copy(sourceManagedAssembly, Path.Combine(_tempInstallDir, "GeomUtil.dll"), overwrite: true);
        File.Copy(sourceManagedAssembly, Path.Combine(_tempInstallDir, "ForgeUnits.dll"), overwrite: true);
        File.Copy(sourceManagedAssembly, Path.Combine(_tempInstallDir, "ForgeParameters.dll"), overwrite: true);
        File.WriteAllText(Path.Combine(_tempInstallDir, "ASMAHL229A.dll"), "native-placeholder");

        var options = new RevitIntrospectionOptions
        {
            IsEnabled = true,
            OutputDirectoryOverride = _tempOutputDir
        };

        var keywordIndexer = new GeometryKeywordIndexer();
        var service = new RevitGeometryMeasurementIntrospectionService(
            options,
            new StaticInstallLocator(_tempInstallDir),
            new RevitBinaryCatalogService(),
            new RevitManagedAssemblyInspector(),
            keywordIndexer,
            new UnitsAndParametersInspector(keywordIndexer),
            new IntrospectionReportWriter());

        var result = service.RunScan();

        Assert.True(result.Success);
        Assert.NotNull(result.Report);
        Assert.NotNull(result.Output);
        Assert.True(File.Exists(result.Output!.JsonReportPath));
        Assert.True(File.Exists(result.Output.SummaryReportPath));

        var asmEntry = Assert.Single(result.Report!.BinaryCatalog.Entries.Where(entry => entry.FileName == "ASMAHL229A.dll"));
        Assert.Equal(RevitBinaryClassification.NativeBinary, asmEntry.Classification);
    }

    [Fact]
    public void RunScan_WhenDisabled_ReturnsFailedResult()
    {
        var keywordIndexer = new GeometryKeywordIndexer();
        var service = new RevitGeometryMeasurementIntrospectionService(
            new RevitIntrospectionOptions { IsEnabled = false },
            new StaticInstallLocator(_tempInstallDir),
            new RevitBinaryCatalogService(),
            new RevitManagedAssemblyInspector(),
            keywordIndexer,
            new UnitsAndParametersInspector(keywordIndexer),
            new IntrospectionReportWriter());

        var result = service.RunScan();

        Assert.False(result.Success);
        Assert.Contains("disabled", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        TryDeleteDirectory(_tempInstallDir);
        TryDeleteDirectory(_tempOutputDir);
    }

    private sealed class StaticInstallLocator : IRevitInstallLocator
    {
        private readonly string _path;

        public StaticInstallLocator(string path)
        {
            _path = path;
        }

        public string? LocateInstallPath(string? overridePath = null)
        {
            if (!string.IsNullOrWhiteSpace(overridePath))
                return overridePath;
            return _path;
        }

        public IReadOnlyList<string> DiscoverCandidateInstallPaths(string? overridePath = null)
        {
            return [overridePath ?? _path];
        }
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(directoryPath, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100);
            }
        }
    }
}

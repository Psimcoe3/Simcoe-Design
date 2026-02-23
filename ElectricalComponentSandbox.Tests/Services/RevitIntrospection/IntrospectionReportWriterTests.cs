using System.IO;
using ElectricalComponentSandbox.Services.RevitIntrospection;

namespace ElectricalComponentSandbox.Tests.Services.RevitIntrospection;

public sealed class IntrospectionReportWriterTests : IDisposable
{
    private readonly string _tempDir;

    public IntrospectionReportWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ECS_RevitReport_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void WriteReports_WritesJsonAndSummaryFiles()
    {
        var report = new RevitIntrospectionReport
        {
            ScannedPath = @"C:\Program Files\Autodesk\Revit 2024",
            BinaryCatalog = new RevitBinaryCatalog
            {
                InstallPath = @"C:\Program Files\Autodesk\Revit 2024",
                Entries =
                [
                    new RevitBinaryInfo(
                        FileName: "RevitDB.dll",
                        FullPath: @"C:\Program Files\Autodesk\Revit 2024\RevitDB.dll",
                        Exists: true,
                        Classification: RevitBinaryClassification.ManagedAssembly,
                        FileSizeBytes: 1024,
                        FileVersion: "1.2.3.4")
                ]
            },
            RecommendedNextIntegrationPoints =
            [
                "Use app-level abstractions instead of direct runtime coupling."
            ]
        };

        var writer = new IntrospectionReportWriter();
        var output = writer.WriteReports(report, _tempDir);

        Assert.True(File.Exists(output.JsonReportPath));
        Assert.True(File.Exists(output.SummaryReportPath));

        var json = File.ReadAllText(output.JsonReportPath);
        Assert.Contains("ScannedPath", json, StringComparison.Ordinal);
        Assert.Contains("RevitDB.dll", json, StringComparison.Ordinal);

        var summary = File.ReadAllText(output.SummaryReportPath);
        Assert.Contains("Revit Geometry & Measurement Introspection Summary", summary, StringComparison.Ordinal);
        Assert.Contains("C:\\Program Files\\Autodesk\\Revit 2024", summary, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
